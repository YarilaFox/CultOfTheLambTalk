using System.Reflection;
using COTL_API.CustomFollowerCommand;
using UnityEngine;

namespace FollowerTalk;

internal sealed class TalkCommand : CustomFollowerCommand
{
    private static readonly MethodInfo? CloseForConversation =
        typeof(interaction_FollowerInteraction).GetMethod("Close",
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new[] { typeof(bool), typeof(bool), typeof(bool) }, null);
    private static readonly MethodInfo? ResetFollower =
        typeof(interaction_FollowerInteraction).GetMethod("ResetFollower",
            BindingFlags.Instance | BindingFlags.NonPublic);

    public TalkCommand()
    {
        // This COTL_API release has a placeholder icon but no public setter.
        FieldInfo? iconField = typeof(CustomFollowerCommand).GetField(
            "<CommandIcon>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        iconField?.SetValue(this, CreateSpeechIcon());
    }

    public override string InternalName => "FOLLOWER_TALK_PROTOTYPE";

    public override string GetTitle(Follower follower) => "Поговорить";

    public override string GetDescription(Follower follower) => "Написать последователю.";

    public override void Execute(
        interaction_FollowerInteraction interaction,
        FollowerCommands finalCommand = FollowerCommands.None)
    {
        Follower follower = interaction.follower;
        bool held = false;
        try
        {
            if (CloseForConversation != null)
            {
                CloseForConversation.Invoke(interaction, new object[] { false, false, false });
                held = true;
            }
        }
        catch (System.Exception) { }
        if (!held) base.Execute(interaction, finalCommand);
        Plugin.Instance.OpenTalk(follower, held ? interaction : null);
    }

    internal static void ReleaseFollower(interaction_FollowerInteraction? interaction)
    {
        if (interaction != null) ResetFollower?.Invoke(interaction, null);
    }

    private static Sprite CreateSpeechIcon()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point
        };
        Color clear = new Color(0, 0, 0, 0);
        Color ink = new Color(0.05f, 0.02f, 0.04f);
        Color paper = new Color(1f, 0.94f, 0.78f);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool outer = x >= 3 && x <= 28 && y >= 10 && y <= 26;
            bool tail = x >= 7 && x <= 13 && y >= 5 && y <= 10 && x - y <= 4;
            bool inner = x >= 5 && x <= 26 && y >= 12 && y <= 24;
            texture.SetPixel(x, y, outer || tail ? inner ? paper : ink : clear);
        }
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
    }
}
