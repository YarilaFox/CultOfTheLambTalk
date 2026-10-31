using System;
using UnityEngine;

namespace FollowerTalk;

internal sealed partial class TalkWindow
{
    private Texture2D? dimTexture;
    private Texture2D? panelTexture;
    private Texture2D? buttonTexture;
    private Texture2D? buttonHoverTexture;
    private Texture2D? buttonSelectedTexture;
    private Texture2D? fieldTexture;
    private Texture2D? eyeTexture;
    private Texture2D? hiddenEyeTexture;
    private GUIStyle? panelStyle;
    private GUIStyle? settingsPanelStyle;
    private GUIStyle? settingsFieldStyle;
    private GUIStyle? settingsButtonStyle;
    private GUIStyle? titleStyle;
    private GUIStyle? bodyStyle;
    private GUIStyle? fieldStyle;
    private GUIStyle? broadcastButtonStyle;
    private GUIStyle? choiceStyle;
    private GUIStyle? commandStyle;
    private string Ui(string key) => UiText.Get(
        settingsOpen ? SettingsLanguage : plugin.UiLanguage, key);

    public void Draw()
    {
        bool atAltar = !settingsOpen &&
            Lamb.UI.UIManager.GetActiveMenu<Lamb.UI.AltarMenu.UIAltarMenuController>() != null;
        if (!settingsOpen && !broadcastOpen && !atAltar) return;
        EnsureStyles();
        bool rightToLeft = (settingsOpen ? SettingsLanguage : plugin.UiLanguage) == "ar";
        titleStyle!.alignment = rightToLeft ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        bodyStyle!.alignment = rightToLeft ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
        int oldDepth = GUI.depth;
        GUI.depth = -1000;
        float scale = Mathf.Min(Screen.width / 1600f, Screen.height / 900f);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        float height = Screen.height / scale;
        if (settingsOpen)
        {
            GUI.DrawTexture(new Rect(0, 0, width, height), dimTexture!);
            DrawSettings(width, height);
        }
        else if (broadcastOpen) DrawBroadcast(width, height);
        else if (GUI.Button(new Rect(width - 310, height - 82, 278, 50),
            Ui("altar.button"), broadcastButtonStyle!)) OpenBroadcast();
        GUI.matrix = oldMatrix;
        GUI.depth = oldDepth;
    }

    private void DrawBroadcast(float width, float height)
    {
        GUI.DrawTexture(new Rect(0, 0, width, height), dimTexture!);
        Rect panel = new Rect((width - 740f) / 2f, (height - 230f) / 2f, 740f, 230f);
        GUI.Box(panel, "", panelStyle!);
        GUI.BeginGroup(panel);
        GUI.Label(new Rect(28, 20, 560, 42), Ui("altar.title"), titleStyle!);
        if (GUI.Button(new Rect(612, 22, 100, 34), Ui("close"))) broadcastOpen = false;
        GUI.SetNextControlName("cult-message");
        broadcastText = GUI.TextField(new Rect(28, 81, 684, 46), broadcastText, 320,
            fieldStyle!);
        if (broadcastFocus && Event.current.type == EventType.Repaint)
        {
            GUI.FocusControl("cult-message");
            broadcastFocus = false;
        }
        if (GUI.Button(new Rect(542, 156, 170, 44), Ui("speak"), broadcastButtonStyle!))
            SaveBroadcast();
        if (broadcastStatus.Length > 0)
            GUI.Label(new Rect(28, 158, 495, 44), broadcastStatus, bodyStyle!);
        if (Event.current.type == EventType.KeyDown &&
            (Event.current.keyCode == KeyCode.Return ||
             Event.current.keyCode == KeyCode.KeypadEnter))
        {
            SaveBroadcast();
            Event.current.Use();
        }
        GUI.EndGroup();
    }

    private void DrawSettings(float width, float height)
    {
        Rect panel = new Rect((width - 680f) / 2f, (height - 340f) / 2f, 680f, 340f);
        GUI.Box(panel, "", settingsPanelStyle!);
        GUI.BeginGroup(panel);
        GUI.Label(new Rect(30, 18, 554, 42), Ui("settings.title"), titleStyle!);
        if (GUI.Button(new Rect(614, 20, 40, 40), "×", settingsButtonStyle!)) CloseSettings();
        GUI.Label(new Rect(30, 67, 610, 28), selectedProvider, bodyStyle!);
        GUI.Label(new Rect(30, 104, 610, 28), Ui("api.key"), bodyStyle!);
        GUI.SetNextControlName("api-key");
        editedKey = revealKey
            ? GUI.TextField(new Rect(30, 137, 546, 42), editedKey, 512, settingsFieldStyle!)
            : GUI.PasswordField(new Rect(30, 137, 546, 42), editedKey, '•', 512, settingsFieldStyle!);
        if (pendingFocus && Event.current.type == EventType.Repaint)
        {
            GUI.FocusControl("api-key");
            pendingFocus = false;
        }
        if (GUI.Button(new Rect(586, 137, 68, 42), new GUIContent("", revealKey
            ? Ui("key.hide") : Ui("key.show")), settingsButtonStyle!))
            revealKey = !revealKey;
        GUI.DrawTexture(new Rect(607, 145, 26, 26), revealKey ? hiddenEyeTexture! : eyeTexture!);
        if (!string.IsNullOrEmpty(GUI.tooltip))
            GUI.Label(new Rect(450, 180, 204, 24), GUI.tooltip, bodyStyle!);
        GUI.Label(new Rect(30, 191, 610, 28), Ui("model"), bodyStyle!);
        editedModel = GUI.TextField(new Rect(30, 224, 624, 42), editedModel, 100,
            settingsFieldStyle!);
        if (GUI.Button(new Rect(30, 281, 180, 42), Ui("save"), settingsButtonStyle!)) SaveSettings();
        GUI.Label(new Rect(225, 283, 429, 38), status, bodyStyle!);
        GUI.EndGroup();
    }

    private void EnsureStyles()
    {
        if (panelStyle != null) return;
        dimTexture = SolidTexture(new Color(0f, 0f, 0f, 0.82f));
        panelTexture = InkTexture(64, new Color(.035f, .028f, .027f),
            new Color(.012f, .009f, .009f), 12);
        buttonTexture = InkTexture(48, new Color(.07f, .058f, .05f),
            new Color(.49f, .37f, .28f), 8);
        buttonHoverTexture = InkTexture(48, new Color(.16f, .1f, .075f),
            new Color(.85f, .7f, .49f), 8);
        buttonSelectedTexture = InkTexture(48, new Color(.28f, .045f, .04f),
            new Color(.97f, .68f, .43f), 8);
        fieldTexture = InkTexture(48, new Color(.025f, .022f, .02f),
            new Color(.42f, .31f, .23f), 8);
        eyeTexture = EyeTexture(false);
        hiddenEyeTexture = EyeTexture(true);
        panelStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = panelTexture },
            border = new RectOffset(12, 12, 12, 12)
        };
        settingsPanelStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = SolidTexture(new Color(.045f, .042f, .041f)) },
            border = new RectOffset(2, 2, 2, 2)
        };
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 29,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.88f, 0.57f) }
        };
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            wordWrap = true,
            normal = { textColor = new Color(0.93f, 0.88f, 0.76f) }
        };
        choiceStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            alignment = TextAnchor.MiddleCenter,
            border = new RectOffset(8, 8, 8, 8),
            normal = { background = buttonTexture, textColor = new Color(.93f, .88f, .76f) },
            hover = { background = buttonHoverTexture, textColor = Color.white },
            active = { background = buttonSelectedTexture, textColor = Color.white },
            onNormal = { background = buttonSelectedTexture, textColor = Color.white },
            onHover = { background = buttonSelectedTexture, textColor = Color.white },
            onActive = { background = buttonSelectedTexture, textColor = Color.white }
        };
        commandStyle = new GUIStyle(choiceStyle)
        {
            fontStyle = FontStyle.Bold,
            normal = { background = buttonSelectedTexture, textColor = Color.white }
        };
        broadcastButtonStyle = new GUIStyle(commandStyle) { fontSize = 19 };
        fieldStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = 19,
            padding = new RectOffset(12, 12, 8, 8),
            border = new RectOffset(8, 8, 8, 8),
            normal = { background = fieldTexture, textColor = Color.white },
            focused = { background = fieldTexture, textColor = Color.white },
            hover = { background = fieldTexture, textColor = Color.white }
        };
        settingsFieldStyle = new GUIStyle(fieldStyle)
        {
            normal = { background = SolidTexture(new Color(.15f, .15f, .15f)), textColor = Color.white },
            focused = { background = SolidTexture(new Color(.2f, .2f, .2f)), textColor = Color.white }
        };
        settingsButtonStyle = new GUIStyle(choiceStyle)
        {
            normal = { background = SolidTexture(new Color(.4f, .4f, .4f)), textColor = Color.black },
            hover = { background = SolidTexture(new Color(.55f, .55f, .55f)), textColor = Color.black }
        };
    }

    private static Texture2D InkTexture(int size, Color fill, Color edge, int margin)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int distance = Math.Min(Math.Min(x, y), Math.Min(size - 1 - x, size - 1 - y));
            int rough = ((x * 17 + y * 29 + x * y * 3) % 7) - 3;
            bool visible = distance + rough >= margin / 2;
            Color color = !visible ? Color.clear : distance < margin ? edge : fill;
            texture.SetPixel(x, y, color);
        }
        texture.Apply();
        return texture;
    }

    private static Texture2D SolidTexture(Color color)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private static Texture2D EyeTexture(bool crossedOut)
    {
        const int size = 24;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color paper = new Color(1f, 0.93f, 0.72f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - 11.5f) / 10f;
            float dy = (y - 11.5f) / 5f;
            bool outline = Mathf.Abs(dx * dx + dy * dy - 1f) < .27f;
            bool pupil = (x - 11.5f) * (x - 11.5f) + (y - 11.5f) * (y - 11.5f) < 8f;
            bool slash = crossedOut && Mathf.Abs((x + y) - 23) < 1.5f;
            texture.SetPixel(x, y, outline || pupil || slash ? paper : clear);
        }
        texture.Apply();
        return texture;
    }
}
