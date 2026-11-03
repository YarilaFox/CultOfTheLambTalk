using System;
using BepInEx;
using BepInEx.Configuration;
using COTL_API.CustomFollowerCommand;
using COTL_API.CustomSettings;
using COTL_API.CustomSettings.Elements;
using HarmonyLib;
using UnityEngine;

namespace FollowerTalk;

[BepInPlugin("arikm.cotl.followertalk", "Follower Talk", "0.5.2")]
[BepInDependency("io.github.xhayper.COTL_API")]
public sealed class Plugin : BaseUnityPlugin
{
    private Toggle? keyEditorToggle;
    private HorizontalSelector? providerSelector;
    private HorizontalSelector? openAiModelSelector;
    private HorizontalSelector? geminiModelSelector;
    private HorizontalSelector? replyLanguageSelector;
    private HorizontalSelector? uiLanguageSelector;
    private HorizontalSelector? conflictSelector;
    private TalkWindow window = null!;
    private Harmony harmony = null!;
    private string settingsLocale = "";

    internal const string OpenAi = "OpenAI API";
    internal const string Gemini = "Gemini API";
    internal const string OpenCode = "OpenCode API";
    internal const string Compatible = "OpenAI-compatible";
    internal const string Claude = "Claude API";
    internal const string Grok = "Grok API";
    internal const string OpenClaw = "OpenClaw Gateway";
    internal const string Hermes = "Hermes Agent";
    internal const string DeepSeek = "DeepSeek API";
    internal static readonly string[] Providers = { OpenAi, Gemini, Claude, Grok,
        OpenCode, OpenClaw, Hermes, DeepSeek, Compatible };
    internal static Plugin Instance { get; private set; } = null!;
    internal ConfigEntry<string> Provider { get; private set; } = null!;
    internal ConfigEntry<string> OpenAiModel { get; private set; } = null!;
    internal ConfigEntry<string> GeminiModel { get; private set; } = null!;
    internal ConfigEntry<string> OpenCodeModel { get; private set; } = null!;
    internal ConfigEntry<string> CompatibleModel { get; private set; } = null!;
    internal ConfigEntry<string> CompatibleEndpoint { get; private set; } = null!;
    internal ConfigEntry<string> ClaudeModel { get; private set; } = null!;
    internal ConfigEntry<string> GrokModel { get; private set; } = null!;
    internal ConfigEntry<string> OpenClawModel { get; private set; } = null!;
    internal ConfigEntry<string> HermesModel { get; private set; } = null!;
    internal ConfigEntry<string> DeepSeekModel { get; private set; } = null!;
    internal ConfigEntry<string> OpenClawEndpoint { get; private set; } = null!;
    internal ConfigEntry<string> HermesEndpoint { get; private set; } = null!;
    internal ConfigEntry<string> LanguageMode { get; private set; } = null!;
    internal ConfigEntry<string> UiLanguageMode { get; private set; } = null!;
    internal ConfigEntry<string> CustomPrompt { get; private set; } = null!;
    internal ConfigEntry<string> ConflictMode { get; private set; } = null!;
    internal string GameLanguageCode
    {
        get
        {
            try
            {
                string code = I2.Loc.LocalizationManager.CurrentLanguageCode;
                if (!string.IsNullOrEmpty(code)) return code;
            }
            catch (Exception) { }
            return Application.systemLanguage.ToString();
        }
    }
    internal string UiLanguage => UiText.Resolve(UiLanguageMode.Value, GameLanguageCode);
    internal string ContextLanguage(string playerMessage) =>
        TalkLanguage.Context(LanguageMode.Value, playerMessage,
            UiLanguage == TalkLanguage.Russian ? TalkLanguage.Russian : TalkLanguage.English);
    internal KeyStore Keys { get; private set; } = null!;
    internal FollowerRequests Requests { get; private set; } = null!;
    internal FollowerFaith Faith { get; private set; } = null!;
    internal FollowerMemory Memory { get; private set; } = null!;
    private int pendingGiftFollowerId = -1;

    private void Awake()
    {
        Instance = this;
        Provider = Config.Bind("Conversation", "Provider", OpenAi, "Selected conversation API.");
        OpenAiModel = Config.Bind("Conversation", "OpenAIModel", "gpt-5.6-luna", "OpenAI model ID.");
        GeminiModel = Config.Bind("Conversation", "GeminiModel", "gemini-3.8-flash", "Gemini model ID.");
        OpenCodeModel = Config.Bind("Conversation", "OpenCodeModel", "kimi-k3", "OpenCode Console model ID.");
        CompatibleModel = Config.Bind("Conversation", "CompatibleModel", "", "OpenAI-compatible model ID.");
        CompatibleEndpoint = Config.Bind("Conversation", "CompatibleEndpoint", "http://127.0.0.1:11434/v1", "OpenAI-compatible API base URL.");
        ClaudeModel = Config.Bind("Conversation", "ClaudeModel", "claude-sonnet-5-5", "Claude model ID.");
        GrokModel = Config.Bind("Conversation", "GrokModel", "grok-4.7", "Grok model ID.");
        OpenClawModel = Config.Bind("Conversation", "OpenClawModel", "openclaw/default", "OpenClaw agent ID.");
        HermesModel = Config.Bind("Conversation", "HermesModel", "hermes-agent", "Hermes model ID.");
        DeepSeekModel = Config.Bind("Conversation", "DeepSeekModel", "deepseek-flash", "DeepSeek model ID.");
        OpenClawEndpoint = Config.Bind("Conversation", "OpenClawEndpoint", "http://127.0.0.1:18789/v1", "OpenClaw Gateway API base URL.");
        HermesEndpoint = Config.Bind("Conversation", "HermesEndpoint", "http://127.0.0.1:8642/v1", "Hermes Agent API base URL.");
        LanguageMode = Config.Bind("Conversation", "Language", TalkLanguage.Auto,
            "Reply language: auto, ru, or en.");
        UiLanguageMode = Config.Bind("Conversation", "InterfaceLanguage", UiText.Game,
            "Interface language: game or a supported language code.");
        CustomPrompt = Config.Bind("Conversation", "CustomPrompt", "",
            "Optional custom follower instructions. Empty uses the built-in prompt.");
        ConflictMode = Config.Bind("Conversation", "ConflictMode", FollowerConflictRules.Off,
            "Off, feelings only, or allow seriously hurt followers to become dissenters and leave.");
        if (Array.IndexOf(Providers, Provider.Value) < 0) Provider.Value = OpenAi;
        if (!TalkLanguage.IsValid(LanguageMode.Value)) LanguageMode.Value = TalkLanguage.Auto;
        if (!UiText.IsValidMode(UiLanguageMode.Value)) UiLanguageMode.Value = UiText.Game;
        if (!FollowerConflictRules.IsValidMode(ConflictMode.Value))
            ConflictMode.Value = FollowerConflictRules.Off;
        Keys = new KeyStore();
        Requests = new FollowerRequests();
        Faith = new FollowerFaith();
        Memory = new FollowerMemory();
        window = new TalkWindow(this);
        harmony = new Harmony("arikm.cotl.followertalk.dialogue");
        harmony.Patch(AccessTools.Method(typeof(MMTools.MMConversation), "HandleNextClicked"),
            prefix: new HarmonyMethod(typeof(NativeDialogue), nameof(NativeDialogue.AllowAdvance)));
        harmony.Patch(AccessTools.Method(typeof(MMTools.MMConversation), "UpdateText"),
            postfix: new HarmonyMethod(typeof(NativeDialogue), nameof(NativeDialogue.RestoreText)));
        harmony.Patch(AccessTools.Method(typeof(Lamb.UI.UIManager), "ShowPauseMenu"),
            prefix: new HarmonyMethod(typeof(NativeDialogue), nameof(NativeDialogue.AllowPauseMenu)));
        harmony.Patch(AccessTools.Method(typeof(Interaction_BackToWork), "OnInteract"),
            prefix: new HarmonyMethod(typeof(NativeDialogue), nameof(NativeDialogue.AllowBackToWork)));
        harmony.Patch(AccessTools.Method(typeof(interaction_FollowerInteraction), "GiveItemRoutine"),
            postfix: new HarmonyMethod(typeof(Plugin), nameof(WrapGiftRoutine)));
        try
        {
            var faithNotification = AccessTools.DeclaredMethod(typeof(NotificationFaith), "Configure",
                new[] { typeof(string), typeof(float), typeof(FollowerInfo), typeof(bool),
                    typeof(NotificationBase.Flair), typeof(bool), typeof(string[]) });
            if (faithNotification != null)
                harmony.Patch(faithNotification,
                    finalizer: new HarmonyMethod(typeof(Plugin), nameof(RecoverFaithNotification)));
            else Logger.LogWarning("Faith notification patch unavailable; continuing without it.");
        }
        catch (Exception error)
        {
            Logger.LogWarning("Faith notification patch failed; continuing without it: " + error);
        }
        try
        {
            var relationshipNotification = AccessTools.DeclaredMethod(typeof(NotificationRelationship), "Configure",
                new[] { typeof(NotificationCentre.NotificationType), typeof(FollowerInfo),
                    typeof(FollowerInfo), typeof(NotificationFollower.Animation),
                    typeof(NotificationFollower.Animation), typeof(NotificationBase.Flair) });
            if (relationshipNotification != null)
                harmony.Patch(relationshipNotification,
                    prefix: new HarmonyMethod(typeof(Plugin), nameof(GuardRelationshipNotification)));
            else Logger.LogWarning("Relationship notification patch unavailable; continuing without it.");
        }
        catch (Exception error)
        {
            Logger.LogWarning("Relationship notification patch failed; continuing without it: " + error);
        }
        harmony.Patch(AccessTools.Method(typeof(Lamb.UI.AltarMenu.UIAltarMenuController), "Start"),
            postfix: new HarmonyMethod(typeof(AltarAnnouncementMenu), nameof(AltarAnnouncementMenu.Install)));
        harmony.Patch(AccessTools.Method(typeof(Lamb.UI.AltarMenu.UIAltarMenuController), "OnEnable"),
            postfix: new HarmonyMethod(typeof(AltarAnnouncementMenu), nameof(AltarAnnouncementMenu.Refresh)));
        harmony.Patch(AccessTools.Method(typeof(Lamb.UI.PrisonerMenu.UIPrisonerMenuController), "OnShowStarted"),
            postfix: new HarmonyMethod(typeof(PrisonTalkMenu), nameof(PrisonTalkMenu.Install)));
        harmony.Patch(AccessTools.Method(typeof(Lamb.UI.PrisonerMenu.UIPrisonerMenuController), "OnHideCompleted"),
            postfix: new HarmonyMethod(typeof(PrisonTalkMenu), nameof(PrisonTalkMenu.Remove)));

        providerSelector = CustomSettingsManager.AddHorizontalSelector("Conversations", "Provider", Provider.Value,
            Providers, index =>
            {
                if (index >= 0 && index < Providers.Length) Provider.Value = Providers[index];
            });
        openAiModelSelector = CustomSettingsManager.AddHorizontalSelector("Conversations", "OpenAI model",
            OpenAiModel.Value, ModelChoices(OpenAi), index =>
            {
                string[] choices = ModelChoices(OpenAi);
                if (index >= 0 && index < choices.Length) OpenAiModel.Value = choices[index];
            });
        geminiModelSelector = CustomSettingsManager.AddHorizontalSelector("Conversations", "Gemini model",
            GeminiModel.Value, ModelChoices(Gemini), index =>
            {
                string[] choices = ModelChoices(Gemini);
                if (index >= 0 && index < choices.Length) GeminiModel.Value = choices[index];
            });
        replyLanguageSelector = CustomSettingsManager.AddHorizontalSelector("Conversations", "Reply language",
            ReplyLanguageNames()[Array.IndexOf(ReplyLanguageModes, LanguageMode.Value)],
            ReplyLanguageNames(), index =>
            {
                if (index >= 0 && index < ReplyLanguageModes.Length)
                    LanguageMode.Value = ReplyLanguageModes[index];
            });
        uiLanguageSelector = CustomSettingsManager.AddHorizontalSelector("Conversations", "Interface language",
            UiLanguageNames()[Array.IndexOf(UiText.Languages, UiLanguageMode.Value)],
            UiLanguageNames(), index =>
            {
                if (index >= 0 && index < UiText.Languages.Length)
                {
                    UiLanguageMode.Value = UiText.Languages[index];
                    RefreshSettingsLanguage();
                }
            });
        conflictSelector = CustomSettingsManager.AddHorizontalSelector("Conversations", "Follower conflict",
            ConflictModeNames()[Array.IndexOf(FollowerConflictRules.Modes, ConflictMode.Value)],
            ConflictModeNames(), index =>
            {
                if (index >= 0 && index < FollowerConflictRules.Modes.Length)
                    ConflictMode.Value = FollowerConflictRules.Modes[index];
            });
        keyEditorToggle = CustomSettingsManager.AddToggle("Conversations", "Settings", false,
            enabled =>
            {
                if (!enabled) return;
                if (keyEditorToggle != null) keyEditorToggle.Value = false;
                window.OpenSettings();
            });
        RefreshSettingsLanguage();
        CustomFollowerCommandManager.Add(new TalkCommand());
        Logger.LogInfo("Follower conversation command registered.");
    }

    private void Update()
    {
        RefreshSettingsLanguage();
        window.HandleUpdate();
        FollowerEmotion.UpdateDayMoods(Memory);
        Requests.Update();
    }

    private void LateUpdate()
    {
        NativeSettingsLabels.Refresh(UiLanguage);
        AltarAnnouncementMenu.RefreshActive(UiLanguage);
        PrisonTalkMenu.RefreshActive(UiLanguage);
    }

    internal void OpenAnnouncement(Lamb.UI.AltarMenu.UIAltarMenuController menu) =>
        window.OpenAnnouncement(menu);

    internal void OpenPrisonTalk(int followerId,
        Lamb.UI.PrisonerMenu.UIPrisonerMenuController menu) =>
        RunConversation(OpenPrisonTalkAfterMenu(followerId, menu));

    private System.Collections.IEnumerator OpenPrisonTalkAfterMenu(int followerId,
        Lamb.UI.PrisonerMenu.UIPrisonerMenuController menu)
    {
        float deadline = Time.unscaledTime + 4f;
        while (menu != null && Time.unscaledTime < deadline)
            yield return null;
        if (menu != null) yield break;
        GameManager.GetInstance().OnConversationEnd();
        Follower? follower = FollowerManager.FindFollowerByID(followerId);
        if (follower != null && follower.Brain != null)
            window.OpenTalk(follower, null);
    }

    internal void RefreshSettingsLanguage()
    {
        string language = UiLanguage;
        if (settingsLocale == language) return;
        settingsLocale = language;
        string category = UiText.Get(language, "settings.category");
        if (providerSelector != null)
        {
            providerSelector.Category = category;
            providerSelector.Text = UiText.Get(language, "provider");
        }
        if (openAiModelSelector != null)
        {
            openAiModelSelector.Category = category;
            openAiModelSelector.Text = UiText.Get(language, "model") + " - OpenAI";
        }
        if (geminiModelSelector != null)
        {
            geminiModelSelector.Category = category;
            geminiModelSelector.Text = UiText.Get(language, "model") + " - Gemini";
        }
        if (replyLanguageSelector != null)
        {
            replyLanguageSelector.Category = category;
            replyLanguageSelector.Text = UiText.Get(language, "reply.language");
            replyLanguageSelector.Options = ReplyLanguageNames();
            replyLanguageSelector.Value = replyLanguageSelector.Options[
                Array.IndexOf(ReplyLanguageModes, LanguageMode.Value)];
        }
        if (uiLanguageSelector != null)
        {
            uiLanguageSelector.Category = category;
            uiLanguageSelector.Text = UiText.Get(language, "ui.language");
            uiLanguageSelector.Options = UiLanguageNames();
            uiLanguageSelector.Value = uiLanguageSelector.Options[
                Array.IndexOf(UiText.Languages, UiLanguageMode.Value)];
        }
        if (conflictSelector != null)
        {
            conflictSelector.Category = category;
            conflictSelector.Text = UiText.Get(language, "conflict.mode");
            conflictSelector.Options = ConflictModeNames();
            conflictSelector.Value = conflictSelector.Options[
                Array.IndexOf(FollowerConflictRules.Modes, ConflictMode.Value)];
        }
        if (keyEditorToggle != null)
        {
            keyEditorToggle.Category = category;
            keyEditorToggle.Text = UiText.Get(language, "settings.open");
        }
    }

    private static readonly string[] ReplyLanguageModes =
        { TalkLanguage.Auto, TalkLanguage.Russian, TalkLanguage.English };

    private string[] ReplyLanguageNames() =>
        new[] { UiText.Get(UiLanguage, "auto"), "Русский", "English" };

    private string[] ConflictModeNames() => new[]
    {
        UiText.Get(UiLanguage, "conflict.off"),
        UiText.Get(UiLanguage, "conflict.feelings"),
        UiText.Get(UiLanguage, "conflict.leave")
    };

    private string[] UiLanguageNames()
    {
        string[] names = (string[])UiText.LanguageNames.Clone();
        names[0] = UiText.Get(UiLanguage, "game.language");
        return names;
    }

    private string[] ModelChoices(string provider)
    {
        if (provider == Claude) return new[] { ClaudeModel.Value };
        if (provider == Grok) return new[] { GrokModel.Value };
        if (provider == OpenClaw) return new[] { OpenClawModel.Value };
        if (provider == Hermes) return new[] { HermesModel.Value };
        if (provider == DeepSeek) return new[] { DeepSeekModel.Value };
        if (provider == OpenCode) return new[] { OpenCodeModel.Value };
        if (provider == Compatible) return new[] { CompatibleModel.Value };
        string[] presets = provider == OpenAi
            ? new[] { "gpt-5.6-luna", "gpt-6-luna" }
            : new[] { "gemini-3.8-flash", "gemini-3.6-flash" };
        string current = provider == OpenAi ? OpenAiModel.Value : GeminiModel.Value;
        return Array.IndexOf(presets, current) >= 0
            ? presets : new[] { presets[0], presets[1], current };
    }

    internal void RefreshModelSelector()
    {
        if (openAiModelSelector != null)
        {
            openAiModelSelector.Options = ModelChoices(OpenAi);
            openAiModelSelector.Value = OpenAiModel.Value;
        }
        if (geminiModelSelector != null)
        {
            geminiModelSelector.Options = ModelChoices(Gemini);
            geminiModelSelector.Value = GeminiModel.Value;
        }
    }

    private void OnGUI() => window.Draw();

    private void OnDestroy()
    {
        window?.Shutdown();
        harmony?.UnpatchSelf();
    }

    internal void OpenTalk(Follower follower, interaction_FollowerInteraction? heldInteraction,
        FollowerActivity activity) =>
        StartCoroutine(OpenTalkAfterWheel(follower, heldInteraction, activity));

    private System.Collections.IEnumerator OpenTalkAfterWheel(Follower follower,
        interaction_FollowerInteraction? heldInteraction, FollowerActivity activity)
    {
        yield return null;
        if (follower != null && follower.Brain != null)
            window.OpenTalk(follower, heldInteraction, activity);
        else if (heldInteraction != null)
        {
            try { TalkCommand.ReleaseFollower(heldInteraction); }
            catch (System.Exception error) { Logger.LogWarning("Follower release failed: " + error); }
            SimulationManager.UnPause();
        }
    }

    internal void RunConversation(System.Collections.IEnumerator routine) => StartCoroutine(routine);

    internal void ResumeAfterTithe(Follower follower) =>
        StartCoroutine(AfterNativeAction(() => window.ResumeAfterTithe(follower)));

    private System.Collections.IEnumerator AfterNativeAction(System.Action resume)
    {
        yield return new WaitForSecondsRealtime(.25f);
        for (int attempt = 0; attempt < 40; attempt++)
        {
            var wheel = Lamb.UI.UIManager.GetActiveMenu<
                Lamb.UI.FollowerInteractionWheel.UIFollowerInteractionWheelOverlayController>();
            if (wheel != null) wheel.Hide(false);
            if (wheel == null && MMTools.MMConversation.CURRENT_CONVERSATION == null)
            {
                yield return null;
                resume();
                yield break;
            }
            yield return new WaitForSecondsRealtime(.1f);
        }
        Logger.LogWarning("Native interaction remained open; conversation was not overlaid.");
    }

    internal void SaveConfiguration() => Config.Save();

    internal void LogAction(string action, bool available, bool queued) =>
        Logger.LogInfo("Conversation action: " + action +
            "; available=" + available + "; queued=" + queued);

    internal void LogReleaseError(System.Exception error) =>
        Logger.LogWarning("Follower release failed: " + error);

    internal void LogRequestError(System.Exception error) =>
        Logger.LogWarning("Follower request state failed: " + error);

    internal void LogRequestReward(string follower, string request) =>
        Logger.LogInfo("Follower gained loyalty for verified request: " + follower + " " + request);

    internal void LogActionError(string action, System.Exception error) =>
        Logger.LogWarning("Conversation action failed: " + action + " " + error);

    internal void LogMouthSync(int visibleLetters, int starts, bool typewriter) =>
        Logger.LogInfo("Mouth sync: visible_letters=" + visibleLetters +
            "; animation_starts=" + starts + "; typewriter=" + typewriter);

    internal void LogEmotion(string emotion, string face) =>
        Logger.LogInfo("Conversation emotion: " + emotion + " -> " + face);

    internal void LogFaith(string follower, float before, float after) =>
        Logger.LogInfo("Personal faith: " + follower + " " + before + " -> " + after);

    private static Exception? RecoverFaithNotification(Exception? __exception,
        NotificationFaith __instance, string locKey, float faithDelta,
        FollowerInfo followerInfo, bool includeName, NotificationBase.Flair flair,
        bool showAdditionSubtractionSymbols, string[] args)
    {
        if (__exception is not NullReferenceException ||
            __exception.StackTrace?.Contains("SpineExtensions.ConfigureFollowerSkin") != true ||
            followerInfo == null)
            return __exception;
        try
        {
            __instance.Configure(locKey, faithDelta, null, includeName, flair,
                showAdditionSubtractionSymbols, args);
            Instance.Logger.LogWarning("Faith notification portrait unavailable; displayed without portrait.");
            return null;
        }
        catch (Exception fallbackError)
        {
            Instance.Logger.LogWarning("Faith notification fallback failed: " + fallbackError);
            return __exception;
        }
    }

    private static bool GuardRelationshipNotification(ref FollowerInfo followerInfoA,
        ref FollowerInfo followerInfoB)
    {
        try
        {
            WorshipperData data = WorshipperData.Instance;
            if (data == null) return true;
            FollowerInfo? first = NotificationSkinCopy(followerInfoA, data);
            FollowerInfo? second = NotificationSkinCopy(followerInfoB, data);
            if (first == null || second == null)
            {
                Instance.Logger.LogWarning("Relationship notification skipped: no valid portrait available.");
                return false;
            }
            followerInfoA = first;
            followerInfoB = second;
        }
        catch (Exception error)
        {
            Instance.Logger.LogWarning("Relationship notification portrait check failed: " + error);
            return false;
        }
        return true;
    }

    private static FollowerInfo? NotificationSkinCopy(FollowerInfo info, WorshipperData data)
    {
        if (info == null) return null;
        var skin = !string.IsNullOrEmpty(info.SkinName) ? data.GetColourData(info.SkinName) : null;
        if (skin != null && info.Traits != null && info.SkinVariation >= 0 && info.SkinColour >= 0 &&
            skin.Skin != null && skin.Skin.Count > 0 &&
            skin.SlotAndColours != null && skin.SlotAndColours.Count > 0 &&
            !string.IsNullOrEmpty(skin.Skin[Math.Min(info.SkinVariation, skin.Skin.Count - 1)]?.Skin) &&
            skin.SlotAndColours[Math.Min(info.SkinColour, skin.SlotAndColours.Count - 1)] != null)
            return info;

        foreach (var fallback in data.Characters)
        {
            if (fallback?.Skin == null || fallback.Skin.Count == 0 ||
                string.IsNullOrEmpty(fallback.Skin[0]?.Skin) ||
                fallback.SlotAndColours == null || fallback.SlotAndColours.Count == 0 ||
                fallback.SlotAndColours[0] == null)
                continue;

            // The original FollowerInfo belongs to the save; only the notification gets this skin.
            var copy = (FollowerInfo)AccessTools.Method(typeof(object), "MemberwiseClone")
                .Invoke(info, null);
            copy.SkinName = fallback.Skin[0].Skin;
            copy.SkinVariation = 0;
            copy.SkinColour = 0;
            copy.Traits ??= new System.Collections.Generic.List<FollowerTrait.TraitType>();
            Instance.Logger.LogWarning("Relationship notification used a fallback portrait for follower " + info.ID + ".");
            return copy;
        }

        Instance.Logger.LogWarning("Relationship notification has no valid fallback portrait for follower " + info.ID + ".");
        return null;
    }

    internal void LogGiftSelection(string selection) =>
        Logger.LogInfo("Gift menu selection: " + selection);

    internal void LogMemoryError(System.Exception error) =>
        Logger.LogWarning("Follower memory failed: " + error);

    internal void LogApiFailure(string provider, string model, long status,
        string detail, string transportError) =>
        Logger.LogWarning("Conversation API failed: provider=" + provider +
            "; model=" + model + "; status=" + status +
            "; detail=" + detail + "; transport=" + transportError);

    internal void LogResponseParseFailure(string provider, string model, string reason,
        string finishReason, int envelopeLength, int textLength) =>
        Logger.LogWarning("Conversation response unreadable: provider=" + provider +
            "; model=" + model + "; reason=" + reason +
            "; finish=" + finishReason + "; envelope_chars=" + envelopeLength +
            "; text_chars=" + textLength);

    internal void LogAnnouncement(string message) =>
        Logger.LogInfo("Cult announcement saved (" + message.Length + " characters).");

    internal void TrackGift(Follower follower) => pendingGiftFollowerId = follower.Brain.Info.ID;

    internal void ClearGift(Follower follower)
    {
        if (pendingGiftFollowerId == follower.Brain.Info.ID) pendingGiftFollowerId = -1;
    }

    private static void WrapGiftRoutine(interaction_FollowerInteraction __instance,
        InventoryItem.ITEM_TYPE itemToGive, ref System.Collections.IEnumerator __result)
    {
        Follower follower = __instance.follower;
        if (follower == null || Instance.pendingGiftFollowerId != follower.Brain.Info.ID)
            return;
        int before = Inventory.GetItemQuantity(itemToGive);
        __result = Instance.FinishGift(__result, follower, itemToGive, before);
    }

    private System.Collections.IEnumerator FinishGift(System.Collections.IEnumerator routine,
        Follower follower, InventoryItem.ITEM_TYPE item, int before)
    {
        yield return routine;
        ClearGift(follower);
        if (follower == null || follower.Brain == null ||
            Inventory.GetItemQuantity(item) >= before) yield break;
        Faith.Change(follower, 3);
        string giftName = InventoryItem.LocalizedName(item);
        yield return AfterNativeAction(() => window.ReactToGift(follower, giftName));
    }

    internal void SetProvider(string value)
    {
        Provider.Value = value;
        if (providerSelector != null) providerSelector.Value = value;
    }
}
