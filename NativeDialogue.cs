using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FMOD.Studio;
using MMTools;
using TMPro;
using UnityEngine;

namespace FollowerTalk;

internal sealed class NativeDialogue
{
    private const float ConversationZoom = 8f;
    private static readonly MethodInfo? ShowNextLine = typeof(MMConversation).GetMethod(
        "ShowNextLine", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? TalkSoundInstance = typeof(MMConversation).GetField(
        "talkSoundInstance", BindingFlags.Instance | BindingFlags.NonPublic);
    private static NativeDialogue? active;
    private static int suppressPauseThroughFrame = -1;

    private readonly Plugin plugin;
    private Follower? follower;
    private ConversationObject? conversation;
    private Action<string>? submitted;
    private Action? closed;
    private Action? interrupted;
    private readonly DialogueDraft draft = new();
    private string visibleLine = "";
    private bool waiting;
    private bool replyVisible;
    private bool replyEmotionShown;
    private bool replyFinished;
    private string replyEmotion = "neutral";
    private bool closeAfterReply;
    private bool settingsVisible;
    private bool closing;
    private int currentEntryIndex;
    private float nextThinkingFrame;
    private int thinkingDots;
    private Febucci.UI.TextAnimatorPlayer? subscribedTextPlayer;
    private float lastVisibleCharacter;
    private bool mouthSpeaking;
    private int visibleLetterEvents;
    private int mouthStarts;

    public NativeDialogue(Plugin plugin) => this.plugin = plugin;
    private string InputHint => UiText.Get(plugin.UiLanguage, "input.hint");
    private string PlayerName => UiText.Get(plugin.UiLanguage, "speaker.you");

    public static bool IsTalkingTo(Follower follower) =>
        active != null && active.follower == follower && active.IsCurrent();

    public bool IsVisible => IsCurrent() &&
        MMConversation.mmConversation.gameObject.activeInHierarchy;

    public void RefreshInputLanguage()
    {
        if (IsCurrent() && !waiting && !replyVisible && draft.Text.Length == 0)
            RenderDraft();
    }

    public static bool AllowAdvance()
    {
        NativeDialogue? current = active;
        if (current == null || MMConversation.CURRENT_CONVERSATION != current.conversation)
            return true;
        if (current.waiting || current.settingsVisible || !current.replyVisible) return false;
        if (!MMConversation.mmConversation.TextPlayer.textAnimator.allLettersShown)
        {
            MMConversation.mmConversation.TextPlayer.SkipTypewriter();
            current.StopSpeaking();
            return false;
        }
        if (current.closeAfterReply) current.closed?.Invoke();
        else current.StartNextTurn();
        return false;
    }

    public static bool AllowPauseMenu()
    {
        if (active != null && !active.IsVisible) active = null;
        return active == null && Time.frameCount > suppressPauseThroughFrame;
    }

    public static void SuppressPauseForEscape() =>
        suppressPauseThroughFrame = Time.frameCount + 1;

    public static bool AllowBackToWork() => Time.frameCount > suppressPauseThroughFrame;

    public static void RestoreText(MMConversation __instance)
    {
        if (active != null && MMConversation.CURRENT_CONVERSATION == active.conversation)
            __instance.TextPlayer.GetComponent<TextMeshProUGUI>().text = active.visibleLine;
    }

    public void SetSettingsVisible(bool visible) => settingsVisible = visible;

    public void HandleUpdate()
    {
        if (!IsCurrent() || settingsVisible) return;
        if (waiting && Time.unscaledTime >= nextThinkingFrame && !replyVisible)
        {
            thinkingDots = thinkingDots % 3 + 1;
            SetVisibleLine(new string('.', thinkingDots), true);
            nextThinkingFrame = Time.unscaledTime + .45f;
        }
        if (replyVisible)
        {
            if (mouthSpeaking && Time.unscaledTime - lastVisibleCharacter > .075f)
                StopSpeaking();
            if (!waiting && !replyFinished &&
                MMConversation.mmConversation.TextPlayer.textAnimator.allLettersShown)
            {
                replyFinished = true;
                StopSpeaking();
                StopFollowerVoice();
                plugin.LogMouthSync(visibleLetterEvents, mouthStarts,
                    MMConversation.mmConversation.TextPlayer.useTypeWriter);
                if (!replyEmotionShown) FollowerEmotion.Apply(follower, replyEmotion);
            }
            return;
        }
        if (waiting) return;
        var text = MMConversation.mmConversation.TextPlayer.GetComponent<TextMeshProUGUI>();
        if (text.text != visibleLine) SetVisibleLine(visibleLine, true);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            string message = draft.Text.Trim();
            if (message.Length > 0) submitted?.Invoke(message);
            return;
        }

        bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (control && Input.GetKeyDown(KeyCode.A)) draft.SelectAll();
        else if (control && Input.GetKeyDown(KeyCode.C))
            GUIUtility.systemCopyBuffer = draft.SelectedText();
        else if (control && Input.GetKeyDown(KeyCode.X))
        {
            if (draft.HasSelection)
            {
                GUIUtility.systemCopyBuffer = draft.SelectedText();
                draft.DeleteForward();
            }
        }
        else if (control && Input.GetKeyDown(KeyCode.V)) draft.Insert(GUIUtility.systemCopyBuffer);
        else if (Input.GetKeyDown(KeyCode.LeftArrow)) draft.MoveLeft(shift);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) draft.MoveRight(shift);
        else if (Input.GetKeyDown(KeyCode.Home)) draft.Move(0, shift);
        else if (Input.GetKeyDown(KeyCode.End)) draft.Move(draft.Text.Length, shift);
        else if (Input.GetKeyDown(KeyCode.Backspace)) draft.DeleteBack();
        else if (Input.GetKeyDown(KeyCode.Delete)) draft.DeleteForward();
        else if (!control)
        {
            string typed = Input.inputString;
            foreach (char character in typed)
                if (!char.IsControl(character)) draft.Insert(character.ToString());
            if (Input.GetKeyDown(KeyCode.Space) && !typed.Contains(" ")) draft.Insert(" ");
        }
        RenderDraft();
    }

    private void RenderDraft()
    {
        string display = draft.Display(InputHint);
        if (display != visibleLine) SetVisibleLine(display, true);
    }

    public void Open(Follower target, Action<string> onSubmit, Action onClose) =>
        OpenInput(target, onSubmit, onClose);

    public void OpenAnnouncement(Action<string> onSubmit, Action onClose,
        Action onInterrupted) =>
        OpenInput(null, onSubmit, onClose, onInterrupted);

    private void OpenInput(Follower? target, Action<string> onSubmit, Action onClose,
        Action? onInterrupted = null)
    {
        follower = target;
        submitted = onSubmit;
        closed = onClose;
        interrupted = onInterrupted;
        waiting = false;
        replyVisible = false;
        replyEmotionShown = false;
        replyFinished = false;
        replyEmotion = "neutral";
        closeAfterReply = false;
        closing = false;
        thinkingDots = 0;
        currentEntryIndex = 0;
        draft.Clear();
        visibleLine = draft.Display(InputHint);
        active = this;

        var playerEntry = new ConversationEntry(PlayerFarming.Instance.gameObject,
            InputHint, "", "", null, PlayerName)
        {
            SpeakerIsPlayer = true, SetZoom = true, Zoom = ConversationZoom
        };
        conversation = new ConversationObject(new List<ConversationEntry> { playerEntry }, null,
            OnConversationClosed, null);
        MMConversation.Play(conversation, true, true, true, true, true, false, false);
        subscribedTextPlayer = MMConversation.mmConversation.TextPlayer;
        subscribedTextPlayer.onCharacterVisible.AddListener(OnCharacterVisible);
        RenderDraft();
        plugin.RunConversation(RenderDraftAfterOpen());
        MMConversation.mmConversation.NextArrowContainer.SetActive(false);
    }

    private IEnumerator RenderDraftAfterOpen()
    {
        yield return null;
        if (IsCurrent() && !waiting && !replyVisible) RenderDraft();
    }

    public void BeginThinking(string playerMessage)
    {
        if (!IsCurrent()) return;
        StopSpeaking();
        StopFollowerVoice();
        waiting = true;
        thinkingDots = 0;
        nextThinkingFrame = Time.unscaledTime;
        SetVisibleLine(playerMessage, true);
        MMConversation.mmConversation.NextArrowContainer.SetActive(false);
    }

    public void ShowAnnouncement(string message)
    {
        if (!IsCurrent()) return;
        replyVisible = true;
        waiting = true;
        closeAfterReply = true;
        SetVisibleLine(message, false);
        MMConversation.mmConversation.NextArrowContainer.SetActive(false);
    }

    public void AnnouncementReady()
    {
        if (!IsCurrent()) return;
        waiting = false;
        MMConversation.mmConversation.NextArrowContainer.SetActive(true);
    }

    public void ShowReply(string text, bool endConversation = false, string emotion = "neutral")
    {
        if (!IsCurrent()) return;
        bool newPage = !replyVisible;
        waiting = false;
        closeAfterReply = endConversation;
        replyEmotion = emotion;
        replyEmotionShown = false;
        replyFinished = false;
        visibleLetterEvents = 0;
        mouthStarts = 0;
        StopSpeaking();
        if (emotion != "neutral")
        {
            FollowerEmotion.Apply(follower, emotion);
            replyEmotionShown = true;
        }
        if (newPage) SwitchToFollower(text);
        else SetVisibleLine(text, false);
        MMConversation.mmConversation.NextArrowContainer.SetActive(true);
    }

    public void ShowError(string text)
    {
        ShowReply(text, false);
        if (!IsCurrent() || conversation == null) return;
        string title = UiText.Get(plugin.UiLanguage, "connection");
        conversation.Entries[currentEntryIndex].CharacterName = title;
        MMConversation.mmConversation.TitleText.text = title;
    }

    public void Close()
    {
        closing = true;
        UnsubscribeTextPlayer();
        StopSpeaking();
        FollowerEmotion.Restore(follower);
        if (IsCurrent()) MMConversation.mmConversation.CloseImmediately(true, null, true);
        if (active == this) active = null;
        conversation = null;
        follower = null;
    }

    private void SwitchToFollower(string text)
    {
        if (!IsCurrent() || follower == null || conversation == null) return;
        replyVisible = true;
        visibleLine = text;
        currentEntryIndex = conversation.Entries.Count;
        conversation.Entries.Add(new ConversationEntry(follower.gameObject,
            text, "idle", "", null, follower.Brain.Info.Name)
        {
            SetZoom = true, Zoom = ConversationZoom,
            SkeletonData = follower.Spine, followerID = follower.Brain.Info.ID
        });
        MMConversation.Position = currentEntryIndex - 1;
        ShowNextLine?.Invoke(MMConversation.mmConversation, new object[] { true });
    }

    private void StartNextTurn()
    {
        if (!IsCurrent() || conversation == null) return;
        StopSpeaking();
        StopFollowerVoice();
        FollowerEmotion.Restore(follower);
        draft.Clear();
        waiting = false;
        replyVisible = false;
        replyEmotionShown = false;
        replyFinished = false;
        replyEmotion = "neutral";
        closeAfterReply = false;
        visibleLine = draft.Display(InputHint);
        currentEntryIndex = conversation.Entries.Count;
        var playerEntry = new ConversationEntry(PlayerFarming.Instance.gameObject,
            InputHint, "", "", null, PlayerName) { SpeakerIsPlayer = true,
                SetZoom = true, Zoom = ConversationZoom };
        conversation.Entries.Add(playerEntry);
        MMConversation.Position = currentEntryIndex - 1;
        ShowNextLine?.Invoke(MMConversation.mmConversation, new object[] { true });
        RenderDraft();
        MMConversation.mmConversation.NextArrowContainer.SetActive(false);
    }

    private void SetVisibleLine(string text, bool instant)
    {
        visibleLine = text;
        if (conversation != null)
        {
            if (conversation.Entries.Count > currentEntryIndex)
                conversation.Entries[currentEntryIndex].TermToSpeak = text;
        }
        if (!IsCurrent()) return;
        if (instant)
        {
            MMConversation.mmConversation.TextPlayer.StopShowingText();
            MMConversation.mmConversation.TextPlayer.GetComponent<TextMeshProUGUI>().text = text;
        }
        else MMConversation.mmConversation.TextPlayer.ShowText(text);
    }

    private void OnCharacterVisible(char character)
    {
        if (!IsCurrent() || !replyVisible || waiting || replyFinished) return;
        if (!char.IsLetterOrDigit(character))
        {
            StopSpeaking();
            return;
        }
        lastVisibleCharacter = Time.unscaledTime;
        visibleLetterEvents++;
        if (mouthSpeaking) FollowerEmotion.AdvanceSpeakingMouth(follower);
        else
        {
            mouthSpeaking = FollowerEmotion.StartSpeakingMouth(follower);
            if (mouthSpeaking) mouthStarts++;
        }
    }

    private void StopSpeaking()
    {
        if (!mouthSpeaking) return;
        FollowerEmotion.StopSpeakingMouth(follower);
        mouthSpeaking = false;
    }

    private void StopFollowerVoice()
    {
        if (!IsCurrent() || TalkSoundInstance == null) return;
        try
        {
            var conversationView = MMConversation.mmConversation;
            if (TalkSoundInstance.GetValue(conversationView) is EventInstance sound && sound.isValid())
            {
                AudioManager.Instance.StopLoop(sound, STOP_MODE.IMMEDIATE);
                TalkSoundInstance.SetValue(conversationView, default(EventInstance));
            }
        }
        catch (Exception error) { plugin.LogActionError("dialogue voice", error); }
    }

    private void UnsubscribeTextPlayer()
    {
        if (subscribedTextPlayer == null) return;
        subscribedTextPlayer.onCharacterVisible.RemoveListener(OnCharacterVisible);
        subscribedTextPlayer = null;
    }

    private bool IsCurrent() => conversation != null &&
        MMConversation.CURRENT_CONVERSATION == conversation && MMConversation.mmConversation != null;

    private void OnConversationClosed()
    {
        UnsubscribeTextPlayer();
        StopSpeaking();
        conversation = null;
        if (active == this) active = null;
        if (!closing) (interrupted ?? closed)?.Invoke();
    }
}
