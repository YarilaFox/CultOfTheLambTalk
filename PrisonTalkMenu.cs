using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Lamb.UI.PrisonerMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FollowerTalk;

internal static class PrisonTalkMenu
{
    private static readonly Dictionary<UIPrisonerMenuController, MMButton> Buttons = new();

    public static void Install(UIPrisonerMenuController __instance)
    {
        if (__instance == null || Buttons.ContainsKey(__instance)) return;
        var source = AccessTools.Field(typeof(UIPrisonerMenuController), "_readMindButton")
            ?.GetValue(__instance) as MMButton;
        var follower = AccessTools.Field(typeof(UIPrisonerMenuController), "_followerInfo")
            ?.GetValue(__instance) as FollowerInfo;
        if (source == null || follower == null) return;

        GameObject clone = Object.Instantiate(source.gameObject, source.transform.parent);
        clone.name = "FollowerTalk_PrisonConversation";
        clone.transform.SetAsLastSibling();
        MMButton button = clone.GetComponent<MMButton>();
        button.onClick.RemoveAllListeners();
        int followerId = follower.ID;
        button.onClick.AddListener(() =>
        {
            __instance.Hide();
            Plugin.Instance.OpenPrisonTalk(followerId, __instance);
        });
        foreach (I2.Loc.Localize localize in clone.GetComponentsInChildren<I2.Loc.Localize>(true))
            localize.enabled = false;
        if (source.transform.parent.GetComponent<LayoutGroup>() == null &&
            clone.transform is RectTransform added)
        {
            var existing = new[] { "_readMindButton", "_releaseButton", "_reeducateButton" }
                .Select(name => AccessTools.Field(typeof(UIPrisonerMenuController), name)
                    ?.GetValue(__instance) as MMButton)
                .Where(item => item != null && item.transform is RectTransform)
                .Select(item => (RectTransform)item!.transform).ToArray();
            if (existing.Length == 3)
            {
                float top = existing.Max(item => item.anchoredPosition.y);
                float bottom = existing.Min(item => item.anchoredPosition.y);
                var ordered = existing.OrderByDescending(item => item.anchoredPosition.y).ToArray();
                for (int i = 0; i < ordered.Length; i++)
                    ordered[i].anchoredPosition = new Vector2(ordered[i].anchoredPosition.x,
                        Mathf.Lerp(top, bottom, i / 3f));
                added.anchoredPosition = new Vector2(added.anchoredPosition.x, bottom);
            }
        }
        Buttons[__instance] = button;
        SetLabel(button, Plugin.Instance.UiLanguage);
    }

    public static void RefreshActive(string language)
    {
        UIPrisonerMenuController? menu = Lamb.UI.UIManager.GetActiveMenu<UIPrisonerMenuController>();
        if (menu != null && Buttons.TryGetValue(menu, out MMButton? button))
            SetLabel(button, language);
    }

    public static void Remove(UIPrisonerMenuController __instance) => Buttons.Remove(__instance);

    private static void SetLabel(MMButton button, string language)
    {
        if (button == null) return;
        foreach (TextMeshProUGUI text in button.GetComponentsInChildren<TextMeshProUGUI>(true))
            text.text = UiText.Get(language, "talk.title");
    }
}
