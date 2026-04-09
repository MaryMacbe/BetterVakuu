using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;

namespace ModTest.Scripts.Planning;

public interface IPlanningPolicy : ICombatPlanner, IMapPlanner, IRewardPlanner, ICardChoicePlanner
{
}

public interface IPlanningSnapshotBuilder
{
    PlanningSnapshot Build();
}

public interface ICombatPlanner
{
    CombatPlan? PlanCombat(PlanningSnapshot snapshot);
}

public interface IMapPlanner
{
    MapPlan? PlanMap(PlanningSnapshot snapshot, IReadOnlyList<MapPoint> candidates);
}

public interface IRewardPlanner
{
    RewardPlan? PlanReward(PlanningSnapshot snapshot, IReadOnlyList<Reward> rewards);
}

public interface ICardChoicePlanner
{
    CardChoicePlan PlanCardReward(PlanningSnapshot snapshot, IReadOnlyList<CardModel> cards);

    IReadOnlyList<CardModel> PlanCardSelection(PlanningSnapshot snapshot, IReadOnlyList<CardModel> cards, int minSelect, int maxSelect);
}

public sealed record CombatPlan(CardModel Card, Creature? Target, float Score, string Reason);

public sealed record MapPlan(MapPoint Point, float Score, string Reason);

public sealed record RewardPlan(Reward? Reward, float Score, string Reason);

public sealed record CardChoicePlan(CardModel? Card, float Score, string Reason);
