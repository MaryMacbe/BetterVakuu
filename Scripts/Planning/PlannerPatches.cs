using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace BetterVakuu.Scripts.Planning;

[HarmonyPatch(typeof(NHotkeyManager), nameof(NHotkeyManager._UnhandledInput))]
public static class PlannerHotkeyPatch
{
    [HarmonyPostfix]
    private static void OnUnhandledInput(NHotkeyManager __instance, InputEvent inputEvent)
    {
        if (!PlannerRuntime.TryHandleGlobalInput(inputEvent))
        {
            return;
        }

        __instance.GetViewport()?.SetInputAsHandled();
    }
}

[HarmonyPatch(typeof(NRewardsScreen), nameof(NRewardsScreen.SetRewards))]
public static class PlannerRewardsPatch
{
    [HarmonyPostfix]
    private static void OnRewardsSet(NRewardsScreen __instance)
    {
        PlannerRuntime.HandleRewardsScreen(__instance);
    }
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.AfterOverlayOpened))]
public static class PlannerCardRewardPatch
{
    [HarmonyPostfix]
    private static void OnCardRewardOpened(NCardRewardSelectionScreen __instance)
    {
        PlannerRuntime.HandleCardRewardScreen(__instance);
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
public static class PlannerMapPatch
{
    [HarmonyPostfix]
    private static void OnMapOpened(NMapScreen __result)
    {
        if (__result != null)
        {
            PlannerRuntime.HandleMapScreen(__result);
        }
    }
}

[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.AddOptions))]
public static class PlannerEventLayoutPatch
{
    [HarmonyPostfix]
    private static void OnEventOptionsAdded(NEventLayout __instance)
    {
        PlannerRuntime.HandleEventLayout(__instance);
    }
}
