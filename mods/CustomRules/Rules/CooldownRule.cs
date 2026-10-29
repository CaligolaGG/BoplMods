using System.Collections.Generic;
using System.Reflection;
using BoplFixedMath;
using HarmonyLib;

namespace CustomRules.Rules;

/// <summary>
/// Every ability's cooldown goes through GetCooldown() (the ready check in SlimeController, the cooldown UI,
/// Teleport's IgnoreNextCooldown...), so scaling the getter covers them all without touching the stored values
/// (Quantum and the rope change theirs at runtime).
/// </summary>
[HarmonyPatch]
internal static class CooldownRule
{
    /// <summary>Invisibility parks its cooldown at this value while active, and SlimeController checks it exactly.</summary>
    private static readonly Fix InvisibilitySentinel = (Fix)10000L;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Ability), nameof(Ability.GetCooldown));
        yield return AccessTools.Method(typeof(InstantAbility), nameof(InstantAbility.GetCooldown));
    }

    private static void Postfix(ref Fix __result)
    {
        if (__result != InvisibilitySentinel)
            __result *= BasicRules.CooldownMultiplier.ActiveFix;
    }
}
