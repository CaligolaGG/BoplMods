using System.Collections.Generic;
using System.Linq;
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

    private static readonly HashSet<MethodBase> Patched = new();
    private static bool _moddedAbilitiesPatched;

    /// <summary>Every vanilla ability is an Ability or an InstantAbility.</summary>
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Ability), nameof(Ability.GetCooldown));
        yield return AccessTools.Method(typeof(InstantAbility), nameof(InstantAbility.GetCooldown));
    }

    /// <summary>
    /// Abilities from other mods that derive from AbilityMonoBehaviour directly (e.g. CaliSkills' Run, SpawnBot).
    /// Their DLLs may load after this plugin, so this runs when character select opens (everything is loaded by then).
    /// </summary>
    internal static void PatchModdedAbilities(Harmony harmony)
    {
        if (_moddedAbilitiesPatched)
            return;
        _moddedAbilitiesPatched = true;

        var postfix = new HarmonyMethod(AccessTools.Method(typeof(CooldownRule), nameof(Postfix)));
        foreach (MethodBase method in AccessTools.AllTypes()
                     .Where(t => typeof(AbilityMonoBehaviour).IsAssignableFrom(t) && !t.IsAbstract &&
                                 t != typeof(Ability) && t != typeof(InstantAbility))
                     .Select(t => AccessTools.DeclaredMethod(t, nameof(AbilityMonoBehaviour.GetCooldown)))
                     .Where(m => m != null && !m.IsAbstract))
        {
            if (!Patched.Add(method))
                continue;
            harmony.Patch(method, postfix: postfix);
            Plugin.Log.LogInfo($"Cooldown rule also covers {method.DeclaringType?.FullName}.");
        }
    }

    private static void Postfix(ref Fix __result)
    {
        if (__result != InvisibilitySentinel)
            __result *= BasicRules.CooldownMultiplier.ActiveFix;
    }
}
