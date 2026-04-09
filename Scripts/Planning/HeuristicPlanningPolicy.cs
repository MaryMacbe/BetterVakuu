using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;

namespace ModTest.Scripts.Planning;

public sealed class HeuristicPlanningPolicy : IPlanningPolicy
{
    private readonly HeuristicCardScorer _cardScorer = new HeuristicCardScorer();

    public CombatPlan? PlanCombat(PlanningSnapshot snapshot)
    {
        CombatPlan? bestPlan = null;

        foreach (CardModel card in snapshot.Hand)
        {
            if (!CombatTargeting.TryResolveTarget(card, out var target))
            {
                continue;
            }

            float score = _cardScorer.ScoreCombatCard(snapshot, card, target);
            if (bestPlan == null || score > bestPlan.Score)
            {
                bestPlan = new CombatPlan(card, target, score, $"heuristic combat score {score:F2}");
            }
        }

        return bestPlan;
    }

    public MapPlan? PlanMap(PlanningSnapshot snapshot, IReadOnlyList<MapPoint> candidates)
    {
        MapPlan? bestPlan = null;
        bool previousWasShop = snapshot.RunState.CurrentMapPointHistoryEntry?.HasRoomOfType(RoomType.Shop) ?? false;

        foreach (MapPoint point in candidates)
        {
            float score = ScoreMapPoint(
                snapshot,
                point,
                snapshot.Random.UnknownMapOdds.Clone(),
                snapshot.Random.SeenUnknownMapPoints,
                previousWasShop,
                PlannerConfig.MapLookaheadDepth);

            if (bestPlan == null || score > bestPlan.Score)
            {
                bestPlan = new MapPlan(point, score, $"heuristic map score {score:F2}");
            }
        }

        return bestPlan;
    }

    public RewardPlan? PlanReward(PlanningSnapshot snapshot, IReadOnlyList<Reward> rewards)
    {
        RewardPlan? bestPlan = null;

        foreach (Reward reward in rewards)
        {
            float score = ScoreReward(snapshot, reward);
            if (bestPlan == null || score > bestPlan.Score)
            {
                bestPlan = new RewardPlan(reward, score, $"heuristic reward score {score:F2}");
            }
        }

        if (bestPlan != null && bestPlan.Reward != null)
        {
            return bestPlan;
        }

        return new RewardPlan(null, 0f, "skip remaining rewards");
    }

    public CardChoicePlan PlanCardReward(PlanningSnapshot snapshot, IReadOnlyList<CardModel> cards)
    {
        CardModel? bestCard = null;
        float bestScore = float.NegativeInfinity;

        foreach (CardModel card in cards)
        {
            float score = _cardScorer.ScoreRewardCard(snapshot, card);
            if (score > bestScore)
            {
                bestScore = score;
                bestCard = card;
            }
        }

        return new CardChoicePlan(bestCard, bestScore, $"heuristic reward-card score {bestScore:F2}");
    }

    public IReadOnlyList<CardModel> PlanCardSelection(PlanningSnapshot snapshot, IReadOnlyList<CardModel> cards, int minSelect, int maxSelect)
    {
        if (cards.Count == 0 || maxSelect <= 0)
        {
            return [];
        }

        IEnumerable<CardModel> orderedCards = cards.Count <= 5
            ? cards.OrderByDescending(card => _cardScorer.ScoreRewardCard(snapshot, card))
            : cards.OrderByDescending(card => _cardScorer.ScoreRemovalCandidate(snapshot, card));

        int targetCount = Math.Clamp(minSelect, 0, maxSelect);
        if (targetCount == 0 && cards.Count <= 3 && maxSelect > 0)
        {
            targetCount = 1;
        }

        List<CardModel> result = orderedCards
            .Take(targetCount)
            .ToList();

        if (result.Count == 0 && minSelect > 0)
        {
            result.Add(cards[0]);
        }

        return result;
    }

    private float ScoreReward(PlanningSnapshot snapshot, Reward reward)
    {
        return reward switch
        {
            CardReward => 8f,
            RelicReward => 7f,
            GoldReward => 6f,
            PotionReward when snapshot.Player.HasOpenPotionSlots => 5f,
            PotionReward => float.NegativeInfinity,
            _ => 1f
        };
    }

    private float ScoreMapPoint(
        PlanningSnapshot snapshot,
        MapPoint point,
        SimulatedUnknownMapOdds odds,
        int seenUnknownCount,
        bool previousWasShop,
        int remainingDepth)
    {
        float totalScore = 0f;
        IReadOnlyCollection<RoomType> blacklist = BuildBlacklist(previousWasShop, point.Children);

        foreach (MapOutcome outcome in EnumerateOutcomes(snapshot, point, odds, seenUnknownCount, blacklist))
        {
            float immediateScore = ScoreRoomType(snapshot, outcome.RoomType);
            float futureScore = 0f;

            if (remainingDepth > 0 && point.Children.Count > 0)
            {
                futureScore = point.Children
                    .Select(child => ScoreMapPoint(
                        snapshot,
                        child,
                        outcome.NextOdds.Clone(),
                        outcome.NextSeenUnknownCount,
                        outcome.RoomType == RoomType.Shop,
                        remainingDepth - 1))
                    .DefaultIfEmpty(0f)
                    .Max();
            }

            totalScore += outcome.Probability * (immediateScore + PlannerConfig.MapLookaheadDiscount * futureScore);
        }

        return totalScore;
    }

    private IEnumerable<MapOutcome> EnumerateOutcomes(
        PlanningSnapshot snapshot,
        MapPoint point,
        SimulatedUnknownMapOdds odds,
        int seenUnknownCount,
        IReadOnlyCollection<RoomType> blacklist)
    {
        if (point.PointType != MapPointType.Unknown)
        {
            yield return new MapOutcome(ResolveRoomType(point.PointType), 1f, odds.Clone(), seenUnknownCount);
            yield break;
        }

        if (snapshot.Random.NumberOfRuns == 0)
        {
            if (seenUnknownCount < 2)
            {
                yield return new MapOutcome(RoomType.Event, 1f, AdvanceOdds(odds, RoomType.Event, BuildAllowedRoomTypes(blacklist)), seenUnknownCount + 1);
                yield break;
            }

            if (seenUnknownCount == 2)
            {
                yield return new MapOutcome(RoomType.Monster, 1f, AdvanceOdds(odds, RoomType.Monster, BuildAllowedRoomTypes(blacklist)), seenUnknownCount + 1);
                yield break;
            }
        }

        HashSet<RoomType> allowedRoomTypes = BuildAllowedRoomTypes(blacklist);
        Dictionary<RoomType, float> rawWeights = allowedRoomTypes.ToDictionary(roomType => roomType, roomType => Math.Max(0f, odds.GetOdds(roomType)));
        float totalWeight = rawWeights.Values.Sum();

        if (totalWeight <= 0f)
        {
            RoomType fallbackRoom = allowedRoomTypes.Contains(RoomType.Event) ? RoomType.Event : allowedRoomTypes.Order().First();
            yield return new MapOutcome(fallbackRoom, 1f, AdvanceOdds(odds, fallbackRoom, allowedRoomTypes), seenUnknownCount + 1);
            yield break;
        }

        foreach (KeyValuePair<RoomType, float> pair in rawWeights.Where(static pair => pair.Value > 0f))
        {
            yield return new MapOutcome(
                pair.Key,
                pair.Value / totalWeight,
                AdvanceOdds(odds, pair.Key, allowedRoomTypes),
                seenUnknownCount + 1);
        }
    }

    private static HashSet<RoomType> BuildBlacklist(bool previousWasShop, IReadOnlyCollection<MapPoint> nextMapPoints)
    {
        HashSet<RoomType> blacklist = new HashSet<RoomType>();
        if (previousWasShop || (nextMapPoints.Count > 0 && nextMapPoints.All(static point => point.PointType == MapPointType.Shop)))
        {
            blacklist.Add(RoomType.Shop);
        }

        return blacklist;
    }

    private static HashSet<RoomType> BuildAllowedRoomTypes(IReadOnlyCollection<RoomType> blacklist)
    {
        HashSet<RoomType> allowed = new HashSet<RoomType>
        {
            RoomType.Event,
            RoomType.Monster,
            RoomType.Elite,
            RoomType.Treasure,
            RoomType.Shop
        };

        allowed.ExceptWith(blacklist);
        if (allowed.Count == 0)
        {
            allowed.Add(RoomType.Event);
        }

        return allowed;
    }

    private static SimulatedUnknownMapOdds AdvanceOdds(SimulatedUnknownMapOdds odds, RoomType selectedRoomType, IReadOnlyCollection<RoomType> allowedRoomTypes)
    {
        SimulatedUnknownMapOdds clone = odds.Clone();
        clone.Advance(selectedRoomType, allowedRoomTypes);
        return clone;
    }

    private static RoomType ResolveRoomType(MapPointType pointType)
    {
        return pointType switch
        {
            MapPointType.Unknown => RoomType.Event,
            MapPointType.Shop => RoomType.Shop,
            MapPointType.Treasure => RoomType.Treasure,
            MapPointType.RestSite => RoomType.RestSite,
            MapPointType.Monster => RoomType.Monster,
            MapPointType.Elite => RoomType.Elite,
            MapPointType.Boss => RoomType.Boss,
            MapPointType.Ancient => RoomType.Event,
            _ => RoomType.Unassigned
        };
    }

    private static float ScoreRoomType(PlanningSnapshot snapshot, RoomType roomType)
    {
        return roomType switch
        {
            RoomType.Elite => 6f,
            RoomType.Treasure => 4.5f,
            RoomType.Event => 3.75f,
            RoomType.Monster => 3f,
            RoomType.Shop => snapshot.Player.Gold >= 250 ? 4f : 2f,
            RoomType.RestSite => snapshot.Player.Creature.CurrentHp * 2 < snapshot.Player.Creature.MaxHp ? 5.25f : 2.5f,
            RoomType.Boss => 10f,
            _ => 0f
        };
    }

    private sealed record MapOutcome(RoomType RoomType, float Probability, SimulatedUnknownMapOdds NextOdds, int NextSeenUnknownCount);
}
