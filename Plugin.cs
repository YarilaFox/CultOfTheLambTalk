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
        if (Array.IndexOf(Providers, Provider.Value) < 0) Provider.Value = OpenAi;
        if (!TalkLanguage.IsValid(LanguageMode.Value)) LanguageMode.Value = TalkLanguage.Auto;
        if (!UiText.IsValidMode(UiLanguageMode.Value)) UiLanguageMode.Value = UiText.Game;
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
        harmony.Patch(AccessTools.Method(typeof(Lamb.UI.AltarMenu.UIAltarMenuController), "Start"),
            postfix: new HarmonyMethod(typeof(AltarAnnouncementMenu), nameof(AltarAnnouncementMenu.Install)));
        harmony.Patch(AccessTools.Method(typeof(Lamb.UI.AltarMenu.UIAltarMenuController), "OnEnable"),
            postfix: new HarmonyMethod(typeof(AltarAnnouncementMenu), nameof(AltarAnnouncementMenu.Refresh)));

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
    }

    internal void OpenAnnouncement(Lamb.UI.AltarMenu.UIAltarMenuController menu) =>
        window.OpenAnnouncement(menu);

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

    private void OnDestroy() => harmony?.UnpatchSelf();

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

    internal void LogEmotion(string emotion, string face) =>
        Logger.LogInfo("Conversation emotion: " + emotion + " -> " + face);

    internal void LogFaith(string follower, float before, float after) =>
        Logger.LogInfo("Personal faith: " + follower + " " + before + " -> " + after);

    internal void LogGiftSelection(string selection) =>
        Logger.LogInfo("Gift menu selection: " + selection);

    internal void LogMemoryError(System.Exception error) =>
        Logger.LogWarning("Follower memory failed: " + error);

    internal void LogApiFailure(string provider, string model, long status,
        string detail, string transportError) =>
        Logger.LogWarning("Conversation API failed: provider=" + provider +
            "; model=" + model + "; status=" + status +
            "; detail=" + detail + "; transport=" + transportError);

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
