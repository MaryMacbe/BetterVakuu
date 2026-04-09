using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace ModTest.Scripts.Planning;

public sealed class PlanningSnapshot
{
    public required RunState RunState { get; init; }

    public required Player Player { get; init; }

    public required DeckMetrics DeckMetrics { get; init; }

    public required PlanningRandomSnapshot Random { get; init; }

    public CombatState? CombatState { get; init; }

    public IReadOnlyList<CardModel> Hand { get; init; } = [];

    public IReadOnlyList<CardModel> DrawPile { get; init; } = [];

    public IReadOnlyList<CardModel> DiscardPile { get; init; } = [];

    public IReadOnlyList<CardModel> Deck { get; init; } = [];

    public IReadOnlyList<PotionModel> Potions { get; init; } = [];

    public IReadOnlyList<Creature> Enemies { get; init; } = [];

    public IReadOnlyList<Creature> Allies { get; init; } = [];

    public IReadOnlyList<MapPoint> NextMapPoints { get; init; } = [];
}

public sealed class PlanningRandomSnapshot
{
    public required string RunSeedString { get; init; }

    public required uint RunSeed { get; init; }

    public required uint PlayerRewardSeed { get; init; }

    public required int UnknownMapCounter { get; init; }

    public required int RewardCounter { get; init; }

    public required int ShopCounter { get; init; }

    public required int TransformationCounter { get; init; }

    public required int NumberOfRuns { get; init; }

    public required int SeenUnknownMapPoints { get; init; }

    public required SimulatedUnknownMapOdds UnknownMapOdds { get; init; }
}

public sealed class PlanningSnapshotBuilder : IPlanningSnapshotBuilder
{
    public PlanningSnapshot Build()
    {
        RunState? runState = RunManager.Instance?.DebugOnlyGetState();
        Player? player = LocalContext.GetMe(runState);
        if (runState == null || player == null)
        {
            throw new System.InvalidOperationException("Planner snapshot requested outside of an active local run.");
        }

        CombatState? combatState = player.Creature.CombatState;
        IReadOnlyList<MapPoint> nextMapPoints = GetNextMapPoints(runState);

        return new PlanningSnapshot
        {
            RunState = runState,
            Player = player,
            DeckMetrics = DeckMetrics.From(player),
            Random = new PlanningRandomSnapshot
            {
                RunSeedString = runState.Rng.StringSeed,
                RunSeed = runState.Rng.Seed,
                PlayerRewardSeed = player.PlayerRng.Seed,
                UnknownMapCounter = runState.Rng.UnknownMapPoint.Counter,
                RewardCounter = player.PlayerRng.Rewards.Counter,
                ShopCounter = player.PlayerRng.Shops.Counter,
                TransformationCounter = player.PlayerRng.Transformations.Counter,
                NumberOfRuns = runState.UnlockState.NumberOfRuns,
                SeenUnknownMapPoints = runState.MapPointHistory.SelectMany(static entry => entry).Count(static entry => entry.MapPointType == MapPointType.Unknown),
                UnknownMapOdds = SimulatedUnknownMapOdds.From(runState.Odds.UnknownMapPoint)
            },
            CombatState = combatState,
            Hand = player.PlayerCombatState?.Hand.Cards.ToList() ?? [],
            DrawPile = player.PlayerCombatState?.DrawPile.Cards.ToList() ?? [],
            DiscardPile = player.PlayerCombatState?.DiscardPile.Cards.ToList() ?? [],
            Deck = player.Deck.Cards.ToList(),
            Potions = player.Potions.ToList(),
            Enemies = combatState?.Enemies.Where(static creature => creature.IsAlive).ToList() ?? [],
            Allies = combatState?.PlayerCreatures.Where(static creature => creature.IsAlive).ToList() ?? [],
            NextMapPoints = nextMapPoints
        };
    }

    private static IReadOnlyList<MapPoint> GetNextMapPoints(RunState runState)
    {
        if (runState.CurrentMapPoint != null)
        {
            return runState.CurrentMapPoint.Children.ToList();
        }

        if (runState.Map.StartingMapPoint != null)
        {
            return [runState.Map.StartingMapPoint];
        }

        return [];
    }
}

public sealed class SimulatedUnknownMapOdds
{
    private const float BaseMonsterOdds = 0.1f;
    private const float BaseEliteOdds = -1f;
    private const float BaseTreasureOdds = 0.02f;
    private const float BaseShopOdds = 0.03f;

    public float MonsterOdds { get; set; }

    public float EliteOdds { get; set; }

    public float TreasureOdds { get; set; }

    public float ShopOdds { get; set; }

    public float EventOdds => System.Math.Max(0f, 1f - PositiveNonEventOdds.Sum());

    private IEnumerable<float> PositiveNonEventOdds => new[] { MonsterOdds, EliteOdds, TreasureOdds, ShopOdds }.Where(static odds => odds > 0f);

    public static SimulatedUnknownMapOdds From(MegaCrit.Sts2.Core.Odds.UnknownMapPointOdds odds)
    {
        return new SimulatedUnknownMapOdds
        {
            MonsterOdds = odds.MonsterOdds,
            EliteOdds = odds.EliteOdds,
            TreasureOdds = odds.TreasureOdds,
            ShopOdds = odds.ShopOdds
        };
    }

    public SimulatedUnknownMapOdds Clone()
    {
        return new SimulatedUnknownMapOdds
        {
            MonsterOdds = MonsterOdds,
            EliteOdds = EliteOdds,
            TreasureOdds = TreasureOdds,
            ShopOdds = ShopOdds
        };
    }

    public float GetOdds(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.Monster => MonsterOdds,
            RoomType.Elite => EliteOdds,
            RoomType.Treasure => TreasureOdds,
            RoomType.Shop => ShopOdds,
            RoomType.Event => EventOdds,
            _ => 0f
        };
    }

    public void Advance(RoomType selectedRoomType, IReadOnlyCollection<RoomType> allowedRoomTypes)
    {
        foreach (RoomType roomType in new[] { RoomType.Monster, RoomType.Elite, RoomType.Treasure, RoomType.Shop })
        {
            if (selectedRoomType == roomType)
            {
                SetOdds(roomType, GetBaseOdds(roomType));
            }
            else if (allowedRoomTypes.Contains(roomType))
            {
                SetOdds(roomType, GetOdds(roomType) + GetBaseOdds(roomType));
            }
        }
    }

    private void SetOdds(RoomType roomType, float value)
    {
        switch (roomType)
        {
            case RoomType.Monster:
                MonsterOdds = value;
                break;
            case RoomType.Elite:
                EliteOdds = value;
                break;
            case RoomType.Treasure:
                TreasureOdds = value;
                break;
            case RoomType.Shop:
                ShopOdds = value;
                break;
        }
    }

    private static float GetBaseOdds(RoomType roomType)
    {
        return roomType switch
        {
            RoomType.Monster => BaseMonsterOdds,
            RoomType.Elite => BaseEliteOdds,
            RoomType.Treasure => BaseTreasureOdds,
            RoomType.Shop => BaseShopOdds,
            _ => 0f
        };
    }
}
