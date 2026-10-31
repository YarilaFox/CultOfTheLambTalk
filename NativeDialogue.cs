using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MMTools;
using TMPro;
using UnityEngine;

namespace FollowerTalk;

internal sealed class NativeDialogue
{
    private const float ConversationZoom = 8f;
    private static readonly MethodInfo? ShowNextLine = typeof(MMConversation).GetMethod(
        "ShowNextLine", BindingFlags.Instance | BindingFlags.NonPublic);
    private static NativeDialogue? active;
    private static int suppressPauseThroughFrame = -1;

    private readonly Plugin plugin;
    private Follower? follower;
    private ConversationObject? conversation;
    private Action<string>? submitted;
    private Action? closed;
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

    public NativeDialogue(Plugin plugin) => this.plugin = plugin;
    private string InputHint => UiText.Get(plugin.UiLanguage, "input.hint");
    private string PlayerName => UiText.Get(plugin.UiLanguage, "speaker.you");

    public static bool IsTalkingTo(Follower follower) =>
        active != null && active.follower == follower && active.IsCurrent();

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
            return false;
        }
        if (current.closeAfterReply) current.closed?.Invoke();
        else current.StartNextTurn();
        return false;
    }

    public static bool AllowPauseMenu() => active == null &&
        Time.frameCount > suppressPauseThroughFrame;

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
        if (replyVisible)
        {
            if (!waiting && !replyFinished &&
                MMConversation.mmConversation.TextPlayer.textAnimator.allLettersShown)
            {
                replyFinished = true;
                FollowerEmotion.StopSpeakingMouth(follower);
                SetFollowerDialogueAnimation("idle");
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

    public void Open(Follower target, Action<string> onSubmit, Action onClose)
    {
        follower = target;
        submitted = onSubmit;
        closed = onClose;
        waiting = false;
        replyVisible = false;
        replyEmotionShown = false;
        replyFinished = false;
        replyEmotion = "neutral";
        closeAfterReply = false;
        closing = false;
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
        waiting = true;
        SetVisibleLine(playerMessage, true);
        MMConversation.mmConversation.NextArrowContainer.SetActive(false);
        plugin.RunConversation(ShowThinkingAfterPause());
    }

    public void ShowReply(string text, bool endConversation = false, string emotion = "neutral")
    {
        if (!IsCurrent()) return;
        if (!replyVisible) SwitchToFollower();
        waiting = false;
        closeAfterReply = endConversation;
        replyEmotion = emotion;
        replyEmotionShown = false;
        replyFinished = false;
        bool mouthOverlay = FollowerEmotion.StartSpeakingMouth(follower);
        if (mouthOverlay)
        {
            if (emotion != "neutral")
            {
                FollowerEmotion.Apply(follower, emotion);
                replyEmotionShown = true;
            }
        }
        else SetFollowerDialogueAnimation("talk");
        SetVisibleLine(text, false);
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
        FollowerEmotion.Restore(follower);
        if (IsCurrent()) MMConversation.mmConversation.CloseImmediately(true, null, true);
        if (active == this) active = null;
        conversation = null;
        follower = null;
    }

    private IEnumerator ShowThinkingAfterPause()
    {
        yield return new WaitForSecondsRealtime(.45f);
        if (IsCurrent() && waiting && !replyVisible) SwitchToFollower();
    }

    private void SwitchToFollower()
    {
        if (!IsCurrent() || follower == null || conversation == null) return;
        replyVisible = true;
        visibleLine = "...";
        currentEntryIndex = conversation.Entries.Count;
        conversation.Entries.Add(new ConversationEntry(follower.gameObject,
            "...", "idle", "", null, follower.Brain.Info.Name)
        {
            SetZoom = true, Zoom = ConversationZoom
        });
        MMConversation.Position = currentEntryIndex - 1;
        ShowNextLine?.Invoke(MMConversation.mmConversation, new object[] { true });
        SetVisibleLine("...", true);
        MMConversation.mmConversation.NextArrowContainer.SetActive(false);
    }

    private void StartNextTurn()
    {
        if (!IsCurrent() || conversation == null) return;
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

    private void SetFollowerDialogueAnimation(string animation)
    {
        try
        {
            var state = follower?.Spine?.AnimationState;
            if (state != null && state.Data.SkeletonData.FindAnimation(animation) != null)
                state.SetAnimation(1, animation, true);
        }
        catch (Exception error) { plugin.LogActionError("dialogue animation", error); }
    }

    private bool IsCurrent() => conversation != null &&
        MMConversation.CURRENT_CONVERSATION == conversation && MMConversation.mmConversation != null;

    private void OnConversationClosed()
    {
        conversation = null;
        if (active == this) active = null;
        if (!closing) closed?.Invoke();
    }
}
