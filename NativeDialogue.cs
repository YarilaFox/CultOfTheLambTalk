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
    private const string InputHint = "Напишите что-нибудь или выйдите на Esc.";
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
    private string draft = "";
    private string visibleLine = "";
    private bool waiting;
    private bool replyVisible;
    private bool closeAfterReply;
    private bool settingsVisible;
    private bool closing;
    private int currentEntryIndex;

    public NativeDialogue(Plugin plugin) => this.plugin = plugin;

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
        if (!IsCurrent() || waiting || replyVisible || settingsVisible) return;
        var text = MMConversation.mmConversation.TextPlayer.GetComponent<TextMeshProUGUI>();
        if (text.text != visibleLine) SetVisibleLine(visibleLine, true);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            string message = draft.Trim();
            if (message.Length > 0) submitted?.Invoke(message);
            return;
        }

        bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (control && Input.GetKeyDown(KeyCode.V))
        {
            Append(GUIUtility.systemCopyBuffer);
            return;
        }
        string before = draft;
        if (Input.GetKeyDown(KeyCode.Backspace) && draft.Length > 0)
            draft = draft.Substring(0, draft.Length - 1);
        if (!control)
            foreach (char character in Input.inputString)
                if (!char.IsControl(character) && draft.Length < 320) draft += character;
        if (before != draft) RenderDraft();
    }

    private void Append(string value)
    {
        foreach (char character in value)
            if (character == '\r' || character == '\n')
            {
                if (draft.Length < 320) draft += ' ';
            }
            else if (!char.IsControl(character) && draft.Length < 320) draft += character;
        RenderDraft();
    }

    private void RenderDraft() => SetVisibleLine(draft.Length == 0
        ? InputHint
        : draft + "▌", true);

    public void Open(Follower target, Action<string> onSubmit, Action onClose)
    {
        follower = target;
        submitted = onSubmit;
        closed = onClose;
        waiting = false;
        replyVisible = false;
        closeAfterReply = false;
        closing = false;
        currentEntryIndex = 0;
        draft = "";
        visibleLine = InputHint;
        active = this;

        var playerEntry = new ConversationEntry(PlayerFarming.Instance.gameObject,
            InputHint, "", "", null, "Вы")
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
        SetVisibleLine(text, false);
        FollowerEmotion.Apply(follower, emotion);
        MMConversation.mmConversation.NextArrowContainer.SetActive(true);
    }

    public void ShowError(string text)
    {
        ShowReply(text, false);
        if (!IsCurrent() || conversation == null) return;
        conversation.Entries[currentEntryIndex].CharacterName = "Связь";
        MMConversation.mmConversation.TitleText.text = "Связь";
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
            "...", "talk", "", null, follower.Brain.Info.Name)
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
        draft = "";
        waiting = false;
        replyVisible = false;
        closeAfterReply = false;
        visibleLine = InputHint;
        currentEntryIndex = conversation.Entries.Count;
        var playerEntry = new ConversationEntry(PlayerFarming.Instance.gameObject,
            InputHint, "", "", null, "Вы") { SpeakerIsPlayer = true,
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

    private bool IsCurrent() => conversation != null &&
        MMConversation.CURRENT_CONVERSATION == conversation && MMConversation.mmConversation != null;

    private void OnConversationClosed()
    {
        conversation = null;
        if (active == this) active = null;
        if (!closing) closed?.Invoke();
    }
}
