using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace CaliSkills.Core;

/// <summary>Right-click a skill in the character select ability grid to open its settings editor.</summary>
[HarmonyPatch(typeof(AbilityGrid), nameof(AbilityGrid.Update))]
internal static class OpenSkillSettingsOnRightClickPatch
{
    private static void Postfix(AbilityGrid __instance)
    {
        if (SkillSettingsEditor.BlocksMenuInput || !__instance.usesKeyboardAndMouse)
            return;
        if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame)
            return;

        // Hovering an entry selects it (AbilityGridEntry.Select), so the selection is the entry under the mouse.
        int index = __instance.SelectedIcon + 1;
        if (index < 0 || index >= __instance.abilityIcons.sprites.Count)
            return;

        Skill skill = SkillRegistry.Find(__instance.abilityIcons.sprites[index].name);
        if (skill?.SettingsType != null)
            SkillSettingsEditor.Open(skill);
    }
}

/// <summary>
/// While the editor is open, typing in it must not drive the menu (W/S/A/D navigate, Enter/Space confirm,
/// Esc goes back...). These character select scripts read the keyboard directly: skip their updates.
/// </summary>
[HarmonyPatch]
internal static class BlockMenuInputWhileEditingPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new (System.Type Type, string Method)[]
        {
            (typeof(AbilityGrid), "Update"),
            (typeof(CharacterSelectBox), "Update"),
            (typeof(CharacterSelectBox), "LateUpdate"),
            (typeof(CharacterSelectHandler), "Update"),
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

    private static bool Prefix() => !SkillSettingsEditor.BlocksMenuInput;
}
