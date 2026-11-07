using CaliGameModes.Modes.Football;
using CustomRules.Core;
using HarmonyLib;
using UnityEngine;

namespace CaliGameModes.Modes.Rotation;

/// <summary>
/// Rotation mode: every ability can be used a limited number of times and is then gone for that player, replaced by
/// another one (CustomRules' abilities max use, with Destroy and Reroll locked on). Nobody dies for good
/// (<see cref="Respawning"/>): a kill gives the killer a point. The round ends only when every player has used up
/// every ability; the highest score wins it. The match itself: <see cref="RotationMatch"/>.
/// </summary>
internal static class RotationMode
{
    internal static Rule Enabled;

    internal static void Register()
    {
        Enabled = RuleRegistry.AddBool("RotationMode", "Rotation mode", false,
            "Every ability you use up is gone, and you get a new one, until you've used them all. Nobody dies: a kill " +
            "gives a point and the slime comes back. The round ends when every player has used every ability; the " +
            "highest score wins. Locks the abilities max use rules on (set the uses per ability there, at least 1), Revival is banned.",
            section: Plugin.Section);

        RuleRegistry.MakeExclusive(Enabled, FootballMode.Enabled);
        RuleRegistry.AddLock(Enabled, "AbilityMaxUse", true);
        RuleRegistry.AddLock(Enabled, "AbilityMaxUse.Destroy", true);
        RuleRegistry.AddLock(Enabled, "AbilityMaxUse.Reroll", true);
        RuleRegistry.AddLock(Enabled, "InfiniteRound", true);
        // With 0 uses nothing is ever used up, so the round would never end.
        RuleRegistry.AddMinimum(Enabled, "AbilityMaxUse.Amount", 1);
        RuleRegistry.AddBannedAbility(Enabled, "Revival");
    }

    /// <summary>Every level, on every machine: start the match (a scene object, gone with the level).</summary>
    [HarmonyPatch(typeof(GameSessionHandler), nameof(GameSessionHandler.SpawnPlayers))]
    private static class StartOnLevelStart
    {
        private static void Postfix(GameSessionHandler __instance)
        {
            if (Enabled.IsOn)
                new GameObject("CaliGameModes.Rotation").AddComponent<RotationMatch>().Begin(__instance);
            else
                KnockTracker.Unpatch();
        }
    }
}
