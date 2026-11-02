using CaliGameModes.Maps;
using CaliSkills.Abilities.Pinball;
using CustomRules.Core;
using HarmonyLib;
using UnityEngine;

namespace CaliGameModes.Modes.Football;

/// <summary>
/// Football mode: two teams (left and right spawns), footballs spawned by the mode (the Pinball's ball with its own
/// stats, <see cref="FootballSettings"/>), goals under the map's goal platforms (<see cref="FootballMaps"/>), nobody
/// dies. The match itself: <see cref="FootballMatch"/>. Maps: only football maps are played while the mode is on
/// (<see cref="MapLoaderBridge"/>, needs the Map Loader mod).
/// </summary>
internal static class FootballMode
{
    internal const string StatsName = "Football";

    internal static Rule Enabled;
    internal static Rule FirstTo;
    internal static Rule TimeLimit;
    internal static Rule Balls;

    internal static void Register()
    {
        Enabled = RuleRegistry.AddBool("FootballMode", "Football mode", false,
            "Score in the other team's goal! Left spawns against right spawns, nobody dies (you come back in the middle). " +
            "Played on football maps only (needs the Map Loader mod). Turning it on also sets the usual rules for it.",
            section: Plugin.Section);
        FirstTo = RuleRegistry.AddInt("FootballMode.FirstTo", "First to", 5, 0, 99,
            "Goals a team needs to win the match (0 = no goal limit).", parent: Enabled);
        TimeLimit = RuleRegistry.AddInt("FootballMode.Time", "Time (s)", 300, 0, 3600,
            "Match length in seconds; then the team with more goals wins, a tie is a draw (0 = no time limit).", parent: Enabled);
        Balls = RuleRegistry.AddInt("FootballMode.Balls", "Number of balls", 1, 1, 10,
            "Balls put in play at the start and after each goal.", parent: Enabled);

        AbilityStats.Register(StatsName, typeof(FootballSettings));
        AbilityStats.AddMenuButton(Enabled, "Edit football", StatsName,
            "The football's stats: size, knockback, bounce... (separate from the Pinball ability's).");

        RuleRegistry.MakeExclusive(Enabled, PinballMode.Enabled);
        RuleRegistry.AddPreset(Enabled, "DeactivateCustomMaps", false);
        RuleRegistry.AddPreset(Enabled, "ExtraJump", true);
        RuleRegistry.AddPreset(Enabled, "ExtraJump.AirJumps", 1);
        RuleRegistry.AddPreset(Enabled, "InfiniteRound", true);
        RuleRegistry.AddPreset(Enabled, "PlayerBouncing", true);
        RuleRegistry.AddPreset(Enabled, "NoOffensiveAbilities", true);
        RuleRegistry.AddPreset(Enabled, "OnlyOffensiveAbilities", false);
    }

    /// <summary>Every level, on every machine: start the match (a scene object, gone with the level).</summary>
    [HarmonyPatch(typeof(GameSessionHandler), nameof(GameSessionHandler.SpawnPlayers))]
    private static class StartOnLevelStart
    {
        private static void Postfix(GameSessionHandler __instance)
        {
            if (Enabled.IsOn && PinballSkill.Instance != null)
                new GameObject("CaliGameModes.Football").AddComponent<FootballMatch>().Begin(__instance);
        }
    }

    /// <summary>In the lobby, the host (or local player) offers Map Loader only the maps for the chosen mode.</summary>
    [HarmonyPatch]
    private static class FilterMapsInLobby
    {
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CharacterSelectHandler), "Update");
            yield return AccessTools.Method(typeof(CharacterSelectHandler_online), "Update");
        }

        private static void Postfix()
        {
            if (!GameLobby.isOnlineGame || SteamManager.LocalPlayerIsLobbyOwner)
                MapLoaderBridge.ShowMapsFor(Enabled.LobbyBool);
        }
    }

    /// <summary>
    /// Nobody dies: a slime that would die comes back in the middle of the map instead, through the game's own
    /// "permanent revive" (as in the tutorial: revive animation at <see cref="Player.SpawnPosition"/>).
    /// </summary>
    [HarmonyPatch(typeof(PlayerCollision), nameof(PlayerCollision.killPlayer))]
    private static class NoDeaths
    {
        private static void Prefix(PlayerCollision __instance)
        {
            FootballMatch match = FootballMatch.Current;
            if (match == null)
                return;
            Player player = PlayerHandler.Get().GetPlayer(__instance.GetPlayerId());
            if (player == null || player.RespawnPositions.Count > 0 || player.ExtraLife != ReviveStatus.NoRevive)
                return;
            player.ExtraLife = ReviveStatus.permanentRevive;
            player.SpawnPosition = match.PlayerRespawn;
        }
    }

    /// <summary>Water kills directly (and destroys the slime): send slimes through the revive above instead.</summary>
    [HarmonyPatch(typeof(KillZone), nameof(KillZone.OnCollide))]
    private static class NoDrowning
    {
        private static bool Prefix(KillZone __instance, CollisionInformation collision)
        {
            if (FootballMatch.Current == null || collision.layer != LayerMask.NameToLayer("Player"))
                return true;
            PlayerCollision body = collision.pp.fixTrans != null ? collision.pp.fixTrans.GetComponent<PlayerCollision>() : null;
            if (body == null)
                return true;
            __instance.SpawnWaterSplash((Vector2)collision.collider.Pos());
            body.killPlayer(Constants.ENVIRONMENTAL_PLAYER_ID, spawnEffect: false, ignoreInvulnerability: true, CauseOfDeath.Accident);
            return false;
        }
    }
}
