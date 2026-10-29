using System;
using System.Collections;
using System.Reflection;
using Lamb.UI.FollowerInteractionWheel;
using UnityEngine;

namespace FollowerTalk;

internal static class NativeFollowerActions
{
    private static readonly MethodInfo? FinishCommand =
        typeof(interaction_FollowerInteraction).GetMethod("OnFollowerCommandFinalized",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo? CloseInteraction =
        typeof(interaction_FollowerInteraction).GetMethod("Close",
            BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
    private static readonly MethodInfo? ExtortRoutine =
        typeof(interaction_FollowerInteraction).GetMethod("ExtortMoneyRoutine",
            BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? InteractionWheel =
        typeof(interaction_FollowerInteraction).GetField("followerInteractionWheelInstance",
            BindingFlags.Instance | BindingFlags.NonPublic);

    public static bool ShowGiftMenu(Follower follower, interaction_FollowerInteraction interaction)
    {
        var gifts = FollowerCommandGroups.GiftCommands(follower);
        if (gifts.Count == 0 || FinishCommand == null) return false;
        UIFollowerInteractionWheelOverlayController? wheel = null;
        try
        {
            var template = Lamb.UI.UIManager.Instance.FollowerInteractionWheelTemplate;
            wheel = UnityEngine.Object.Instantiate(template.gameObject)
                .GetComponent<UIFollowerInteractionWheelOverlayController>();
            InteractionWheel?.SetValue(interaction, wheel);
            src.UINavigator.UINavigatorNew.Instance.AllowInputOnlyFromPlayer =
                interaction.playerFarming;
            wheel.Show(follower, gifts, false, true, null);
            wheel.OnItemChosen += commands =>
            {
                if (commands == null || commands.Length == 0) return;
                Plugin.Instance.LogGiftSelection(commands[0].ToString());
                Plugin.Instance.TrackGift(follower);
                try { FinishCommand.Invoke(interaction, new object[] { commands }); }
                catch (Exception error)
                {
                    Plugin.Instance.ClearGift(follower);
                    Plugin.Instance.LogActionError("gift", error);
                    CloseInteraction?.Invoke(interaction, null);
                }
            };
            wheel.OnCancel += () =>
            {
                Plugin.Instance.LogGiftSelection("cancel");
                Plugin.Instance.ClearGift(follower);
                CloseInteraction?.Invoke(interaction, null);
            };
            GameManager.GetInstance().OnConversationNew(true, true, null);
            GameManager.GetInstance().OnConversationNext(interaction.gameObject, 4f);
            return true;
        }
        catch (Exception error)
        {
            InteractionWheel?.SetValue(interaction, null);
            if (wheel != null) UnityEngine.Object.Destroy(wheel.gameObject);
            Plugin.Instance.LogActionError("gift", error);
            return false;
        }
    }

    public static bool CollectTithe(Follower follower, interaction_FollowerInteraction interaction)
    {
        if (ExtortRoutine == null || follower.Brain.Stats.PaidTithes ||
            !DoctrineUpgradeSystem.GetUnlocked(
                DoctrineUpgradeSystem.DoctrineType.Possessions_ExtortTithes)) return false;
        try
        {
            if (ExtortRoutine?.Invoke(interaction, null) is not IEnumerator routine)
                return false;
            follower.Brain.Stats.PaidTithes = true;
            Plugin.Instance.RunConversation(FinishTithe(routine, follower));
            return true;
        }
        catch (Exception error)
        {
            follower.Brain.Stats.PaidTithes = false;
            Plugin.Instance.LogActionError("tithe", error);
            return false;
        }
    }

    private static IEnumerator FinishTithe(IEnumerator routine, Follower follower)
    {
        yield return routine;
        Plugin.Instance.ResumeAfterTithe(follower);
    }

    public static bool Bless(Follower follower, interaction_FollowerInteraction interaction)
    {
        if (follower.Brain.Stats.ReceivedBlessing || PlayerFarming.Instance == null) return false;
        try
        {
            follower.Brain.Stats.ReceivedBlessing = true;
            Plugin.Instance.RunConversation(FinishBlessing(interaction, follower));
            return true;
        }
        catch (Exception error)
        {
            follower.Brain.Stats.ReceivedBlessing = false;
            Plugin.Instance.LogActionError("bless", error);
            return false;
        }
    }

    private static IEnumerator FinishBlessing(interaction_FollowerInteraction interaction,
        Follower follower)
    {
        yield return interaction.BlessRoutine(false, PlayerFarming.Instance);
        Plugin.Instance.Faith.Change(follower, 2);
    }
}
