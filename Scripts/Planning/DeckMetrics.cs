using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BetterVakuu.Scripts.Planning;

public sealed class DeckMetrics
{
    private readonly Dictionary<ModelId, int> _countsById;

    private DeckMetrics(
        int totalCards,
        int attackCount,
        int skillCount,
        int powerCount,
        int curseCount,
        int statusCount,
        int upgradedCount,
        int zeroCostCount,
        int oneCostCount,
        int highCostCount,
        int uncommonCount,
        int rareCount,
        float averageEnergyCost,
        Dictionary<ModelId, int> countsById)
    {
        TotalCards = totalCards;
        AttackCount = attackCount;
        SkillCount = skillCount;
        PowerCount = powerCount;
        CurseCount = curseCount;
        StatusCount = statusCount;
        UpgradedCount = upgradedCount;
        ZeroCostCount = zeroCostCount;
        OneCostCount = oneCostCount;
        HighCostCount = highCostCount;
        UncommonCount = uncommonCount;
        RareCount = rareCount;
        AverageEnergyCost = averageEnergyCost;
        _countsById = countsById;
    }

    public int TotalCards { get; }

    public int AttackCount { get; }

    public int SkillCount { get; }

    public int PowerCount { get; }

    public int CurseCount { get; }

    public int StatusCount { get; }

    public int UpgradedCount { get; }

    public int ZeroCostCount { get; }

    public int OneCostCount { get; }

    public int HighCostCount { get; }

    public int UncommonCount { get; }

    public int RareCount { get; }

    public float AverageEnergyCost { get; }

    public float UpgradeRatio => TotalCards > 0 ? (float)UpgradedCount / TotalCards : 0f;

    public int GetCopiesOf(CardModel card)
    {
        if (_countsById.TryGetValue(card.Id, out int count))
        {
            return count;
        }

        return 0;
    }

    public static DeckMetrics From(Player player)
    {
        List<CardModel> cards = player.Deck.Cards.ToList();
        Dictionary<ModelId, int> countsById = cards
            .GroupBy(static card => card.Id)
            .ToDictionary(static group => group.Key, static group => group.Count());

        float averageEnergyCost = cards.Count > 0
            ? cards.Average(static card => card.EnergyCost.CostsX ? 1.5f : card.EnergyCost.GetAmountToSpend())
            : 0f;

        return new DeckMetrics(
            cards.Count,
            cards.Count(static card => card.Type == CardType.Attack),
            cards.Count(static card => card.Type == CardType.Skill),
            cards.Count(static card => card.Type == CardType.Power),
            cards.Count(static card => card.Type == CardType.Curse),
            cards.Count(static card => card.Type == CardType.Status),
            cards.Count(static card => card.IsUpgraded),
            cards.Count(static card => !card.EnergyCost.CostsX && card.EnergyCost.GetAmountToSpend() == 0),
            cards.Count(static card => !card.EnergyCost.CostsX && card.EnergyCost.GetAmountToSpend() == 1),
            cards.Count(static card => card.EnergyCost.CostsX || card.EnergyCost.GetAmountToSpend() >= 2),
            cards.Count(static card => card.Rarity == CardRarity.Uncommon),
            cards.Count(static card => card.Rarity == CardRarity.Rare),
            averageEnergyCost,
            countsById);
    }
}
