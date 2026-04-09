using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace BetterVakuu.Scripts.Planning;

public static class PotionTargeting
{
    public static bool IsUsable(PotionModel potion, PlanningSnapshot snapshot)
    {
        if (potion.IsQueued || !potion.Owner.CanRemovePotions || !potion.Owner.Creature.IsAlive || !potion.PassesCustomUsabilityCheck)
        {
            return false;
        }

        return potion.Usage switch
        {
            PotionUsage.Automatic => false,
            PotionUsage.CombatOnly => snapshot.CombatState != null,
            PotionUsage.AnyTime => true,
            _ => false
        };
    }

    public static bool TryResolveTarget(PotionModel potion, PlanningSnapshot snapshot, out Creature? target)
    {
        target = null;

        if (!IsUsable(potion, snapshot))
        {
            return false;
        }

        CombatState? combatState = snapshot.CombatState;
        if (combatState == null)
        {
            return potion.TargetType is TargetType.None or TargetType.AllEnemies or TargetType.AllAllies;
        }

        switch (potion.TargetType)
        {
            case TargetType.Self:
                target = potion.Owner.Creature;
                return target.IsAlive;

            case TargetType.AnyEnemy:
                target = CombatTargeting.GetEnemyTargets(combatState).FirstOrDefault();
                return target != null;

            case TargetType.AnyAlly:
            case TargetType.AnyPlayer:
                target = CombatTargeting.GetAllyTargets(potion.Owner, combatState)
                    .OrderBy(static creature => (float)creature.CurrentHp / creature.MaxHp)
                    .ThenBy(static creature => creature.CurrentHp)
                    .FirstOrDefault();
                return target != null;

            case TargetType.None:
            case TargetType.AllEnemies:
            case TargetType.AllAllies:
            case TargetType.RandomEnemy:
                return true;

            case TargetType.TargetedNoCreature:
                return false;

            default:
                return !potion.TargetType.IsSingleTarget();
        }
    }
}
