using UnityEngine;

namespace FollowerTalk;

internal sealed partial class TalkWindow
{
    private Texture2D? dimTexture;
    private Texture2D? panelTexture;
    private Texture2D? eyeTexture;
    private Texture2D? hiddenEyeTexture;
    private GUIStyle? panelStyle;
    private GUIStyle? titleStyle;
    private GUIStyle? bodyStyle;
    private GUIStyle? fieldStyle;
    private GUIStyle? broadcastButtonStyle;

    public void Draw()
    {
        bool atAltar = !settingsOpen &&
            Lamb.UI.UIManager.GetActiveMenu<Lamb.UI.AltarMenu.UIAltarMenuController>() != null;
        if (!settingsOpen && !broadcastOpen && !atAltar) return;
        EnsureStyles();
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
            "Сказать всем", broadcastButtonStyle!)) OpenBroadcast();
        GUI.matrix = oldMatrix;
        GUI.depth = oldDepth;
    }

    private void DrawBroadcast(float width, float height)
    {
        GUI.DrawTexture(new Rect(0, 0, width, height), dimTexture!);
        Rect panel = new Rect((width - 740f) / 2f, (height - 230f) / 2f, 740f, 230f);
        GUI.Box(panel, "", panelStyle!);
        GUI.BeginGroup(panel);
        GUI.Label(new Rect(28, 20, 560, 42), "Сказать всему культу", titleStyle!);
        if (GUI.Button(new Rect(630, 22, 80, 34), "Закрыть")) broadcastOpen = false;
        GUI.SetNextControlName("cult-message");
        broadcastText = GUI.TextField(new Rect(28, 81, 684, 46), broadcastText, 320,
            fieldStyle!);
        if (broadcastFocus && Event.current.type == EventType.Repaint)
        {
            GUI.FocusControl("cult-message");
            broadcastFocus = false;
        }
        if (GUI.Button(new Rect(542, 156, 170, 44), "Сказать", broadcastButtonStyle!))
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
        Rect panel = new Rect((width - 740f) / 2f, (height - 555f) / 2f, 740f, 555f);
        GUI.Box(panel, "", panelStyle!);
        GUI.BeginGroup(panel);
        GUI.Label(new Rect(28, 22, 565, 40), "Разговоры: подключение", titleStyle!);
        if (GUI.Button(new Rect(624, 22, 88, 34), "Закрыть")) CloseSettings();

        GUI.Label(new Rect(28, 80, 680, 26), "Провайдер", bodyStyle!);
        if (GUI.Button(new Rect(28, 111, 330, 42), Plugin.OpenAi)) SelectProvider(Plugin.OpenAi);
        if (GUI.Button(new Rect(366, 111, 346, 42), Plugin.Gemini)) SelectProvider(Plugin.Gemini);
        GUI.Label(new Rect(28, 159, 680, 27), "Выбрано: " + selectedProvider, bodyStyle!);

        GUI.Label(new Rect(28, 204, 680, 26), "Ключ API", bodyStyle!);
        GUI.SetNextControlName("api-key");
        editedKey = revealKey
            ? GUI.TextField(new Rect(28, 236, 616, 42), editedKey, 512, fieldStyle!)
            : GUI.PasswordField(new Rect(28, 236, 616, 42), editedKey, '•', 512, fieldStyle!);
        if (pendingFocus && Event.current.type == EventType.Repaint)
        {
            GUI.FocusControl("api-key");
            pendingFocus = false;
        }
        if (GUI.Button(new Rect(652, 236, 60, 42), new GUIContent("", revealKey ? "Скрыть ключ" : "Показать ключ")))
            revealKey = !revealKey;
        GUI.DrawTexture(new Rect(669, 245, 26, 26), revealKey ? hiddenEyeTexture! : eyeTexture!);
        if (!string.IsNullOrEmpty(GUI.tooltip))
            GUI.Label(new Rect(515, 283, 200, 24), GUI.tooltip, bodyStyle!);

        GUI.Label(new Rect(28, 311, 680, 26), "Модель", bodyStyle!);
        string[] presets = selectedProvider == Plugin.OpenAi
            ? new[] { "gpt-5.6-luna", "gpt-6-luna" }
            : new[] { "gemini-3.8-flash", "gemini-3.6-flash" };
        if (GUI.Button(new Rect(28, 343, 330, 35), presets[0])) editedModel = presets[0];
        if (GUI.Button(new Rect(366, 343, 346, 35), presets[1])) editedModel = presets[1];
        editedModel = GUI.TextField(new Rect(28, 390, 684, 42), editedModel, 100, fieldStyle!);
        GUI.Label(new Rect(28, 441, 684, 26), "Можно вписать ID другой модели.", bodyStyle!);

        if (GUI.Button(new Rect(28, 489, 170, 42), "Сохранить")) SaveSettings();
        GUI.Label(new Rect(218, 492, 495, 38), status, bodyStyle!);
        GUI.EndGroup();
    }

    private void EnsureStyles()
    {
        if (panelStyle != null) return;
        dimTexture = SolidTexture(new Color(0f, 0f, 0f, 0.76f));
        panelTexture = SolidTexture(new Color(0.08f, 0.07f, 0.08f, 0.98f));
        eyeTexture = EyeTexture(false);
        hiddenEyeTexture = EyeTexture(true);
        panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } };
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 29,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.93f, 0.72f) }
        };
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            wordWrap = true,
            normal = { textColor = new Color(0.96f, 0.94f, 0.9f) }
        };
        broadcastButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 19,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.93f, 0.72f) }
        };
        fieldStyle = new GUIStyle(GUI.skin.textField) { fontSize = 19, padding = new RectOffset(10, 10, 8, 8) };
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
