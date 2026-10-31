using System;
using System.Collections;
using System.Collections.Generic;
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

    private static CommandItem? PetCommand(Follower follower)
    {
        foreach (Func<Follower, List<CommandItem>> commands in new Func<Follower, List<CommandItem>>[]
        {
            FollowerCommandGroups.MakeDemandCommands,
            FollowerCommandGroups.OldAgeCommands,
            FollowerCommandGroups.DrunkCommands
        })
        {
            try
            {
                foreach (CommandItem item in commands(follower))
                    if ((item.Command == FollowerCommands.PetDog ||
                         item.Command == FollowerCommands.PetFollower) && item.IsAvailable(follower))
                        return item;
            }
            catch (Exception) { }
        }
        return null;
    }

    public static bool CanPet(Follower follower) => PetCommand(follower) != null;

    public static bool Pet(Follower follower, interaction_FollowerInteraction interaction)
    {
        if (FinishCommand == null) return false;
        try
        {
            CommandItem? item = PetCommand(follower);
            if (item == null) return false;
            FinishCommand.Invoke(interaction, new object[] { new[] { item.Command } });
            return true;
        }
        catch (Exception error) { Plugin.Instance.LogActionError("pet", error); }
        return false;
    }

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

    public static bool CanDrink(Follower follower)
    {
        try
        {
            var drink = FollowerCommandItems.DrinkSomething(follower);
            return drink?.SubCommands != null && drink.SubCommands.Count > 0;
        }
        catch (Exception) { return false; }
    }

    public static bool ShowDrinkMenu(Follower follower, interaction_FollowerInteraction interaction)
    {
        var drink = FollowerCommandItems.DrinkSomething(follower);
        if (drink?.SubCommands == null || drink.SubCommands.Count == 0 || FinishCommand == null)
            return false;
        UIFollowerInteractionWheelOverlayController? wheel = null;
        try
        {
            var template = Lamb.UI.UIManager.Instance.FollowerInteractionWheelTemplate;
            wheel = UnityEngine.Object.Instantiate(template.gameObject)
                .GetComponent<UIFollowerInteractionWheelOverlayController>();
            InteractionWheel?.SetValue(interaction, wheel);
            src.UINavigator.UINavigatorNew.Instance.AllowInputOnlyFromPlayer =
                interaction.playerFarming;
            wheel.Show(follower, drink.SubCommands, false, true, null);
            wheel.OnItemChosen += commands =>
            {
                if (commands == null || commands.Length == 0) return;
                try { FinishCommand.Invoke(interaction, new object[] { commands }); }
                catch (Exception error)
                {
                    Plugin.Instance.LogActionError("drink", error);
                    CloseInteraction?.Invoke(interaction, null);
                }
            };
            wheel.OnCancel += () => CloseInteraction?.Invoke(interaction, null);
            GameManager.GetInstance().OnConversationNew(true, true, null);
            GameManager.GetInstance().OnConversationNext(interaction.gameObject, 4f);
            return true;
        }
        catch (Exception error)
        {
            InteractionWheel?.SetValue(interaction, null);
            if (wheel != null) UnityEngine.Object.Destroy(wheel.gameObject);
            Plugin.Instance.LogActionError("drink", error);
            return false;
        }
    }

    public static bool CanInspire(Follower follower) =>
        DoctrineUpgradeSystem.GetUnlocked(
            DoctrineUpgradeSystem.DoctrineType.WorkWorship_Inspire) &&
        !follower.Brain.Stats.Inspired;

    public static bool Inspire(Follower follower, interaction_FollowerInteraction interaction)
    {
        if (!CanInspire(follower) || FinishCommand == null) return false;
        try
        {
            FinishCommand.Invoke(interaction,
                new object[] { new[] { FollowerCommands.Dance } });
            return true;
        }
        catch (Exception error)
        {
            Plugin.Instance.LogActionError("inspire", error);
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
