using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace CustomRules.UI;

/// <summary>
/// Creates the rules menu the first time a character select screen opens, and extends the cooldown rule to modded
/// abilities (all mods are loaded by then).
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
        RulesMenu.EnsureCreated();
        Rules.CooldownRule.PatchModdedAbilities(Plugin.Harmony);
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
