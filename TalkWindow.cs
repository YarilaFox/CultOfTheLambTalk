using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace FollowerTalk;

internal sealed partial class TalkWindow
{
    private readonly Plugin plugin;
    private readonly NativeDialogue native;
    private readonly Dictionary<string, string> stagedKeys = new();
    private readonly Dictionary<string, string> stagedModels = new();
    private Follower? selectedFollower;
    private FollowerActivity observedActivity;
    private interaction_FollowerInteraction? heldInteraction;
    private string? pendingAction;
    private bool talkOpen;
    private bool settingsOpen;
    private bool broadcastOpen;
    private bool broadcastFocus;
    private string broadcastText = "";
    private string broadcastStatus = "";
    private bool revealKey;
    private bool waiting;
    private float previousTimeScale;
    private bool pendingFocus;
    private int session;
    private string status = "";
    private string editedKey = "";
    private string editedModel = "";
    private string selectedProvider = Plugin.OpenAi;
    private string SettingsLanguage => plugin.UiLanguage;
    public TalkWindow(Plugin plugin)
    {
        this.plugin = plugin;
        native = new NativeDialogue(plugin);
    }

    public void OpenTalk(Follower follower, interaction_FollowerInteraction? interaction,
        FollowerActivity? activity = null)
    {
        if (talkOpen) CloseTalk();
        selectedFollower = follower;
        observedActivity = activity ?? FollowerContext.Snapshot(follower, interaction);
        heldInteraction = interaction;
        pendingAction = null;
        talkOpen = true;
        session++;
        status = "";
        try { native.Open(follower, SendMessage, OnNativeClosed); }
        catch
        {
            CloseTalk();
            throw;
        }
    }

    public void OpenSettings()
    {
        if (!settingsOpen) previousTimeScale = Time.timeScale;
        settingsOpen = true;
        native.SetSettingsVisible(true);
        selectedProvider = plugin.Provider.Value;
        stagedKeys[Plugin.OpenAi] = plugin.Keys.Get(Plugin.OpenAi);
        stagedKeys[Plugin.Gemini] = plugin.Keys.Get(Plugin.Gemini);
        stagedModels[Plugin.OpenAi] = plugin.OpenAiModel.Value;
        stagedModels[Plugin.Gemini] = plugin.GeminiModel.Value;
        editedKey = stagedKeys[selectedProvider];
        editedModel = stagedModels[selectedProvider];
        revealKey = false;
        status = "";
        pendingFocus = true;
        if (!talkOpen) Time.timeScale = 0f;
    }

    public void HandleUpdate()
    {
        if (broadcastOpen &&
            Lamb.UI.UIManager.GetActiveMenu<Lamb.UI.AltarMenu.UIAltarMenuController>() == null)
            broadcastOpen = false;
        if (Input.GetKeyDown(KeyCode.F8))
        {
            if (settingsOpen) CloseSettings();
            else OpenSettings();
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (broadcastOpen)
            {
                NativeDialogue.SuppressPauseForEscape();
                broadcastOpen = false;
            }
            else if (settingsOpen) CloseSettings();
            else if (talkOpen)
            {
                NativeDialogue.SuppressPauseForEscape();
                CloseTalk();
            }
        }
        native.HandleUpdate();
    }

    private void CloseTalk()
    {
        talkOpen = false;
        Follower? follower = selectedFollower;
        interaction_FollowerInteraction? interaction = heldInteraction;
        string? action = pendingAction;
        selectedFollower = null;
        heldInteraction = null;
        pendingAction = null;
        session++;
        try { native.Close(); }
        finally
        {
            if (interaction != null)
            {
                try { TalkCommand.ReleaseFollower(interaction); }
                catch (Exception error) { plugin.LogReleaseError(error); }
                finally
                {
                    try
                    {
                        if (follower != null && follower.Brain != null &&
                            (follower.Brain.CurrentTaskType == FollowerTaskType.ManualControl ||
                             follower.Brain.CurrentTaskType == FollowerTaskType.GetPlayerAttention))
                            follower.Brain.CompleteCurrentTask();
                    }
                    finally { SimulationManager.UnPause(); }
                }
            }
        }
        if (follower != null && action != null)
            plugin.LogAction(action, true, FollowerActions.TryExecute(follower, action, interaction));
    }

    private void CloseSettings()
    {
        settingsOpen = false;
        revealKey = false;
        editedKey = "";
        stagedKeys.Clear();
        native.SetSettingsVisible(false);
        native.RefreshInputLanguage();
        if (!talkOpen) Time.timeScale = previousTimeScale;
    }

    private void OpenBroadcast()
    {
        broadcastText = "";
        broadcastStatus = "";
        broadcastOpen = true;
        broadcastFocus = true;
    }

    private void SaveBroadcast()
    {
        string message = broadcastText.Trim();
        if (message.Length == 0) return;
        if (!plugin.Memory.Announce(message))
        {
            broadcastStatus = UiText.Get(plugin.UiLanguage, "broadcast.error");
            return;
        }
        plugin.LogAnnouncement(message);
        broadcastOpen = false;
    }

    private void SaveSettings()
    {
        stagedKeys[selectedProvider] = editedKey.Trim();
        stagedModels[selectedProvider] = editedModel.Trim();
        if (string.IsNullOrEmpty(stagedModels[Plugin.OpenAi]) ||
            string.IsNullOrEmpty(stagedModels[Plugin.Gemini]))
        {
            status = UiText.Get(SettingsLanguage, "models.error");
            return;
        }
        foreach (string provider in new[] { Plugin.OpenAi, Plugin.Gemini })
        {
            if (!plugin.Keys.Save(provider, stagedKeys[provider], SettingsLanguage,
                out string error))
            {
                status = error;
                return;
            }
        }
        plugin.SetProvider(selectedProvider);
        plugin.OpenAiModel.Value = stagedModels[Plugin.OpenAi];
        plugin.GeminiModel.Value = stagedModels[Plugin.Gemini];
        plugin.RefreshModelSelector();
        plugin.SaveConfiguration();
        status = UiText.Get(plugin.UiLanguage, "saved");
        native.RefreshInputLanguage();
    }

    private void SendMessage(string message)
    {
        if (message.Length == 0 || selectedFollower == null || waiting) return;
        status = "";
        Follower follower = selectedFollower;
        string key = plugin.Keys.Get(plugin.Provider.Value);
        if (string.IsNullOrEmpty(key))
        {
            native.BeginThinking(message);
            native.ShowError(UiText.Get(plugin.UiLanguage, "key.required"));
            return;
        }
        plugin.Memory.AddTurn(follower, true, message);
        List<ConversationTurn> history = plugin.Memory.History(follower);
        int requestSession = session;
        waiting = true;
        native.BeginThinking(message);
        string model = plugin.Provider.Value == Plugin.Gemini
            ? plugin.GeminiModel.Value : plugin.OpenAiModel.Value;
        string language = plugin.ContextLanguage(message);
        ActionCatalog actions = FollowerActions.Inspect(follower, heldInteraction, language);
        string followerState = FollowerContext.Capture(follower, language, observedActivity) + " " +
            plugin.Requests.Context(follower, language) + " " +
            plugin.Memory.Context(follower, language);
        plugin.RunConversation(ConversationApi.Generate(plugin.Provider.Value, model, key,
            language, follower.Brain.Info.Name, followerState, actions,
            new List<ConversationTurn>(history), null,
            decision =>
            {
                if (requestSession != session || !talkOpen) return;
                if (decision.Action == "cancel")
                {
                    waiting = false;
                    pendingAction = follower.Brain.FollowingPlayer ? "stop_follow" : null;
                    plugin.LogAction("cancel", true, false);
                    AddReply(follower, decision);
                    return;
                }
                bool available = actions.Contains(decision.Action);
                if (decision.Action != "none" && !available)
                {
                    pendingAction = null;
                    plugin.LogAction(decision.Action, false, false);
                    string correction = FollowerActions.UnavailableReason(follower,
                        decision.Action, language) + TalkLanguage.Pick(language,
                        " Предыдущий вариант ответа: ", " Previous draft: ") + decision.Reply;
                    plugin.RunConversation(ConversationApi.Generate(plugin.Provider.Value, model, key,
                        language, follower.Brain.Info.Name, followerState, actions,
                        new List<ConversationTurn>(history), correction,
                        revised =>
                        {
                            if (requestSession != session || !talkOpen) return;
                            waiting = false;
                            AddReply(follower, revised);
                        },
                        retryError =>
                        {
                            if (requestSession != session || !talkOpen) return;
                            waiting = false;
                            native.ShowError(retryError);
                        }));
                }
                else
                {
                    waiting = false;
                    if (decision.Action != "none") pendingAction = decision.Action;
                    plugin.LogAction(decision.Action, available, decision.Action != "none");
                    AddReply(follower, decision);
                }
            },
            error =>
            {
                if (requestSession != session || !talkOpen) return;
                waiting = false;
                native.ShowError(error);
            }));
    }

    private void AddReply(Follower follower, ConversationDecision decision)
    {
        if (follower == null || follower.Brain == null) return;
        plugin.Faith.FromConversation(follower, decision.FaithEffect);
        plugin.Requests.ObserveReply(follower, decision);
        string source = plugin.Memory.History(follower).FindLast(turn => turn.Player).Text ?? "";
        string language = plugin.ContextLanguage(source);
        plugin.Memory.Remember(follower, decision.MemoryNote,
            TalkLanguage.Pick(language, "Игрок: ", "Player: ") + source +
            TalkLanguage.Pick(language, " | Последователь: ", " | Follower: ") + decision.Reply);
        plugin.Memory.AddTurn(follower, false, decision.Reply);
        FollowerEmotion.RememberDayMood(follower, decision.Emotion, plugin.Memory);
        native.ShowReply(decision.Reply, pendingAction == "gift" || pendingAction == "pet" ||
            pendingAction == "follow" || pendingAction == "stop_follow" ||
            pendingAction == "bless" ||
            pendingAction == "tithe" || pendingAction == "inspire" || pendingAction == "drink" ||
            (decision.EndConversation && pendingAction != null), decision.Emotion);
    }

    public void ResumeAfterTithe(Follower follower)
    {
        if (follower != null && follower.Brain != null && !talkOpen)
            OpenTalk(follower, null);
    }

    public void ReactToGift(Follower follower, string giftName)
    {
        if (follower == null || follower.Brain == null) return;
        OpenTalk(follower, null);
        string language = plugin.UiLanguage;
        string eventText = TalkLanguage.Pick(language,
            "Я подарил тебе: ", "I gave you: ") + giftName;
        plugin.Memory.AddTurn(follower, true, eventText);
        List<ConversationTurn> history = plugin.Memory.History(follower);
        int requestSession = session;
        waiting = true;
        native.BeginThinking(eventText);
        string model = plugin.Provider.Value == Plugin.Gemini
            ? plugin.GeminiModel.Value : plugin.OpenAiModel.Value;
        plugin.RunConversation(ConversationApi.Generate(plugin.Provider.Value, model,
            plugin.Keys.Get(plugin.Provider.Value), language, follower.Brain.Info.Name,
            FollowerContext.Capture(follower, language, observedActivity) + " " +
                plugin.Requests.Context(follower, language) + " " +
                plugin.Memory.Context(follower, language),
            FollowerActions.Inspect(follower, null, language),
            new List<ConversationTurn>(history), null,
            decision =>
            {
                if (requestSession != session || !talkOpen) return;
                waiting = false;
                plugin.Memory.Remember(follower, decision.MemoryNote,
                    TalkLanguage.Pick(language, "Игрок: ", "Player: ") + eventText +
                    TalkLanguage.Pick(language, " | Последователь: ", " | Follower: ") +
                    decision.Reply);
                plugin.Memory.AddTurn(follower, false, decision.Reply);
                FollowerEmotion.RememberDayMood(follower, decision.Emotion, plugin.Memory);
                native.ShowReply(decision.Reply, false, decision.Emotion);
            },
            error =>
            {
                if (requestSession != session || !talkOpen) return;
                waiting = false;
                native.ShowError(error);
            }, true));
    }

    private void OnNativeClosed()
    {
        if (talkOpen) CloseTalk();
    }

}
