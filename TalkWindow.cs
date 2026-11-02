using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace FollowerTalk;

internal sealed partial class TalkWindow
{
    private readonly Plugin plugin;
    private readonly NativeDialogue native;
    private readonly Dictionary<string, string> stagedKeys = new();
    private readonly Dictionary<string, string> stagedModels = new();
    private readonly Dictionary<string, string> stagedEndpoints = new();
    private readonly Dictionary<int, string> stagedPersonalPrompts = new();
    private Follower? selectedFollower;
    private FollowerActivity observedActivity;
    private interaction_FollowerInteraction? heldInteraction;
    private string? pendingAction;
    private bool talkOpen;
    private bool settingsOpen;
    private bool announcementOpen;
    private bool announcementInputStarted;
    private float announcementInputTime;
    private Lamb.UI.AltarMenu.UIAltarMenuController? announcementMenu;
    private Coroutine? announcementRoutine;
    private bool revealKey;
    private bool waiting;
    private float previousTimeScale;
    private bool pendingFocus;
    private int session;
    private string status = "";
    private string editedKey = "";
    private string editedModel = "";
    private string editedEndpoint = "";
    private string editedPrompt = "";
    private int settingsTab;
    private string selectedProvider = Plugin.OpenAi;
    private string SettingsLanguage => plugin.UiLanguage;
    private static bool NeedsEndpoint(string provider) =>
        provider == Plugin.Compatible || provider == Plugin.OpenClaw ||
        provider == Plugin.Hermes;

    private string EndpointFor(string provider) => provider == Plugin.OpenClaw
        ? plugin.OpenClawEndpoint.Value : provider == Plugin.Hermes
            ? plugin.HermesEndpoint.Value : plugin.CompatibleEndpoint.Value;

    private string ModelFor(string provider) => provider switch
    {
        Plugin.Gemini => plugin.GeminiModel.Value,
        Plugin.Claude => plugin.ClaudeModel.Value,
        Plugin.Grok => plugin.GrokModel.Value,
        Plugin.OpenCode => plugin.OpenCodeModel.Value,
        Plugin.OpenClaw => plugin.OpenClawModel.Value,
        Plugin.Hermes => plugin.HermesModel.Value,
        Plugin.DeepSeek => plugin.DeepSeekModel.Value,
        Plugin.Compatible => plugin.CompatibleModel.Value,
        _ => plugin.OpenAiModel.Value
    };

    private void SelectProvider(string provider)
    {
        stagedKeys[selectedProvider] = editedKey.Trim();
        stagedModels[selectedProvider] = editedModel.Trim();
        if (NeedsEndpoint(selectedProvider)) stagedEndpoints[selectedProvider] = editedEndpoint;
        selectedProvider = provider;
        editedKey = stagedKeys[provider];
        editedModel = stagedModels[provider];
        editedEndpoint = stagedEndpoints.TryGetValue(provider, out string? endpoint)
            ? endpoint : EndpointFor(provider);
        revealKey = false;
        GUI.FocusControl("");
    }
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
        foreach (string provider in Plugin.Providers)
        {
            stagedKeys[provider] = plugin.Keys.Get(provider);
            stagedModels[provider] = ModelFor(provider);
        }
        stagedEndpoints.Clear();
        foreach (string provider in new[] { Plugin.Compatible, Plugin.OpenClaw, Plugin.Hermes })
            stagedEndpoints[provider] = EndpointFor(provider);
        editedKey = stagedKeys[selectedProvider];
        editedModel = stagedModels[selectedProvider];
        editedEndpoint = EndpointFor(selectedProvider);
        editedPrompt = string.IsNullOrWhiteSpace(plugin.CustomPrompt.Value)
            ? ConversationApi.DefaultInstructions(plugin.ContextLanguage(""))
            : plugin.CustomPrompt.Value;
        selectedPromptFollowerId = selectedFollower?.Brain.Info.ID ?? 0;
        stagedPersonalPrompts.Clear();
        settingsTab = 0;
        promptScroll = Vector2.zero;
        followerScroll = Vector2.zero;
        revealKey = false;
        status = "";
        pendingFocus = true;
        if (!talkOpen) Time.timeScale = 0f;
    }

    public void HandleUpdate()
    {
        if (announcementOpen && announcementInputStarted &&
            Time.unscaledTime - announcementInputTime > 1f && !native.IsVisible)
            CloseAnnouncement(false);
        if (Input.GetKeyDown(KeyCode.F9) && announcementOpen)
        {
            CloseAnnouncement(false);
            return;
        }
        if (Input.GetKeyDown(KeyCode.F8))
        {
            if (settingsOpen) CloseSettings();
            else OpenSettings();
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (announcementOpen)
            {
                NativeDialogue.SuppressPauseForEscape();
                CloseAnnouncement();
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
        stagedEndpoints.Clear();
        stagedPersonalPrompts.Clear();
        selectedPromptFollowerId = 0;
        native.SetSettingsVisible(false);
        native.RefreshInputLanguage();
        if (!talkOpen) Time.timeScale = previousTimeScale;
    }

    public void OpenAnnouncement(Lamb.UI.AltarMenu.UIAltarMenuController menu)
    {
        if (announcementOpen || talkOpen || settingsOpen) return;
        announcementMenu = menu;
        announcementOpen = true;
        announcementInputStarted = false;
        session++;
        menu.Hide(false);
        announcementRoutine = plugin.StartCoroutine(BeginAnnouncementAfterMenu());
    }

    private IEnumerator BeginAnnouncementAfterMenu()
    {
        yield return new WaitForSecondsRealtime(.3f);
        if (!announcementOpen) yield break;
        native.OpenAnnouncement(SubmitAnnouncement, CloseAnnouncement,
            () => CloseAnnouncement(false));
        announcementInputStarted = true;
        announcementInputTime = Time.unscaledTime;
    }

    private void SubmitAnnouncement(string message)
    {
        if (!announcementOpen) return;
        message = message.Trim();
        if (message.Length == 0) return;
        if (!plugin.Memory.Announce(message))
        {
            native.ShowError(UiText.Get(plugin.UiLanguage, "broadcast.error"));
            return;
        }
        plugin.LogAnnouncement(message);
        native.ShowAnnouncement(message);
        int requestSession = session;
        announcementRoutine = plugin.StartCoroutine(GatherForAnnouncement(requestSession));
    }

    private IEnumerator GatherForAnnouncement(int requestSession)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (announcementOpen && Time.realtimeSinceStartup < deadline &&
               MMTools.MMConversation.mmConversation != null &&
               !MMTools.MMConversation.mmConversation.TextPlayer.textAnimator.allLettersShown)
            yield return null;
        if (!announcementOpen || requestSession != session) yield break;

        if (announcementOpen && requestSession == session) native.AnnouncementReady();
    }

    private void CloseAnnouncement() => CloseAnnouncement(true);

    private void CloseAnnouncement(bool restoreMenu)
    {
        if (!announcementOpen) return;
        announcementOpen = false;
        announcementInputStarted = false;
        session++;
        if (announcementRoutine != null) plugin.StopCoroutine(announcementRoutine);
        announcementRoutine = null;
        native.Close();
        Lamb.UI.AltarMenu.UIAltarMenuController? menu = announcementMenu;
        announcementMenu = null;
        if (restoreMenu && menu != null && Interaction_TempleAltar.Instance != null)
            menu.Show(false);
        else SimulationManager.UnPause();
    }

    private void SaveSettings()
    {
        stagedKeys[selectedProvider] = editedKey.Trim();
        stagedModels[selectedProvider] = editedModel.Trim();
        if (NeedsEndpoint(selectedProvider)) stagedEndpoints[selectedProvider] = editedEndpoint.Trim();
        if (string.IsNullOrEmpty(stagedModels[selectedProvider]))
        {
            status = UiText.Get(SettingsLanguage, "models.error");
            return;
        }
        if (NeedsEndpoint(selectedProvider) && !ConversationApi.ValidEndpoint(editedEndpoint))
        {
            status = PromptText("Нужен HTTPS URL или локальный HTTP URL.",
                "Use an HTTPS URL or a local HTTP URL.");
            return;
        }
        foreach (string provider in new[] { Plugin.Compatible, Plugin.OpenClaw, Plugin.Hermes })
            if (!ConversationApi.ValidEndpoint(stagedEndpoints[provider]))
            {
                status = PromptText("Нужен HTTPS URL или локальный HTTP URL.",
                    "Use an HTTPS URL or a local HTTP URL.");
                return;
            }
        foreach (string provider in Plugin.Providers)
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
        plugin.OpenCodeModel.Value = stagedModels[Plugin.OpenCode];
        plugin.CompatibleModel.Value = stagedModels[Plugin.Compatible];
        plugin.ClaudeModel.Value = stagedModels[Plugin.Claude];
        plugin.GrokModel.Value = stagedModels[Plugin.Grok];
        plugin.OpenClawModel.Value = stagedModels[Plugin.OpenClaw];
        plugin.HermesModel.Value = stagedModels[Plugin.Hermes];
        plugin.DeepSeekModel.Value = stagedModels[Plugin.DeepSeek];
        plugin.CompatibleEndpoint.Value = stagedEndpoints[Plugin.Compatible].TrimEnd('/');
        plugin.OpenClawEndpoint.Value = stagedEndpoints[Plugin.OpenClaw].TrimEnd('/');
        plugin.HermesEndpoint.Value = stagedEndpoints[Plugin.Hermes].TrimEnd('/');
        string defaultPrompt = ConversationApi.DefaultInstructions(plugin.ContextLanguage(""));
        plugin.CustomPrompt.Value = editedPrompt == defaultPrompt ? "" : editedPrompt.Trim();
        foreach (KeyValuePair<int, string> entry in stagedPersonalPrompts)
        {
            if (!plugin.Memory.SetPersonalPrompt(entry.Key, entry.Value))
            {
                status = UiText.Get(SettingsLanguage, "broadcast.error");
                return;
            }
        }
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
        if (string.IsNullOrEmpty(key) &&
            (plugin.Provider.Value != Plugin.Compatible ||
             !ConversationApi.LocalEndpoint(plugin.CompatibleEndpoint.Value)))
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
        string model = ModelFor(plugin.Provider.Value);
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
                        }, personalPrompt: plugin.Memory.PersonalPrompt(follower)));
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
            }, personalPrompt: plugin.Memory.PersonalPrompt(follower)));
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
        string model = ModelFor(plugin.Provider.Value);
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
            }, true, plugin.Memory.PersonalPrompt(follower)));
    }

    private void OnNativeClosed()
    {
        if (talkOpen) CloseTalk();
    }

}
