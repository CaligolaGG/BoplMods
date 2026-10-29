using BoplFixedMath;
using HarmonyLib;

namespace CustomRules.Rules;

/// <summary>
/// Invisibility is state on the Player (isInvisible + timers), expired by Player.UpdatePlayer after 5 s and drawn
/// by fading the sprites with timeSinceEnteredAbility (fully faded after 1 s; using an ability resets it, which
/// makes the slime flash visible). Keep every player invisible and fully faded.
/// </summary>
[HarmonyPatch(typeof(Player), nameof(Player.UpdatePlayer))]
internal static class InvisibilityRule
{
    private static void Postfix(Player __instance)
    {
        if (!BasicRules.PermanentInvisibility.ActiveBool)
            return;

        __instance.isInvisible = true;
        __instance.timeSpentInvisible = Fix.Zero;
        if (__instance.timeSinceEnteredAbility < Fix.One)
            __instance.timeSinceEnteredAbility = Fix.One;
    }
}
