using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace ModTest.Scripts;

[HarmonyPatch(typeof(NPlayerHand), nameof(NPlayerHand._UnhandledInput))]
public static class CombatAutoPlayPatch
{
    [HarmonyPostfix]
    private static void OnUnhandledInput(NPlayerHand __instance, InputEvent input)
    {
        if (!CombatAutoPlayController.TryStart(input))
        {
            return;
        }

        __instance.GetViewport()?.SetInputAsHandled();
    }
}
