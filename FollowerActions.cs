using System;
using System.Collections.Generic;
using System.Linq;

namespace FollowerTalk;

internal sealed class ActionCatalog
{
    public ActionCatalog(string description, List<string> ids)
    {
        Description = description;
        Ids = ids;
    }

    public string Description { get; }
    public List<string> Ids { get; }
    public bool Contains(string id) => id == "none" || Ids.Contains(id);
}

internal static class FollowerActions
{
    private readonly struct WorkAction
    {
        public WorkAction(string id, string label, FollowerTaskType type)
        {
            Id = id;
            Label = label;
            Type = type;
        }

        public string Id { get; }
        public string Label { get; }
        public FollowerTaskType Type { get; }
    }

    private static readonly WorkAction[] WorkActions =
    {
        new("build", "строить", FollowerTaskType.Build),
        new("farm", "работать на ферме", FollowerTaskType.Farm),
        new("pray", "молиться у святилища", FollowerTaskType.Pray),
        new("clear_rubble", "расчищать завалы", FollowerTaskType.ClearRubble),
        new("clear_weeds", "убирать сорняки", FollowerTaskType.ClearWeeds),
        new("lumberjack", "работать на лесопилке", FollowerTaskType.Lumberjack),
        new("deposit_wood", "нести древесину", FollowerTaskType.DepositWood),
        new("research", "заниматься исследованиями", FollowerTaskType.Research),
        new("forage", "собирать ресурсы", FollowerTaskType.Forage),
        new("chop_wood", "рубить деревья", FollowerTaskType.ChopTrees),
        new("fish", "ловить рыбу", FollowerTaskType.Fisherman),
        new("clean_waste", "убирать отходы", FollowerTaskType.CleanWaste),
        new("cook", "готовить еду", FollowerTaskType.Cook),
        new("blacksmith", "работать в кузнице", FollowerTaskType.Blacksmith),
        new("refine", "перерабатывать материалы", FollowerTaskType.Refinery),
        new("resource_station", "работать на станции ресурсов", FollowerTaskType.ResourceStation),
        new("janitor", "убираться", FollowerTaskType.Janitor),
        new("undertaker", "заниматься погребением", FollowerTaskType.Undertaker),
        new("brew", "варить напитки", FollowerTaskType.Brew),
        new("medic", "лечить", FollowerTaskType.Medic),
        new("rancher", "ухаживать за животными", FollowerTaskType.Rancher),
        new("logistics", "разносить ресурсы", FollowerTaskType.Logistics),
        new("handyman", "чинить постройки", FollowerTaskType.Handyman),
        new("mine_bloodstone", "добывать кровавый камень", FollowerTaskType.MineBloodStone),
        new("mine_rotstone", "добывать гниющий камень", FollowerTaskType.MineRotstone)
    };

    public static IEnumerable<string> KnownIds
    {
        get
        {
            yield return "cancel";
            yield return "sleep";
            yield return "eat";
            yield return "tithe";
            yield return "gift";
            yield return "bless";
            foreach (WorkAction action in WorkActions) yield return action.Id;
        }
    }

    public static bool IsKnown(string id) => id == "none" || KnownIds.Contains(id);

    public static string UnavailableReason(Follower follower, string id)
    {
        if (id == "farm")
        {
            if (!HasFarmerStation(follower))
                return "Фермерская станция не построена, работать на ферме сейчас невозможно.";
            return "Фермерская станция есть, но доступных заданий на ферме сейчас нет.";
        }
        if (id == "eat") return "Готовой еды сейчас нет.";
        if (id == "tithe") return "Сбор податей сейчас недоступен: доктрина не открыта или подати за сегодня уже собраны.";
        if (id == "gift") return "Сейчас нет подарков, которые можно вручить этому последователю.";
        if (id == "bless") return "Сегодня этот последователь уже получил благословение.";
        if (id == "build") return !follower.Brain.CanWork
            ? "Этот последователь сейчас не может работать."
            : "Сейчас нет доступной строительной площадки.";
        if (id == "lumberjack" && !HasLumberyard(follower))
            return "Лесопилка пока не построена, работать на ней невозможно.";
        foreach (WorkAction action in WorkActions)
            if (action.Id == id)
                return "Для действия «" + action.Label + "» сейчас нет доступного игрового задания.";
        return "Это действие сейчас недоступно.";
    }

    public static ActionCatalog Inspect(Follower follower,
        interaction_FollowerInteraction? interaction)
    {
        var ids = new List<string>();
        var descriptions = new List<string>();
        if (follower == null || follower.Brain == null)
            return new ActionCatalog("Нет доступных действий.", ids);

        ids.Add("sleep");
        descriptions.Add("sleep: пойти спать");
        if (FindMeal(follower) != null)
        {
            ids.Add("eat");
            descriptions.Add("eat: поесть готовую еду");
        }
        if (interaction != null &&
            DoctrineUpgradeSystem.GetUnlocked(
                DoctrineUpgradeSystem.DoctrineType.Possessions_ExtortTithes) &&
            !follower.Brain.Stats.PaidTithes)
        {
            ids.Add("tithe");
            descriptions.Add("tithe: собрать игровые подати с этого последователя");
        }
        if (interaction != null && FollowerCommandGroups.GiftCommands(follower).Count > 0)
        {
            ids.Add("gift");
            descriptions.Add("gift: открыть игровое меню выбора подарка");
        }
        if (interaction != null && !follower.Brain.Stats.ReceivedBlessing)
        {
            ids.Add("bless");
            descriptions.Add("bless: получить благословение игрока");
        }

        List<FollowerTask> tasks = FindWork(follower);
        foreach (WorkAction action in WorkActions)
        {
            if (action.Id == "farm" && !HasFarmerStation(follower)) continue;
            if (action.Id == "build" && (!follower.Brain.CanWork || !HasBuildSite(follower)))
                continue;
            if (action.Id == "lumberjack" && !HasLumberyard(follower)) continue;
            if (!tasks.Any(task => task != null && task.Type == action.Type)
                && !(action.Id == "chop_wood" && FindTree(follower) != null)
                && action.Id != "build") continue;
            ids.Add(action.Id);
            descriptions.Add(action.Id + ": " + action.Label);
        }
        string farmState = !HasFarmerStation(follower)
            ? "фермерская станция не построена"
            : ids.Contains("farm") ? "на ферме есть работа" : "на ферме сейчас нет заданий";
        return new ActionCatalog("Доступно: " + string.Join("; ", descriptions) +
            ". Состояние фермы: " + farmState + ".", ids);
    }

    public static bool TryExecute(Follower follower, string action,
        interaction_FollowerInteraction? interaction)
    {
        if (follower == null || follower.Brain == null) return false;
        if (action == "farm" && !HasFarmerStation(follower)) return false;
        try
        {
            if (action == "build") return StartBuilding(follower);
            if (action == "gift") return interaction != null && NativeFollowerActions.ShowGiftMenu(follower, interaction);
            if (action == "bless")
                return interaction != null && NativeFollowerActions.Bless(follower, interaction);
            if (action == "tithe")
                return interaction != null && NativeFollowerActions.CollectTithe(follower, interaction);
            FollowerTask? task = null;
            if (action == "sleep") task = new FollowerTask_Sleep(false, true, false);
            else if (action == "eat")
            {
                Structures_Meal? meal = FindMeal(follower);
                if (meal != null) task = new FollowerTask_EatMeal(meal.Data.ID);
            }
            else
            {
                List<FollowerTask> tasks = FindWork(follower);
                foreach (WorkAction spec in WorkActions)
                    if (spec.Id == action)
                    {
                        task = tasks.FirstOrDefault(candidate => candidate != null && candidate.Type == spec.Type);
                        break;
                    }
                if (task == null && action == "chop_wood")
                {
                    Structures_Tree? tree = FindTree(follower);
                    if (tree != null) task = new FollowerTask_ChopTrees(tree.Data.ID);
                }
            }
            if (task == null) return false;
            follower.Brain.HardSwapToTask(task);
            return true;
        }
        catch (Exception error)
        {
            Plugin.Instance.LogActionError(action, error);
            return false;
        }
    }

    private static bool StartBuilding(Follower follower)
    {
        FollowerBrain brain = follower.Brain;
        if (!brain.CanWork || !HasBuildSite(follower)) return false;
        if (brain.Info.FollowerRole != FollowerRole.Worker)
        {
            brain.Info.FollowerRole = FollowerRole.Worker;
            brain.Info.Outfit = FollowerOutfitType.Follower;
            follower.SetOutfit(FollowerOutfitType.Follower, false, Thought.None);
        }
        brain.Info.WorkerPriority = WorkerPriority.Build;
        brain.Stats.WorkerBeenGivenOrders = true;
        brain.CompleteCurrentTask();
        return true;
    }

    private static bool HasBuildSite(Follower follower)
    {
        try
        {
            var sites = new List<Structures_BuildSite>();
            var projects = new List<Structures_BuildSiteProject>();
            StructureManager.TryGetAllStructuresOfType(ref sites, follower.Brain.Location, null);
            StructureManager.TryGetAllStructuresOfType(ref projects, follower.Brain.Location, null);
            return sites.Any(site => site != null && site.Data != null) ||
                projects.Any(site => site != null && site.Data != null);
        }
        catch (Exception) { return false; }
    }

    private static bool HasLumberyard(Follower follower)
    {
        try
        {
            var stations = new List<Structures_LumberjackStation>();
            StructureManager.TryGetAllStructuresOfType(ref stations, follower.Brain.Location, null);
            return stations.Any(station => station != null && station.Data != null);
        }
        catch (Exception) { return false; }
    }

    private static List<FollowerTask> FindWork(Follower follower)
    {
        try { return FollowerBrain.GetDesiredTask_Work(follower.Brain.Location); }
        catch (Exception) { return new List<FollowerTask>(); }
    }

    private static bool HasFarmerStation(Follower follower)
    {
        try
        {
            var stations = new List<Structures_FarmerStation>();
            StructureManager.TryGetAllStructuresOfType(ref stations, follower.Brain.Location, null);
            return stations.Any(station => station != null && station.Data != null);
        }
        catch (Exception) { return false; }
    }

    private static Structures_Meal? FindMeal(Follower follower)
    {
        try
        {
            var meals = new List<Structures_Meal>();
            StructureManager.TryGetAllStructuresOfType(ref meals, follower.Brain.Location, null);
            foreach (Structures_Meal meal in meals)
            {
                if (meal == null || meal.Data == null || !meal.CanEat() || meal.ReservedForTask
                    || meal.Data.Rotten || meal.Data.Burned) continue;
                return meal;
            }
        }
        catch (Exception) { }
        return null;
    }

    private static Structures_Tree? FindTree(Follower follower)
    {
        try
        {
            var trees = new List<Structures_Tree>();
            StructureManager.TryGetAllStructuresOfType(ref trees, follower.Brain.Location, null);
            foreach (Structures_Tree tree in trees)
            {
                if (tree == null || tree.Data == null || tree.ReservedForTask || tree.TreeChopped
                    || tree.Data.IsSapling) continue;
                return tree;
            }
        }
        catch (Exception) { }
        return null;
    }
}
