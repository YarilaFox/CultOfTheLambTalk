using System;
using System.Collections.Generic;
using UnityEngine;

namespace FollowerTalk;

internal sealed partial class TalkWindow
{
    private Texture2D? dimTexture;
    private GUIStyle? panelStyle, fieldStyle, editorStyle, buttonStyle, tabStyle,
        selectedStyle, titleStyle, labelStyle, hintStyle;
    private Vector2 promptScroll, followerScroll;
    private int selectedPromptFollowerId;

    private string Ui(string key) => UiText.Get(SettingsLanguage, key);
    private string PromptText(string ru, string en) => SettingsLanguage == "ru" ? ru : en;

    public void Draw()
    {
        if (!settingsOpen) return;
        EnsureStyles();
        int depth = GUI.depth;
        GUI.depth = -1000;
        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        Matrix4x4 matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
            new Vector3(scale, scale, 1f));
        float width = Screen.width / scale, height = Screen.height / scale;
        GUI.DrawTexture(new Rect(0, 0, width, height), dimTexture!);
        DrawSettings(width, height);
        GUI.matrix = matrix;
        GUI.depth = depth;
    }

    private void DrawSettings(float width, float height)
    {
        Rect panel = new Rect((width - 900) / 2, (height - 680) / 2, 900, 680);
        GUI.Box(panel, "", panelStyle!);
        GUI.BeginGroup(panel);
        GUI.Label(new Rect(32, 20, 720, 42), Ui("settings.title"), titleStyle!);
        if (GUI.Button(new Rect(832, 20, 38, 38), "×", buttonStyle!)) CloseSettings();
        string[] tabs = {
            PromptText("Подключение", "Connection"),
            PromptText("Общий промпт", "Shared prompt"),
            PromptText("Последователи", "Followers")
        };
        for (int i = 0; i < tabs.Length; i++)
            if (GUI.Button(new Rect(32 + i * 280, 80, 270, 42), tabs[i],
                i == settingsTab ? selectedStyle! : tabStyle!))
            {
                settingsTab = i;
                promptScroll = Vector2.zero;
                GUI.FocusControl("");
            }
        if (settingsTab == 0) DrawConnection();
        else if (settingsTab == 1) DrawSharedPrompt();
        else DrawFollowerPrompt();
        if (GUI.Button(new Rect(32, 612, 160, 42), Ui("save"), buttonStyle!)) SaveSettings();
        GUI.Label(new Rect(210, 614, 660, 38), status, hintStyle!);
        GUI.EndGroup();
    }

    private void DrawConnection()
    {
        GUI.Label(new Rect(32, 146, 240, 30), Ui("provider"), labelStyle!);
        int providerIndex = Array.IndexOf(Plugin.Providers, selectedProvider);
        if (GUI.Button(new Rect(390, 140, 48, 42), "<", buttonStyle!))
            SelectProvider(Plugin.Providers[(providerIndex + Plugin.Providers.Length - 1) %
                Plugin.Providers.Length]);
        GUI.Label(new Rect(452, 146, 340, 30), selectedProvider, labelStyle!);
        if (GUI.Button(new Rect(820, 140, 48, 42), ">", buttonStyle!))
            SelectProvider(Plugin.Providers[(providerIndex + 1) % Plugin.Providers.Length]);
        GUI.Label(new Rect(32, 205, 830, 28), Ui("api.key"), labelStyle!);
        GUI.SetNextControlName("api-key");
        editedKey = revealKey
            ? GUI.TextField(new Rect(32, 242, 744, 44), editedKey, 512, fieldStyle!)
            : GUI.PasswordField(new Rect(32, 242, 744, 44), editedKey, '•', 512, fieldStyle!);
        if (pendingFocus && Event.current.type == EventType.Repaint)
        {
            GUI.FocusControl("api-key");
            pendingFocus = false;
        }
        if (GUI.Button(new Rect(788, 242, 80, 44),
            revealKey ? Ui("key.hide") : Ui("key.show"), buttonStyle!)) revealKey = !revealKey;
        GUI.Label(new Rect(32, 318, 830, 28), Ui("model"), labelStyle!);
        editedModel = GUI.TextField(new Rect(32, 354, 836, 44), editedModel, 100,
            fieldStyle!);
        if (NeedsEndpoint(selectedProvider))
        {
            GUI.Label(new Rect(32, 415, 830, 28), "API URL", labelStyle!);
            editedEndpoint = GUI.TextField(new Rect(32, 450, 836, 44), editedEndpoint,
                512, fieldStyle!);
        }
        GUI.Label(new Rect(32, 515, 830, 54), Ui("model.hint"), hintStyle!);
    }

    private void DrawSharedPrompt()
    {
        GUI.Label(new Rect(32, 142, 830, 28),
            PromptText("Инструкции для всех последователей", "Instructions for all followers"),
            labelStyle!);
        GUI.Label(new Rect(32, 174, 830, 38),
            PromptText("Изменение заменит стандартный текст. Игровые действия проверяются отдельно.",
                "Changes replace the default text. Game actions are checked separately."), hintStyle!);
        DrawEditor(new Rect(32, 220, 836, 340), ref editedPrompt);
        if (GUI.Button(new Rect(32, 568, 200, 34),
            PromptText("Вернуть стандартный", "Restore default"), buttonStyle!))
        {
            editedPrompt = ConversationApi.DefaultInstructions(plugin.ContextLanguage(""));
            promptScroll = Vector2.zero;
            GUI.FocusControl("");
        }
    }

    private void DrawFollowerPrompt()
    {
        GUI.Label(new Rect(32, 142, 830, 28),
            PromptText("Личное дополнение", "Personal instructions"), labelStyle!);
        GUI.Label(new Rect(32, 172, 830, 40),
            PromptText("Выбери последователя. Переименование не сотрёт его настройки.",
                "Select a follower. Renaming will not erase their settings."), hintStyle!);
        var ids = new List<int>(plugin.Memory.KnownFollowerIds());
        if (selectedFollower != null && !ids.Contains(selectedFollower.Brain.Info.ID))
            ids.Insert(0, selectedFollower.Brain.Info.ID);
        followerScroll = GUI.BeginScrollView(new Rect(32, 220, 222, 340), followerScroll,
            new Rect(0, 0, 200, Mathf.Max(340, ids.Count * 44)));
        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];
            FollowerBrain brain = FollowerBrain.FindBrainByID(id);
            string name = brain?.Info.Name ?? "#" + id;
            if (GUI.Button(new Rect(0, i * 44, 194, 40), name,
                selectedPromptFollowerId == id ? selectedStyle! : tabStyle!))
            {
                selectedPromptFollowerId = id;
                promptScroll = Vector2.zero;
                GUI.FocusControl("");
            }
        }
        GUI.EndScrollView();
        if (selectedPromptFollowerId == 0)
        {
            GUI.Label(new Rect(276, 238, 580, 90),
                PromptText("Сначала поговори с последователем, затем открой настройки.",
                    "Talk to a follower first, then open settings."), hintStyle!);
            return;
        }
        int followerId = selectedPromptFollowerId;
        string nameLabel = FollowerBrain.FindBrainByID(followerId)?.Info.Name ?? "#" + followerId;
        GUI.Label(new Rect(276, 220, 580, 28), nameLabel, labelStyle!);
        string draft = stagedPersonalPrompts.TryGetValue(followerId, out string? value)
            ? value : plugin.Memory.PersonalPrompt(followerId);
        DrawEditor(new Rect(276, 254, 592, 306), ref draft);
        stagedPersonalPrompts[followerId] = draft;
        if (GUI.Button(new Rect(276, 568, 160, 34),
            PromptText("Очистить", "Clear"), buttonStyle!))
            stagedPersonalPrompts[followerId] = "";
    }

    private void DrawEditor(Rect rect, ref string value)
    {
        float contentHeight = Mathf.Max(rect.height - 8,
            editorStyle!.CalcHeight(new GUIContent(value + "\n"), rect.width - 44) + 22);
        promptScroll = GUI.BeginScrollView(rect, promptScroll,
            new Rect(0, 0, rect.width - 18, contentHeight));
        value = GUI.TextArea(new Rect(0, 0, rect.width - 28, contentHeight), value,
            12000, editorStyle!);
        GUI.EndScrollView();
    }

    private void EnsureStyles()
    {
        if (panelStyle != null) return;
        dimTexture = SolidTexture(new Color(0, 0, 0, .82f));
        Texture2D panel = SolidTexture(new Color(.065f, .067f, .07f));
        Texture2D field = SolidTexture(new Color(.12f, .125f, .13f));
        Texture2D button = SolidTexture(new Color(.17f, .175f, .18f));
        Texture2D hover = SolidTexture(new Color(.24f, .245f, .25f));
        Texture2D selected = SolidTexture(new Color(.31f, .12f, .11f));
        Color text = new Color(.94f, .92f, .85f);
        panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panel } };
        fieldStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 21, padding = new RectOffset(12, 12, 8, 8),
            normal = { background = field, textColor = text },
            focused = { background = field, textColor = Color.white }
        };
        editorStyle = new GUIStyle(GUI.skin.textArea)
        {
            fontSize = 18, wordWrap = true, padding = new RectOffset(14, 14, 12, 12),
            normal = { background = field, textColor = text },
            focused = { background = field, textColor = Color.white }
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 19, alignment = TextAnchor.MiddleCenter,
            normal = { background = button, textColor = text },
            hover = { background = hover, textColor = Color.white },
            active = { background = hover, textColor = Color.white }
        };
        tabStyle = new GUIStyle(buttonStyle) { fontSize = 20 };
        selectedStyle = new GUIStyle(tabStyle)
        {
            normal = { background = selected, textColor = Color.white },
            hover = { background = selected, textColor = Color.white }
        };
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 29, fontStyle = FontStyle.Bold, normal = { textColor = text }
        };
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20, normal = { textColor = text }
        };
        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17, wordWrap = true,
            normal = { textColor = new Color(.67f, .68f, .68f) }
        };
    }

    private static Texture2D SolidTexture(Color color)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
