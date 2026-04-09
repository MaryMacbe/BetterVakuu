using System;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Models;

namespace ModTest.Scripts.Planning;

public sealed class HeuristicPotionScorer
{
    public float ScorePotion(PlanningSnapshot snapshot, PotionModel potion, Creature? target)
    {
        float score = 1f;
        float playerHealthRatio = (float)snapshot.Player.Creature.CurrentHp / Math.Max(1, snapshot.Player.Creature.MaxHp);

        score += potion.Rarity switch
        {
            PotionRarity.Common => 1f,
            PotionRarity.Uncommon => 0.35f,
            PotionRarity.Rare => -0.35f,
            _ => 0f
        };

        switch (potion.TargetType)
        {
            case TargetType.AnyEnemy:
                score += 2.25f;
                if (target != null && target.CurrentHp <= 20)
                {
                    score += 1.25f;
                }
                break;

            case TargetType.AllEnemies:
            case TargetType.RandomEnemy:
                score += snapshot.Enemies.Count > 1 ? 3.25f : 1.5f;
                break;

            case TargetType.Self:
            case TargetType.AnyAlly:
            case TargetType.AnyPlayer:
            case TargetType.AllAllies:
                score += playerHealthRatio <= 0.5f ? 3f : -1f;
                break;

            case TargetType.None:
                score += 1.25f;
                break;
        }

        if (snapshot.Player.PlayerCombatState?.Energy <= 0)
        {
            score += 0.75f;
        }

        return score;
    }
}
