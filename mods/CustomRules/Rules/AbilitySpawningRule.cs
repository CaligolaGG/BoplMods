using BoplFixedMath;
using HarmonyLib;

namespace CustomRules.Rules;

/// <summary>
/// Ability pickups come from two places: the level's AbilitySpawner (one every 40 s, up to 3 per round) and
/// SlimeController.DropAbilities (a player's last slime drops one of its abilities when it dies).
/// "Ability spawning time ×" scales the spawner's 40 s by slowing down (or speeding up) its clock: its UpdateSim only
/// uses the delta time to advance that clock. "Ability cap per round" replaces its MaxSpawns (3), set before every tick
/// (the spawner turns itself off once it reached the cap).
/// </summary>
[HarmonyPatch(typeof(AbilitySpawner), nameof(AbilitySpawner.UpdateSim))]
internal static class AbilitySpawningRule
{
    private static bool Prefix(AbilitySpawner __instance, ref Fix simDeltaTime)
    {
        if (BasicRules.DisableAbilitySpawning.ActiveBool)
            return false;
        __instance.MaxSpawns = BasicRules.AbilitySpawnCap.ActiveInt;
        Fix multiplier = BasicRules.AbilitySpawnTime.ActiveFix;
        if (multiplier > Fix.Zero && multiplier != Fix.One)
            simDeltaTime /= multiplier;
        return true;
    }
}

[HarmonyPatch(typeof(SlimeController), nameof(SlimeController.DropAbilities))]
internal static class AbilityDropOnDeathRule
{
    private static bool Prefix() => !BasicRules.DisableAbilitySpawning.ActiveBool;
}
