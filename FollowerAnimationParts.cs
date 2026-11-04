using System;

namespace FollowerTalk;

internal static class FollowerAnimationParts
{
    public static bool IsMouth(string name) =>
        name.IndexOf("mouth", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("jaw", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("tongue", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("lip", StringComparison.OrdinalIgnoreCase) >= 0;
}
