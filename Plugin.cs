using BepInEx;
using BepInEx.Configuration;
using COTL_API.CustomFollowerCommand;
using COTL_API.CustomSettings;
using COTL_API.CustomSettings.Elements;
using HarmonyLib;
using UnityEngine;

namespace FollowerTalk;

[BepInPlugin("arikm.cotl.followertalk", "Follower Talk", "0.4.1")]
[BepInDependency("io.github.xhayper.COTL_API")]
public sealed class Plugin : BaseUnityPlugin
{
    private Toggle? keyEditorToggle;
    private HorizontalSelector? providerSelector;
    private TalkWindow window = null!;
    private Harmony harmony = null!;

    internal const string OpenAi = "OpenAI API";
    internal const string Gemini = "Gemini API";
    internal static Plugin Instance { get; private set; } = null!;
    internal ConfigEntry<string> Provider { get; private set; } = null!;
    internal ConfigEntry<string> OpenAiModel { get; private set; } = null!;
    internal ConfigEntry<string> GeminiModel { get; private set; } = null!;
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
        if (Provider.Value != OpenAi && Provider.Value != Gemini) Provider.Value = OpenAi;
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

        providerSelector = CustomSettingsManager.AddHorizontalSelector("Разговоры", "Провайдер", Provider.Value,
            new[] { OpenAi, Gemini }, index => Provider.Value = index == 1 ? Gemini : OpenAi);
        keyEditorToggle = CustomSettingsManager.AddToggle("Разговоры", "Настроить ключ и модель", false,
            enabled =>
            {
                if (!enabled) return;
                if (keyEditorToggle != null) keyEditorToggle.Value = false;
                window.OpenSettings();
            });
        CustomFollowerCommandManager.Add(new TalkCommand());
        Logger.LogInfo("Follower conversation command registered.");
    }

    private void Update()
    {
        window.HandleUpdate();
        Requests.Update();
    }

    private void OnGUI() => window.Draw();

    private void OnDestroy() => harmony?.UnpatchSelf();

    internal void OpenTalk(Follower follower, interaction_FollowerInteraction? heldInteraction) =>
        StartCoroutine(OpenTalkAfterWheel(follower, heldInteraction));

    private System.Collections.IEnumerator OpenTalkAfterWheel(Follower follower,
        interaction_FollowerInteraction? heldInteraction)
    {
        yield return null;
        if (follower != null && follower.Brain != null)
            window.OpenTalk(follower, heldInteraction);
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

    internal void LogFaith(string follower, float before, float after) =>
        Logger.LogInfo("Personal faith: " + follower + " " + before + " -> " + after);

    internal void LogGiftSelection(string selection) =>
        Logger.LogInfo("Gift menu selection: " + selection);

    internal void LogMemoryError(System.Exception error) =>
        Logger.LogWarning("Follower memory failed: " + error);

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
