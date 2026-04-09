using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace ModTest.Scripts.Planning;

public sealed class HeuristicCardScorer
{
    public float ScoreCombatCard(PlanningSnapshot snapshot, CardModel card, Creature? target)
    {
        if (!card.CanPlayTargeting(target))
        {
            return float.NegativeInfinity;
        }

        float score = 10f;
        int energyToSpend = card.EnergyCost.GetAmountToSpend();
        int currentEnergy = snapshot.Player.PlayerCombatState?.Energy ?? 0;

        score += ScoreKeepValue(snapshot, card);
        score -= energyToSpend * 0.8f;

        if (card.EnergyCost.CostsX)
        {
            score += Math.Max(0, currentEnergy - 1) * 0.9f;
        }

        if (energyToSpend == 0)
        {
            score += 1.5f;
        }

        if (target != null && target.CurrentHp <= 15)
        {
            score += 0.5f;
        }

        if (snapshot.Player.Creature.CurrentHp * 2 <= snapshot.Player.Creature.MaxHp && card.Type == CardType.Skill)
        {
            score += 1.5f;
        }

        return score;
    }

    public float ScoreRewardCard(PlanningSnapshot snapshot, CardModel card)
    {
        float score = ScoreKeepValue(snapshot, card);
        score += card.Rarity switch
        {
            CardRarity.Rare => 3.5f,
            CardRarity.Uncommon => 1.5f,
            _ => 0f
        };

        if (snapshot.DeckMetrics.AttackCount < snapshot.DeckMetrics.SkillCount && card.Type == CardType.Attack)
        {
            score += 1.25f;
        }

        if (snapshot.DeckMetrics.SkillCount < snapshot.DeckMetrics.AttackCount && card.Type == CardType.Skill)
        {
            score += 1.25f;
        }

        if (snapshot.DeckMetrics.PowerCount < 2 && card.Type == CardType.Power)
        {
            score += 1.5f;
        }

        return score;
    }

    public float ScoreRemovalCandidate(PlanningSnapshot snapshot, CardModel card)
    {
        float score = -ScoreKeepValue(snapshot, card);

        if (card.Type == CardType.Curse || card.Type == CardType.Status)
        {
            score += 50f;
        }

        if (snapshot.DeckMetrics.GetCopiesOf(card) > 1)
        {
            score += 2.5f;
        }

        if (card.Rarity == CardRarity.Common)
        {
            score += 0.5f;
        }

        return score;
    }

    private static float ScoreKeepValue(PlanningSnapshot snapshot, CardModel card)
    {
        if (card.Type == CardType.Curse)
        {
            return -40f;
        }

        if (card.Type == CardType.Status)
        {
            return -30f;
        }

        float score = 0f;

        score += card.Type switch
        {
            CardType.Power => 4.5f,
            CardType.Attack => 2.5f,
            CardType.Skill => 2.25f,
            _ => 0f
        };

        score += card.Rarity switch
        {
            CardRarity.Rare => 3f,
            CardRarity.Uncommon => 1.5f,
            _ => 0f
        };

        if (card.IsUpgraded)
        {
            score += 1.25f;
        }

        if (card.EnergyCost.CostsX)
        {
            score += 1f;
        }
        else
        {
            int energy = card.EnergyCost.GetAmountToSpend();
            if (energy == 0)
            {
                score += 1.5f;
            }
            else if (energy >= 3)
            {
                score -= 0.75f;
            }
        }

        if (snapshot.DeckMetrics.GetCopiesOf(card) > 1)
        {
            score -= 1.25f;
        }

        return score;
    }
}
