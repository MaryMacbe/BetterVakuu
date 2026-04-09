using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace BetterVakuu.Scripts.Planning;

public static class PlannerRuntime
{
    private static readonly object Sync = new object();
    private static readonly HashSet<ulong> RewardScreensInFlight = [];
    private static readonly HashSet<ulong> CardRewardScreensInFlight = [];
    private static readonly HashSet<ulong> MapScreensInFlight = [];
    private static readonly HashSet<ulong> EventLayoutsInFlight = [];
    private static readonly IPlanningSnapshotBuilder SnapshotBuilder = new PlanningSnapshotBuilder();

    private static IDisposable? _selectorScope;

    public static bool IsEnabled { get; private set; }

    public static IPlanningPolicy Policy { get; set; } = new HeuristicPlanningPolicy();

    public static bool TryHandleGlobalInput(InputEvent input)
    {
        if (!IsTogglePressed(input))
        {
            return false;
        }

        SetEnabled(!IsEnabled);
        return true;
    }

    public static PlanningSnapshot BuildSnapshot()
    {
        return SnapshotBuilder.Build();
    }

    public static Player? GetLocalPlayer()
    {
        return LocalContext.GetMe(MegaCrit.Sts2.Core.Runs.RunManager.Instance?.DebugOnlyGetState());
    }

    public static bool TryPlanCombatAction(Player player, out CardModel card, out MegaCrit.Sts2.Core.Entities.Creatures.Creature? target)
    {
        card = null!;
        target = null;

        if (!IsEnabled)
        {
            return false;
        }

        PlanningSnapshot snapshot = BuildSnapshot();
        CombatPlan? plan = Policy.PlanCombat(snapshot);
        if (plan == null)
        {
            return false;
        }

        card = plan.Card;
        target = plan.Target;
        return true;
    }

    public static bool TryPlanPotionUse(Player player, out PotionModel potion, out MegaCrit.Sts2.Core.Entities.Creatures.Creature? target)
    {
        potion = null!;
        target = null;

        if (!IsEnabled)
        {
            return false;
        }

        PlanningSnapshot snapshot = BuildSnapshot();
        PotionPlan? plan = Policy.PlanPotion(snapshot);
        if (plan == null)
        {
            return false;
        }

        potion = plan.Potion;
        target = plan.Target;
        return true;
    }

    public static void HandleRewardsScreen(NRewardsScreen screen)
    {
        if (!IsEnabled || !TryRegister(RewardScreensInFlight, screen))
        {
            return;
        }

        TaskHelper.RunSafely(HandleRewardsScreenAsync(screen));
    }

    public static void HandleCardRewardScreen(NCardRewardSelectionScreen screen)
    {
        if (!IsEnabled || !TryRegister(CardRewardScreensInFlight, screen))
        {
            return;
        }

        TaskHelper.RunSafely(HandleCardRewardScreenAsync(screen));
    }

    public static void HandleMapScreen(NMapScreen screen)
    {
        if (!IsEnabled || !TryRegister(MapScreensInFlight, screen))
        {
            return;
        }

        TaskHelper.RunSafely(HandleMapScreenAsync(screen));
    }

    public static void HandleEventLayout(NEventLayout layout)
    {
        if (!IsEnabled || !TryRegister(EventLayoutsInFlight, layout))
        {
            return;
        }

        TaskHelper.RunSafely(HandleEventLayoutAsync(layout));
    }

    private static void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
        {
            return;
        }

        IsEnabled = enabled;
        UpdateSelectorScope();
        Log.Info($"Planner {(enabled ? "enabled" : "disabled")}.");
    }

    private static void UpdateSelectorScope()
    {
        if (IsEnabled)
        {
            _selectorScope ??= CardSelectCmd.PushSelector(new PlanningCardSelector());
            return;
        }

        _selectorScope?.Dispose();
        _selectorScope = null;
    }

    private static bool IsTogglePressed(InputEvent input)
    {
        return input is InputEventKey
        {
            Pressed: true,
            Echo: false,
            Keycode: PlannerConfig.ToggleKey
        };
    }

    private static async Task HandleRewardsScreenAsync(NRewardsScreen screen)
    {
        try
        {
            await Cmd.Wait(PlannerConfig.RewardScreenStartDelaySeconds);

            while (IsEnabled && GodotObject.IsInstanceValid(screen))
            {
                if (!ReferenceEquals(NOverlayStack.Instance?.Peek(), screen))
                {
                    await Cmd.Wait(PlannerConfig.UiPollIntervalSeconds);
                    continue;
                }

                List<NRewardButton> rewardButtons = UiHelper.FindAll<NRewardButton>(screen)
                    .Where(static button => GodotObject.IsInstanceValid(button) && button.Reward != null && button.IsEnabled)
                    .ToList();

                if (rewardButtons.Count == 0)
                {
                    NProceedButton? proceedButton = screen.GetNodeOrNull<NProceedButton>("ProceedButton");
                    if (proceedButton?.IsEnabled ?? false)
                    {
                        await UiHelper.Click(proceedButton, PlannerConfig.UiClickDelayMs);
                    }

                    break;
                }

                PlanningSnapshot snapshot = BuildSnapshot();
                RewardPlan? plan = Policy.PlanReward(snapshot, rewardButtons.Select(static button => button.Reward!).ToList());
                if (plan?.Reward == null)
                {
                    NProceedButton? proceedButton = screen.GetNodeOrNull<NProceedButton>("ProceedButton");
                    if (proceedButton?.IsEnabled ?? false)
                    {
                        await UiHelper.Click(proceedButton, PlannerConfig.UiClickDelayMs);
                    }

                    break;
                }

                NRewardButton? rewardButton = rewardButtons.FirstOrDefault(button => ReferenceEquals(button.Reward, plan.Reward));
                if (rewardButton == null)
                {
                    break;
                }

                await UiHelper.Click(rewardButton, PlannerConfig.UiClickDelayMs);
                await Cmd.Wait(PlannerConfig.UiPollIntervalSeconds);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Planner reward automation failed: {ex}");
        }
        finally
        {
            Unregister(RewardScreensInFlight, screen);
        }
    }

    private static async Task HandleCardRewardScreenAsync(NCardRewardSelectionScreen screen)
    {
        try
        {
            await Cmd.Wait(PlannerConfig.CardRewardStartDelaySeconds);
            if (!IsEnabled || !GodotObject.IsInstanceValid(screen) || !ReferenceEquals(NOverlayStack.Instance?.Peek(), screen))
            {
                return;
            }

            List<NGridCardHolder> holders = UiHelper.FindAll<NGridCardHolder>(screen)
                .Where(static holder => GodotObject.IsInstanceValid(holder))
                .ToList();

            if (holders.Count == 0)
            {
                return;
            }

            PlanningSnapshot snapshot = BuildSnapshot();
            CardChoicePlan plan = Policy.PlanCardReward(snapshot, holders.Select(static holder => holder.CardModel!).ToList());
            if (plan.Card == null)
            {
                return;
            }

            NGridCardHolder? chosenHolder = holders.FirstOrDefault(holder => ReferenceEquals(holder.CardModel, plan.Card));
            if (chosenHolder != null)
            {
                chosenHolder.EmitSignal(NCardHolder.SignalName.Pressed, chosenHolder);
                await Cmd.Wait(PlannerConfig.UiPollIntervalSeconds);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Planner card reward automation failed: {ex}");
        }
        finally
        {
            Unregister(CardRewardScreensInFlight, screen);
        }
    }

    private static async Task HandleMapScreenAsync(NMapScreen screen)
    {
        try
        {
            await Cmd.Wait(PlannerConfig.MapScreenStartDelaySeconds);

            while (IsEnabled && GodotObject.IsInstanceValid(screen))
            {
                if (CombatManager.Instance.IsInProgress || (NCombatRoom.Instance != null && ActiveScreenContext.Instance.IsCurrent(NCombatRoom.Instance)))
                {
                    if (screen.IsOpen)
                    {
                        screen.Close(animateOut: false);
                    }

                    break;
                }

                if (!screen.IsOpen || !screen.IsTravelEnabled || screen.IsTraveling)
                {
                    await Cmd.Wait(PlannerConfig.UiPollIntervalSeconds);
                    continue;
                }

                PlanningSnapshot snapshot = BuildSnapshot();
                if (snapshot.NextMapPoints.Count == 0)
                {
                    break;
                }

                MapPlan? plan = Policy.PlanMap(snapshot, snapshot.NextMapPoints);
                if (plan == null)
                {
                    break;
                }

                NMapPoint? mapPoint = UiHelper.FindAll<NMapPoint>(screen)
                    .FirstOrDefault(point =>
                        GodotObject.IsInstanceValid(point) &&
                        point.IsEnabled &&
                        point.Point.coord.Equals(plan.Point.coord));

                if (mapPoint != null)
                {
                    await UiHelper.Click(mapPoint, PlannerConfig.UiClickDelayMs);
                }

                break;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Planner map automation failed: {ex}");
        }
        finally
        {
            Unregister(MapScreensInFlight, screen);
        }
    }

    private static async Task HandleEventLayoutAsync(NEventLayout layout)
    {
        try
        {
            await Cmd.Wait(PlannerConfig.EventScreenStartDelaySeconds);

            while (IsEnabled && GodotObject.IsInstanceValid(layout))
            {
                NEventRoom? eventRoom = NEventRoom.Instance;
                if (eventRoom == null || !GodotObject.IsInstanceValid(eventRoom) || !ReferenceEquals(eventRoom.Layout, layout))
                {
                    break;
                }

                if (!ActiveScreenContext.Instance.IsCurrent(eventRoom))
                {
                    if (!layout.IsVisibleInTree())
                    {
                        break;
                    }

                    await Cmd.Wait(PlannerConfig.UiPollIntervalSeconds);
                    continue;
                }

                List<NEventOptionButton> buttons = layout.OptionButtons
                    .Where(static button => GodotObject.IsInstanceValid(button) && !button.Option.IsLocked)
                    .ToList();

                if (buttons.Count == 0)
                {
                    await Cmd.Wait(PlannerConfig.UiPollIntervalSeconds);
                    continue;
                }

                PlanningSnapshot snapshot = BuildSnapshot();
                EventPlan? plan = Policy.PlanEvent(snapshot, buttons.Select(static button => button.Option).ToList());
                if (plan == null)
                {
                    break;
                }

                NEventOptionButton? chosenButton = buttons.FirstOrDefault(button => ReferenceEquals(button.Option, plan.Option));
                if (chosenButton == null)
                {
                    await Cmd.Wait(PlannerConfig.UiPollIntervalSeconds);
                    continue;
                }

                await UiHelper.Click(chosenButton, PlannerConfig.UiClickDelayMs);
                await Cmd.Wait(PlannerConfig.EventPostChoiceDelaySeconds);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Planner event automation failed: {ex}");
        }
        finally
        {
            Unregister(EventLayoutsInFlight, layout);
        }
    }

    private static bool TryRegister(HashSet<ulong> registry, Node node)
    {
        lock (Sync)
        {
            return registry.Add(node.GetInstanceId());
        }
    }

    private static void Unregister(HashSet<ulong> registry, Node node)
    {
        lock (Sync)
        {
            registry.Remove(node.GetInstanceId());
        }
    }
}
