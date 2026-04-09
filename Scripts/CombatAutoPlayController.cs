using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using ModTest.Scripts.Planning;

namespace ModTest.Scripts;

public static class CombatAutoPlayController
{
    private const Key ShortcutKey = Key.F8;
    private const float PollIntervalSeconds = 0.05f;
    private const int MaxResolvePollCount = 120;

    private static bool _isRunning;

    public static bool TryStart(InputEvent input)
    {
        if (!IsShortcutPressed(input))
        {
            return false;
        }

        if (_isRunning || !CanAutoPlay(out _))
        {
            return true;
        }

        TaskHelper.RunSafely(RunAutoPlayAsync());
        return true;
    }

    private static bool IsShortcutPressed(InputEvent input)
    {
        return input is InputEventKey
        {
            Pressed: true,
            Echo: false,
            Keycode: ShortcutKey
        };
    }

    private static async Task RunAutoPlayAsync()
    {
        _isRunning = true;

        try
        {
            while (CanAutoPlay(out Player? maybePlayer))
            {
                Player player = maybePlayer!;
                if (player.PlayerCombatState == null || player.PlayerCombatState.Energy <= 0)
                {
                    break;
                }

                if (!TryGetNextCard(player, out CardModel card, out Creature? target))
                {
                    break;
                }

                if (!card.TryManualPlay(target))
                {
                    break;
                }

                bool resolved = await WaitForCardToLeaveHand(player, card);
                if (!resolved)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Auto play failed: {ex}");
        }
        finally
        {
            _isRunning = false;
        }
    }

    private static bool CanAutoPlay(out Player? player)
    {
        player = null;

        if (NCombatRoom.Instance == null ||
            !ActiveScreenContext.Instance.IsCurrent(NCombatRoom.Instance) ||
            CombatManager.Instance.IsOverOrEnding ||
            CombatManager.Instance.PlayerActionsDisabled ||
            !CombatManager.Instance.IsPlayPhase)
        {
            return false;
        }

        CombatState? combatState = CombatManager.Instance.DebugOnlyGetState();
        if (combatState == null)
        {
            return false;
        }

        if (combatState.CurrentSide != CombatSide.Player)
        {
            return false;
        }

        player = LocalContext.GetMe(combatState);
        if (player?.PlayerCombatState == null || !player.Creature.IsAlive)
        {
            return false;
        }

        if (CombatManager.Instance.PlayersTakingExtraTurn.Count > 0 &&
            !CombatManager.Instance.PlayersTakingExtraTurn.Contains(player))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetNextCard(Player player, out CardModel card, out Creature? target)
    {
        card = null!;
        target = null;

        if (PlannerRuntime.TryPlanCombatAction(player, out CardModel? plannedCard, out Creature? plannedTarget))
        {
            card = plannedCard;
            target = plannedTarget;
            return true;
        }

        List<CardModel> handCards = player.PlayerCombatState?.Hand.Cards.ToList() ?? new List<CardModel>();
        foreach (CardModel handCard in handCards)
        {
            if (!CombatTargeting.TryResolveTarget(handCard, out Creature? resolvedTarget))
            {
                continue;
            }

            card = handCard;
            target = resolvedTarget;
            return true;
        }

        return false;
    }

    private static async Task<bool> WaitForCardToLeaveHand(Player player, CardModel card)
    {
        for (int i = 0; i < MaxResolvePollCount; i++)
        {
            if (!CanAutoPlay(out Player? currentPlayer) || currentPlayer != player || player.PlayerCombatState == null)
            {
                return false;
            }

            if (card.Pile?.Type != PileType.Hand || !player.PlayerCombatState.Hand.Cards.Contains(card))
            {
                return true;
            }

            await Cmd.Wait(PollIntervalSeconds);
        }

        Log.Warn($"Auto play timed out waiting for {card.Id.Entry} to resolve.");
        return false;
    }
}
