using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Lamb.UI;
using Lamb.UI.AltarMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FollowerTalk;

internal static class AltarAnnouncementMenu
{
    private const string ButtonName = "FollowerTalk_Announcement";
    private static readonly FieldInfo? DescriptionField = AccessTools.Field(
        typeof(UIAltarMenuController), "_description");
    private static readonly string[] ButtonFields =
    {
        "_sermonButton", "_playerUpgradesButton", "_ritualsButton",
        "_doctrineButton", "_cultUpgradeButton"
    };
    private static readonly Dictionary<UIAltarMenuController, MMButton> Buttons = new();
    private static Sprite? icon;

    public static void Install(UIAltarMenuController __instance)
    {
        if (__instance == null || Buttons.ContainsKey(__instance)) return;
        var originals = ButtonFields.Select(name => AccessTools.Field(
            typeof(UIAltarMenuController), name)?.GetValue(__instance) as MMButton)
            .Where(button => button != null).Cast<MMButton>().ToList();
        if (originals.Count != ButtonFields.Length) return;

        MMButton source = originals[4];
        GameObject clone = Object.Instantiate(source.gameObject, source.transform.parent);
        clone.name = ButtonName;
        clone.transform.SetAsLastSibling();
        MMButton button = clone.GetComponent<MMButton>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => Plugin.Instance.OpenAnnouncement(__instance));
        button.OnSelected = () =>
        {
            if (DescriptionField?.GetValue(__instance) is TextMeshProUGUI description)
                description.text = UiText.Get(Plugin.Instance.UiLanguage, "altar.title");
        };
        foreach (I2.Loc.Localize localize in clone.GetComponentsInChildren<I2.Loc.Localize>(true))
            localize.enabled = false;
        SetIcon(clone);
        Reflow(originals, button);
        Buttons[__instance] = button;
        SetLabel(button, Plugin.Instance.UiLanguage);
    }

    public static void Refresh(UIAltarMenuController __instance)
    {
        if (__instance != null && Buttons.TryGetValue(__instance, out MMButton? button))
            SetLabel(button, Plugin.Instance.UiLanguage);
    }

    internal static void RefreshActive(string language)
    {
        UIAltarMenuController? menu = UIManager.GetActiveMenu<UIAltarMenuController>();
        if (menu != null && Buttons.TryGetValue(menu, out MMButton? button))
            SetLabel(button, language);
    }

    private static void SetLabel(MMButton button, string language)
    {
        if (button == null) return;
        string label = UiText.Get(language, "altar.button");
        foreach (TextMeshProUGUI text in button.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (text.text != label) text.text = label;
    }

    private static void Reflow(List<MMButton> originals, MMButton added)
    {
        if (originals[0].transform.parent.GetComponent<LayoutGroup>() != null) return;
        var rects = originals.Select(item => item.transform as RectTransform).ToList();
        if (rects.Any(item => item == null) || added.transform is not RectTransform last) return;
        float first = rects.Min(item => item!.anchoredPosition.x);
        float end = rects.Max(item => item!.anchoredPosition.x);
        var ordered = rects.OrderBy(item => item!.anchoredPosition.x).ToList();
        ordered.Add(last);
        for (int index = 0; index < ordered.Count; index++)
        {
            RectTransform rect = ordered[index]!;
            Vector2 position = rect.anchoredPosition;
            position.x = Mathf.Lerp(first, end, index / 5f);
            rect.anchoredPosition = position;
            rect.localScale = rect.localScale * .84f;
        }
    }

    private static void SetIcon(GameObject clone)
    {
        Image? image = clone.GetComponentsInChildren<Image>(true)
            .FirstOrDefault(item => item.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) >= 0);
        if (image == null) return;
        icon ??= DrawIcon();
        image.sprite = icon;
    }

    private static Sprite DrawIcon()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
        var pixels = new Color[size * size];
        Color ink = new Color(1f, .94f, .77f);
        void Dot(int x, int y, int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
                if (dx * dx + dy * dy <= radius * radius && x + dx >= 0 &&
                    x + dx < size && y + dy >= 0 && y + dy < size)
                    pixels[(y + dy) * size + x + dx] = ink;
        }
        void Line(int x1, int y1, int x2, int y2, int width = 2)
        {
            int steps = Mathf.Max(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1));
            for (int step = 0; step <= steps; step++)
            {
                float t = steps == 0 ? 0 : step / (float)steps;
                Dot(Mathf.RoundToInt(Mathf.Lerp(x1, x2, t)),
                    Mathf.RoundToInt(Mathf.Lerp(y1, y2, t)), width);
            }
        }
        Line(8, 35, 26, 35); Line(26, 35, 35, 44); Line(35, 44, 35, 19);
        Line(35, 19, 26, 28); Line(26, 28, 8, 28); Line(8, 28, 8, 35);
        Line(13, 28, 16, 15); Line(16, 15, 22, 15);
        Line(42, 25, 49, 20); Line(43, 32, 54, 32); Line(42, 39, 49, 44);
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(.5f, .5f), size);
    }
}
