using System;

namespace FollowerTalk;

internal static class FollowerConflictRules
{
    public const string Off = "off";
    public const string Feelings = "feelings";
    public const string MayLeave = "may_leave";
    public static readonly string[] Modes = { Off, Feelings, MayLeave };

    public static bool IsValidMode(string mode) => Array.IndexOf(Modes, mode) >= 0;

    public static int Apply(int score, string effect) => Math.Max(0, Math.Min(100,
        score + (effect switch
        {
            "hurt" => 10,
            "deep_hurt" => 20,
            "repair" => -12,
            "deep_repair" => -24,
            _ => 0
        })));

    public static bool IsValidEffect(string effect) =>
        effect == "same" || effect == "hurt" || effect == "deep_hurt" ||
        effect == "repair" || effect == "deep_repair";

    public static string Level(int score) => score >= 75 ? "ready_to_leave" :
        score >= 45 ? "resentful" : score >= 20 ? "hurt" : "calm";

    public static bool AllowDeparture(string mode, int score, string intent,
        bool imprisoned) => mode == MayLeave && score >= 75 &&
        intent == "leave" && !imprisoned;
}
