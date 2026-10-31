using System;
using System.Collections.Generic;
using Lamb.UI;
using TMPro;
using UnityEngine;

namespace FollowerTalk;

internal static class NativeSettingsLabels
{
    private static readonly Dictionary<string, string> Titles = new()
    {
        ["Provider"] = "provider",
        ["OpenAI model"] = "model.openai",
        ["Gemini model"] = "model.gemini",
        ["Reply language"] = "reply.language",
        ["Interface language"] = "ui.language",
        ["Settings"] = "settings.open"
    };

    private static readonly Dictionary<string, TextMeshProUGUI> Labels = new();
    private static float nextSearch;

    internal static void Refresh(string language)
    {
        if (Time.unscaledTime >= nextSearch)
        {
            nextSearch = Time.unscaledTime + 1f;
            FindLabels();
        }

        foreach (var pair in Labels)
        {
            TextMeshProUGUI label = pair.Value;
            if (label == null || !label.gameObject.activeInHierarchy) continue;
            string value = pair.Key == "OpenAI model" ? UiText.Get(language, "model") + " - OpenAI" :
                pair.Key == "Gemini model" ? UiText.Get(language, "model") + " - Gemini" :
                UiText.Get(language, Titles[pair.Key]);
            if (label.text != value) label.text = value;
        }
    }

    private static void FindLabels()
    {
        foreach (MMHorizontalSelector selector in Resources.FindObjectsOfTypeAll<MMHorizontalSelector>())
            Remember(selector.transform);
        foreach (MMToggle toggle in Resources.FindObjectsOfTypeAll<MMToggle>())
            Remember(toggle.transform);
    }

    private static void Remember(Transform child)
    {
        for (Transform? node = child; node != null; node = node.parent)
        {
            if (!Titles.ContainsKey(node.name)) continue;
            TextMeshProUGUI label = node.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null) return;
            I2.Loc.Localize localize = label.GetComponent<I2.Loc.Localize>();
            if (localize != null) localize.enabled = false;
            Labels[node.name] = label;
            return;
        }
    }
}
