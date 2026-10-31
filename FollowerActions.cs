using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
        public WorkAction(string id, string labelRu, string labelEn, FollowerTaskType type)
        {
            Id = id;
            LabelRu = labelRu;
            LabelEn = labelEn;
            Type = type;
        }

        public string Id { get; }
        public string LabelRu { get; }
        public string LabelEn { get; }
        public string Label(string language) => TalkLanguage.Pick(language, LabelRu, LabelEn);
        public FollowerTaskType Type { get; }
    }

    private static readonly WorkAction[] WorkActions =
    {
        new("build", "строить", "build", FollowerTaskType.Build),
        new("farm", "работать на ферме", "work on the farm", FollowerTaskType.Farm),
        new("pray", "молиться у святилища", "pray at the shrine", FollowerTaskType.Pray),
        new("clear_rubble", "расчищать завалы", "clear rubble", FollowerTaskType.ClearRubble),
        new("clear_weeds", "убирать сорняки", "clear weeds", FollowerTaskType.ClearWeeds),
        new("lumberjack", "работать на лесопилке", "work at the lumberyard", FollowerTaskType.ResourceStation),
        new("deposit_wood", "нести древесину", "carry wood", FollowerTaskType.DepositWood),
        new("research", "заниматься исследованиями", "research", FollowerTaskType.Research),
        new("forage", "собирать ресурсы", "forage", FollowerTaskType.Forage),
        new("chop_wood", "рубить деревья", "chop trees", FollowerTaskType.ChopTrees),
        new("fish", "ловить рыбу", "fish", FollowerTaskType.Fisherman),
        new("clean_waste", "убирать отходы", "clean waste", FollowerTaskType.CleanWaste),
        new("cook", "готовить еду", "cook food", FollowerTaskType.Cook),
        new("blacksmith", "работать в кузнице", "work at the forge", FollowerTaskType.Blacksmith),
        new("refine", "освящать материалы в переработчике", "consecrate materials at the refinery", FollowerTaskType.Refinery),
        new("resource_station", "работать на станции ресурсов", "work at a resource station", FollowerTaskType.ResourceStation),
        new("janitor", "убираться", "clean the grounds", FollowerTaskType.Janitor),
        new("undertaker", "заниматься погребением", "tend to burials", FollowerTaskType.Undertaker),
        new("brew", "варить напитки", "brew drinks", FollowerTaskType.Brew),
        new("medic", "лечить", "heal followers", FollowerTaskType.Medic),
        new("rancher", "ухаживать за животными", "tend animals", FollowerTaskType.Rancher),
        new("logistics", "разносить ресурсы", "deliver resources", FollowerTaskType.Logistics),
        new("handyman", "чинить постройки", "repair structures", FollowerTaskType.Handyman),
        new("mine_bloodstone", "добывать кровавый камень", "mine bloodstone", FollowerTaskType.MineBloodStone),
        new("mine_rotstone", "добывать гниющий камень", "mine rotstone", FollowerTaskType.MineRotstone),
        new("dance_firepit", "помогать у танцевального костра", "help at the dancing fire pit", FollowerTaskType.DanceFirePit),
        new("offering_shrine", "работать у алтаря подношений", "tend the offering shrine", FollowerTaskType.PassivePray),
        new("collect_scarf", "собирать ткань", "collect cloth", FollowerTaskType.CollectScarf),
        new("collect_devotion", "собирать преданность", "collect devotion", FollowerTaskType.CollectDevotion),
        new("feast_table", "работать у пиршественного стола", "tend the feast table", FollowerTaskType.EatFeastTable),
        new("trait_manipulator", "работать с изменением черт", "work with the trait manipulator", FollowerTaskType.TraitManipulator)
    };

    private static readonly MethodInfo? StudyTasks = typeof(FollowerBrain).GetMethod(
        "GetDesiredTask_Study", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo? PrayTasks = typeof(FollowerBrain).GetMethod(
        "GetDesiredTask_Pray", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo? LeisureTasks = typeof(FollowerBrain).GetMethod(
        "GetDesiredTask_Leisure", BindingFlags.Static | BindingFlags.NonPublic);

    public static IEnumerable<string> KnownIds
    {
        get
        {
            yield return "cancel";
            yield return "follow";
            yield return "stop_follow";
            yield return "sleep";
            yield return "eat";
            yield return "tithe";
            yield return "gift";
            yield return "pet";
            yield return "bless";
            yield return "inspire";
            yield return "drink";
            foreach (WorkAction action in WorkActions) yield return action.Id;
        }
    }

    public static bool IsKnown(string id) => id == "none" || KnownIds.Contains(id);

    public static string UnavailableReason(Follower follower, string id, string language)
    {
        if (id == "farm")
        {
            if (!HasFarmerStation(follower))
                return TalkLanguage.Pick(language,
                    "Фермерская станция не построена, работать на ферме сейчас невозможно.",
                    "There is no farm station, so farm work is unavailable.");
            return TalkLanguage.Pick(language,
                "Фермерская станция есть, но доступных заданий на ферме сейчас нет.",
                "There is a farm station, but no farm task is available now.");
        }
        if (id == "eat") return TalkLanguage.Pick(language, "Готовой еды сейчас нет.", "No prepared meal is available.");
        if (id == "tithe") return TalkLanguage.Pick(language,
            "Сбор податей сейчас недоступен: доктрина не открыта или подати за сегодня уже собраны.",
            "Tithes are unavailable: the doctrine is locked or today's tithe was already collected.");
        if (id == "gift") return TalkLanguage.Pick(language,
            "Сейчас нет подарков, которые можно вручить этому последователю.",
            "No gift can be given to this follower right now.");
        if (id == "pet") return TalkLanguage.Pick(language,
            "Сейчас этого последователя нельзя погладить через игровое действие.",
            "This follower cannot be petted through the game action right now.");
        if (id == "follow") return TalkLanguage.Pick(language,
            "Сейчас последователь не может идти за Ягнёнком.",
            "This follower cannot follow the Lamb right now.");
        if (id == "stop_follow") return TalkLanguage.Pick(language,
            "Последователь сейчас не следует за Ягнёнком.",
            "This follower is not following the Lamb.");
        if (id == "bless") return TalkLanguage.Pick(language,
            "Сегодня этот последователь уже получил благословение.",
            "This follower has already been blessed today.");
        if (id == "inspire") return TalkLanguage.Pick(language,
            "Воодушевление ещё не открыто или этот последователь уже воодушевлён.",
            "Inspiration is not unlocked or this follower is already inspired.");
        if (id == "drink") return TalkLanguage.Pick(language,
            "В трактире сейчас нет доступного напитка для этого последователя.",
            "No drink is currently available for this follower at the pub.");
        if (id == "cook") return HasKitchen(follower)
            ? TalkLanguage.Pick(language, "Кухня есть, но сейчас нет доступного задания на готовку.",
                "The kitchen exists, but no cooking task is available now.")
            : TalkLanguage.Pick(language, "Кухня ещё не построена, готовить сейчас негде.",
                "No kitchen has been built, so there is nowhere to cook.");
        if (id == "build") return !follower.Brain.CanWork
            ? TalkLanguage.Pick(language, "Этот последователь сейчас не может работать.",
                "This follower cannot work right now.")
            : TalkLanguage.Pick(language, "Сейчас нет доступной строительной площадки.",
                "No construction task is available right now.");
        if (id == "lumberjack" && !HasLumberyard(follower))
            return TalkLanguage.Pick(language, "Лесопилка пока не построена, работать на ней невозможно.",
                "No lumberyard has been built, so that work is unavailable.");
        if (id == "refine")
            return !follower.Brain.CanWork
                ? TalkLanguage.Pick(language, "Этот последователь сейчас не может работать.",
                    "This follower cannot work right now.")
                : HasRefinery(follower)
                    ? TalkLanguage.Pick(language,
                        "Переработчик есть, но сейчас нет задания на освящение материалов.",
                        "There is a refinery, but no consecration task is available now.")
                    : TalkLanguage.Pick(language,
                        "Переработчик ещё не построен, освящать материалы сейчас негде.",
                        "No refinery has been built, so materials cannot be consecrated yet.");
        foreach (WorkAction action in WorkActions)
            if (action.Id == id)
                return TalkLanguage.Pick(language,
                    "Для действия «" + action.Label(language) + "» сейчас нет доступного игрового задания.",
                    "No game task is available to " + action.Label(language) + " right now.");
        return TalkLanguage.Pick(language, "Это действие сейчас недоступно.",
            "This action is currently unavailable.");
    }

    public static ActionCatalog Inspect(Follower follower,
        interaction_FollowerInteraction? interaction, string language)
    {
        var ids = new List<string>();
        var descriptions = new List<string>();
        if (follower == null || follower.Brain == null)
            return new ActionCatalog(TalkLanguage.Pick(language,
                "Нет доступных действий.", "No actions available."), ids);

        ids.Add("sleep");
        descriptions.Add("sleep: " + TalkLanguage.Pick(language, "пойти спать", "go to sleep"));
        if (follower.Brain.FollowingPlayer)
        {
            ids.Add("stop_follow");
            descriptions.Add("stop_follow: " + TalkLanguage.Pick(language,
                "перестать идти за Ягнёнком", "stop following the Lamb"));
        }
        else if (PlayerFarming.Instance != null && follower.Brain.CanWork)
        {
            ids.Add("follow");
            descriptions.Add("follow: " + TalkLanguage.Pick(language,
                "идти за Ягнёнком", "follow the Lamb"));
        }
        if (FindMeal(follower) != null)
        {
            ids.Add("eat");
            descriptions.Add("eat: " + TalkLanguage.Pick(language, "поесть готовую еду", "eat a prepared meal"));
        }
        if (interaction != null &&
            DoctrineUpgradeSystem.GetUnlocked(
                DoctrineUpgradeSystem.DoctrineType.Possessions_ExtortTithes) &&
            !follower.Brain.Stats.PaidTithes)
        {
            ids.Add("tithe");
            descriptions.Add("tithe: " + TalkLanguage.Pick(language,
                "собрать игровые подати с этого последователя", "collect this follower's game tithe"));
        }
        if (interaction != null && FollowerCommandGroups.GiftCommands(follower).Count > 0)
        {
            ids.Add("gift");
            descriptions.Add("gift: " + TalkLanguage.Pick(language,
                "открыть игровое меню выбора подарка", "open the in-game gift selection"));
        }
        if (interaction != null && NativeFollowerActions.CanPet(follower))
        {
            ids.Add("pet");
            descriptions.Add("pet: " + TalkLanguage.Pick(language,
                "получить ласку от игрока", "be petted by the player"));
        }
        if (interaction != null && !follower.Brain.Stats.ReceivedBlessing)
        {
            ids.Add("bless");
            descriptions.Add("bless: " + TalkLanguage.Pick(language,
                "получить благословение игрока", "receive the player's blessing"));
        }

        if (interaction != null && NativeFollowerActions.CanInspire(follower))
        {
            ids.Add("inspire");
            descriptions.Add("inspire: " + TalkLanguage.Pick(language,
                "получить воодушевление от игрока", "receive inspiration from the player"));
        }
        if (interaction != null && NativeFollowerActions.CanDrink(follower))
        {
            ids.Add("drink");
            descriptions.Add("drink: " + TalkLanguage.Pick(language,
                "открыть игровое меню выбора напитка и уйти пить",
                "open the in-game drink selection, then leave to drink"));
        }

        List<FollowerTask> tasks = FindAvailableTasks(follower);
        foreach (WorkAction action in WorkActions)
        {
            if (action.Id == "farm" && !HasFarmerStation(follower)) continue;
            if (action.Id == "build" && (!follower.Brain.CanWork || !HasBuildSite(follower) ||
                !tasks.Any(task => task != null && task.Type == FollowerTaskType.Build)))
                continue;
            if (action.Id == "lumberjack" && !HasLumberyard(follower)) continue;
            if (!tasks.Any(task => task != null && task.Type == action.Type)
                && !(action.Id == "chop_wood" && FindTree(follower) != null)
                && action.Id != "build") continue;
            ids.Add(action.Id);
            descriptions.Add(action.Id + ": " + action.Label(language));
        }
        string farmState = !HasFarmerStation(follower)
            ? TalkLanguage.Pick(language, "фермерская станция не построена", "farm station not built")
            : ids.Contains("farm")
                ? TalkLanguage.Pick(language, "на ферме есть работа", "farm work available")
                : TalkLanguage.Pick(language, "на ферме сейчас нет заданий", "no farm task right now");
        string refineryState = ids.Contains("refine")
            ? TalkLanguage.Pick(language, "есть работа по освящению материалов",
                "consecration work is available")
            : UnavailableReason(follower, "refine", language);
        return new ActionCatalog(TalkLanguage.Pick(language, "Доступно: ", "Available: ") +
            string.Join("; ", descriptions) + TalkLanguage.Pick(language,
                ". Состояние фермы: ", ". Farm status: ") + farmState +
            TalkLanguage.Pick(language, ". Освящение материалов: ",
                ". Consecration: ") + refineryState + ".", ids);
    }

    public static string TaskName(FollowerTaskType task, string language, int structureId = 0)
    {
        if (task == FollowerTaskType.ResourceStation)
        {
            if (structureId != 0)
            {
                try
                {
                    Structures_LumberjackStation? station =
                        StructureManager.GetStructureByID<Structures_LumberjackStation>(structureId);
                    if (station is Structures_MinerStation)
                        return TalkLanguage.Pick(language, "добывал камень на шахте",
                            "mining stone");
                    if (station != null)
                        return TalkLanguage.Pick(language, "добывал древесину на лесопилке",
                            "gathering wood at the lumberyard");
                }
                catch (Exception) { }
            }
            return TalkLanguage.Pick(language, "добывал ресурсы на станции",
                "gathering resources at a station");
        }
        if (task == FollowerTaskType.Drinking) return TalkLanguage.Pick(language, "пьёт", "drinking");
        if (task == FollowerTaskType.Sleep || task == FollowerTaskType.SleepBedRest)
            return TalkLanguage.Pick(language, "спит", "sleeping");
        if (task == FollowerTaskType.EatMeal || task == FollowerTaskType.EatStoredFood)
            return TalkLanguage.Pick(language, "ест", "eating");
        if (task == FollowerTaskType.Study) return TalkLanguage.Pick(language, "учится", "studying");
        if (task == FollowerTaskType.Chat) return TalkLanguage.Pick(language, "общается", "chatting");
        foreach (WorkAction action in WorkActions)
            if (action.Type == task) return action.Label(language);
        return task.ToString();
    }

    public static bool TryExecute(Follower follower, string action,
        interaction_FollowerInteraction? interaction)
    {
        if (follower == null || follower.Brain == null) return false;
        if (action == "farm" && !HasFarmerStation(follower)) return false;
        try
        {
            if (action == "follow")
            {
                if (PlayerFarming.Instance == null || !follower.Brain.CanWork) return false;
                follower.Brain.FollowingPlayer = true;
                follower.Brain.CompleteCurrentTask();
                return follower.Brain.FollowingPlayer;
            }
            if (action == "stop_follow")
            {
                if (!follower.Brain.FollowingPlayer) return false;
                follower.Brain.FollowingPlayer = false;
                follower.Brain.CompleteCurrentTask();
                return !follower.Brain.FollowingPlayer;
            }
            if (action == "build") return StartBuilding(follower);
            if (action == "gift") return interaction != null && NativeFollowerActions.ShowGiftMenu(follower, interaction);
            if (action == "pet") return interaction != null && NativeFollowerActions.Pet(follower, interaction);
            if (action == "drink") return interaction != null && NativeFollowerActions.ShowDrinkMenu(follower, interaction);
            if (action == "inspire") return interaction != null && NativeFollowerActions.Inspire(follower, interaction);
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
                List<FollowerTask> tasks = FindAvailableTasks(follower);
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
        if (!brain.CanWork || !HasBuildSite(follower) ||
            !FindAvailableTasks(follower).Any(task => task != null &&
                task.Type == FollowerTaskType.Build))
            return false;
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

    private static bool HasRefinery(Follower follower)
    {
        try
        {
            var stations = new List<Structures_Refinery>();
            StructureManager.TryGetAllStructuresOfType(ref stations, follower.Brain.Location, null);
            return stations.Any(station => station != null && station.Data != null);
        }
        catch (Exception) { return false; }
    }

    private static List<FollowerTask> FindAvailableTasks(Follower follower)
    {
        var tasks = new List<FollowerTask>();
        try { tasks.AddRange(FollowerBrain.GetDesiredTask_Work(follower.Brain.Location)); }
        catch (Exception) { }
        foreach (MethodInfo? method in new[] { StudyTasks, PrayTasks, LeisureTasks })
        {
            if (method == null) continue;
            try
            {
                object[] args = method.GetParameters().Length == 0
                    ? Array.Empty<object>() : new object[] { follower.Brain.Location };
                if (method.Invoke(null, args) is List<FollowerTask> available)
                    tasks.AddRange(available);
            }
            catch (Exception) { }
        }
        return tasks;
    }

    private static bool HasKitchen(Follower follower)
    {
        try
        {
            var kitchens = new List<Structures_Kitchen>();
            StructureManager.TryGetAllStructuresOfType(ref kitchens, follower.Brain.Location, null);
            return kitchens.Any(kitchen => kitchen != null && kitchen.Data != null);
        }
        catch (Exception) { return false; }
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
