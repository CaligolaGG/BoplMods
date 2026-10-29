using HarmonyLib;

namespace CustomRules.Rules;

/// <summary>
/// Ability pickups come from two places: the level's AbilitySpawner (one every 40 s, up to 3 per round) and
/// SlimeController.DropAbilities (a player's last slime drops one of its abilities when it dies).
/// </summary>
[HarmonyPatch(typeof(AbilitySpawner), nameof(AbilitySpawner.UpdateSim))]
internal static class AbilitySpawningRule
{
    private static bool Prefix() => !BasicRules.DisableAbilitySpawning.ActiveBool;
}

[HarmonyPatch(typeof(SlimeController), nameof(SlimeController.DropAbilities))]
internal static class AbilityDropOnDeathRule
{
    private static bool Prefix() => !BasicRules.DisableAbilitySpawning.ActiveBool;
}
