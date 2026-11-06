using BoplFixedMath;
using CaliGameModes.Modes.Football;
using CaliGameModes.Modes.Rotation;
using HarmonyLib;
using UnityEngine;

namespace CaliGameModes.Modes;

/// <summary>A match of a game mode where nobody dies for good (Football, Rotation).</summary>
internal interface IRespawnMatch
{
    /// <summary>Where <paramref name="player"/>'s slime comes back.</summary>
    Vec2 RespawnPosition(Player player);

    /// <summary>A slime is about to die (and come back): <paramref name="killerId"/> is whoever the game blames.</summary>
    void OnKilled(Player victim, int killerId);
}

/// <summary>
/// Nobody dies while a <see cref="IRespawnMatch"/> runs: a slime that would die comes back instead, through the game's
/// own "permanent revive" (as in the tutorial: revive animation at <see cref="Player.SpawnPosition"/>).
/// </summary>
internal static class Respawning
{
    internal static IRespawnMatch Current => (IRespawnMatch)FootballMatch.Current ?? RotationMatch.Current;

    [HarmonyPatch(typeof(PlayerCollision), nameof(PlayerCollision.killPlayer))]
    private static class NoDeaths
    {
        private static void Prefix(PlayerCollision __instance, int killerId, bool ignoreInvulnerability)
        {
            IRespawnMatch match = Current;
            if (match == null)
                return;
            // Only when this call really kills the slime itself: not invulnerable, and not in an ability form (the
            // form exits, then the slime is killed through here again).
            if ((__instance.IsInvulnerable && !ignoreInvulnerability) || !__instance.gameObject.activeSelf || __instance.ability != null)
                return;
            Player player = PlayerHandler.Get().GetPlayer(__instance.GetPlayerId());
            if (player == null || player.RespawnPositions.Count > 0 || player.ExtraLife != ReviveStatus.NoRevive)
                return;
            player.ExtraLife = ReviveStatus.permanentRevive;
            player.SpawnPosition = match.RespawnPosition(player);
            match.OnKilled(player, killerId);
        }
    }

    /// <summary>Water kills directly (and destroys the slime): send slimes through the revive above instead.</summary>
    [HarmonyPatch(typeof(KillZone), nameof(KillZone.OnCollide))]
    private static class NoDrowning
    {
        private static bool Prefix(KillZone __instance, ref CollisionInformation collision)
        {
            if (Current == null || collision.layer != LayerMask.NameToLayer("Player"))
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
