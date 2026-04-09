using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BetterVakuu.Scripts.Planning;

public static class CombatTargeting
{
    public static bool TryResolveTarget(CardModel card, out Creature? target)
    {
        target = null;

        CombatState? combatState = card.CombatState;
        if (combatState == null)
        {
            return false;
        }

        switch (card.TargetType)
        {
            case TargetType.AnyEnemy:
                foreach (Creature enemy in GetEnemyTargets(combatState))
                {
                    if (card.CanPlayTargeting(enemy))
                    {
                        target = enemy;
                        return true;
                    }
                }

                return false;

            case TargetType.AnyAlly:
                foreach (Creature ally in GetAllyTargets(card.Owner, combatState))
                {
                    if (card.CanPlayTargeting(ally))
                    {
                        target = ally;
                        return true;
                    }
                }

                return false;

            default:
                return card.CanPlayTargeting(null);
        }
    }

    public static IEnumerable<Creature> GetEnemyTargets(CombatState combatState)
    {
        return combatState.HittableEnemies
            .Where(static creature => creature.IsAlive)
            .Distinct()
            .OrderBy(static creature => creature.CurrentHp)
            .ThenBy(static creature => creature.MaxHp);
    }

    public static IEnumerable<Creature> GetAllyTargets(Player owner, CombatState combatState)
    {
        List<Creature> creatures = new List<Creature>();
        if (owner.Creature.IsAlive)
        {
            creatures.Add(owner.Creature);
        }

        creatures.AddRange(combatState.Creatures.Where(creature =>
            creature.IsAlive &&
            creature.Side == owner.Creature.Side &&
            creature != owner.Creature));

        return creatures
            .Distinct()
            .OrderBy(static creature => creature.CurrentHp)
            .ThenBy(static creature => creature.MaxHp);
    }
}
