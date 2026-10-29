using System;

namespace FollowerTalk;

internal static class FollowerEmotion
{
    public static void Apply(Follower? follower, string emotion)
    {
        if (follower == null || follower.Brain == null) return;
        string face = emotion switch
        {
            "agree" or "amused" => "Emotions/emotion-happy",
            "angry" => "Emotions/emotion-angry",
            "sad" or "uncertain" => "Emotions/emotion-unhappy",
            "worried" => "Emotions/emotion-scared",
            _ => "Emotions/emotion-normal"
        };
        try
        {
            follower.OverridingEmotions = true;
            follower.SetFaceAnimation(face, true);
        }
        catch (Exception error)
        {
            follower.OverridingEmotions = false;
            Plugin.Instance.LogActionError("emotion", error);
        }
    }

    public static void Restore(Follower? follower)
    {
        if (follower == null || follower.Brain == null) return;
        try
        {
            follower.OverridingEmotions = false;
            follower.SetEmotionAnimation();
        }
        catch (Exception error)
        {
            Plugin.Instance.LogActionError("restore emotion", error);
        }
    }
}
