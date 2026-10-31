using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace CustomRules.UI;

/// <summary>
/// Creates the rules menu the first time a character select screen opens, extends the cooldown rule to modded
/// abilities (all mods are loaded by then) and sets up the vanilla ability stats' spawn tracking (needs the ability
/// list).
/// </summary>
[HarmonyPatch]
internal static class CreateRulesMenuPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(CharacterSelectHandler), "Awake");
        yield return AccessTools.Method(typeof(CharacterSelectHandler_online), "Awake");
    }

    private static void Postfix()
    {
        // Each step on its own: an exception here would abort the lobby's Awake (no Start button, no way back).
        Run(RulesMenu.EnsureCreated, "the rules menu");
        Run(() => Rules.CooldownRule.PatchModdedAbilities(Plugin.Harmony), "the cooldown rule for modded abilities");
        Run(() => Rules.VanillaAbilityStats.PatchSpawnScopes(Plugin.Harmony), "the vanilla ability stats");
    }

    private static void Run(System.Action step, string what)
    {
        try
        {
            step();
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogError($"Setting up {what} failed: {e}");
        }
    }
}

/// <summary>
/// While the rules window is open, typing in it must not drive the menu (W/S/A/D navigate, Enter/Space confirm,
/// Esc leaves the lobby...). These character select scripts read the keyboard directly: skip their updates.
/// </summary>
[HarmonyPatch]
internal static class BlockMenuInputWhileOpenPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new (System.Type Type, string Method)[]
        {
            (typeof(AbilityGrid), "Update"),
            (typeof(CharacterSelectBox), "Update"),
            (typeof(CharacterSelectBox), "LateUpdate"),
            (typeof(CharacterSelectHandler), "Update"),
            (typeof(CharacterSelectHandler_online), "Update"),
            (typeof(CharSelectClickToJoin), "LateUpdate"),
            (typeof(SelectAbility), "Update"),
            (typeof(SelectColor), "Update"),
            (typeof(TeamSelector), "Update"),
            (typeof(ReadyButton), "Update"),
        };
        foreach (var (type, method) in targets)
        {
            MethodInfo m = AccessTools.DeclaredMethod(type, method);
            if (m != null)
                yield return m;
            else
                Plugin.Log.LogWarning($"Input blocker: {type.Name}.{method} not found (game update?).");
        }
    }

    private static bool Prefix() => !RulesMenu.BlocksMenuInput;
}
