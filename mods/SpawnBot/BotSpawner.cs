using System;
using System.Collections.Generic;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;

namespace SpawnBot;

/// <summary>
/// Spawns bot players and keeps them out of the round's win/lose logic.
///
/// A bot is a regular <see cref="Player"/> (it must be in PlayerHandler's list: SlimeController, hits and kills
/// all look players up by id) whose slime is created the same way the Duplicator ability creates clones
/// (PlayerCollision.SpawnAbilityLessClone). It has no abilities and no input device, so it just stands there.
/// </summary>
internal static class BotSpawner
{
    private const int TeamCount = 4;

    private static readonly List<Player> Bots = new();
    private static readonly List<Player> TemporarilyRemoved = new();

    internal static void TrySpawnBot(PlayerInfo info)
    {
        if (GameLobby.isOnlineGame)
        {
            Plugin.Log.LogWarning("SpawnBot only works in local games.");
            return;
        }

        int aliveBots = Bots.FindAll(b => b.IsAlive).Count;
        if (aliveBots >= Plugin.MaxBots.Value)
        {
            Plugin.Log.LogInfo($"Not spawning a bot: {aliveBots} already alive (MaxBots = {Plugin.MaxBots.Value}).");
            return;
        }

        SlimeController caster = info.slimeController;
        PlayerCollision casterCollision = caster.GetPlayerCollision();
        List<Player> players = PlayerHandler.Get().PlayerList();
        Player owner = PlayerHandler.Get().GetPlayer(info.playerId);

        int id = 1;
        foreach (Player p in players)
            id = Math.Max(id, p.Id + 1);

        var bot = new Player(id, PickTeam(players, owner))
        {
            Color = PickColor(players, owner),
            UsesKeyboardAndMouse = false,
            CanUseAbilities = false,
            playersAndClonesStillAlive = 1,
        };
        players.Add(bot);
        Bots.Add(bot);

        Fix side = info.isFacingRight ? Fix.One : -Fix.One;
        Fix distance = (Fix)Plugin.SpawnDistance.Value;
        Vec2 pos = info.position + new Vec2(side * distance, distance * (Fix)0.5f);

        SlimeController slime = FixTransform.InstantiateFixed(casterCollision.reviveEffectPrefab.emptyPlayerPrefab, pos);
        slime.playerNumber = id;
        slime.GetPlayerSprite().sprite = null;
        slime.GetPlayerSprite().material = bot.Color;
        slime.abilities = new List<AbilityMonoBehaviour>();
        slime.AbilityReadyIndicators = new AbilityReadyIndicator[0];
        slime.PrepareToRevive(pos);
        slime.isAbilitylessClone = true;
        slime.ReSpawn(pos);
        slime.Spawn();

        Plugin.Log.LogInfo($"Spawned bot P{id} (team {bot.Team}) at {pos}.");
    }

    private static int PickTeam(List<Player> players, Player owner)
    {
        // Prefer a team no human uses, so every human can hit the bot.
        for (int team = 0; team < TeamCount; team++)
        {
            bool used = players.Exists(p => !Bots.Contains(p) && p.Team == team);
            if (!used)
                return team;
        }
        return (owner.Team + 1) % TeamCount;
    }

    private static Material PickColor(List<Player> players, Player owner)
    {
        foreach (PlayerColors colors in Resources.FindObjectsOfTypeAll<PlayerColors>())
        {
            for (int i = 0; i < colors.Length; i++)
            {
                Material mat = colors[i].playerMaterial;
                if (mat != null && !players.Exists(p => p.Color == mat))
                    return mat;
            }
        }
        return owner.Color;
    }

    /// <summary>Called before every level load: bots only live for one round.</summary>
    internal static void RemoveAllBots()
    {
        if (Bots.Count == 0)
            return;
        PlayerHandler.Get().PlayerList()?.RemoveAll(p => Bots.Contains(p));
        Bots.Clear();
        TemporarilyRemoved.Clear();
    }

    internal static void HideBotsFromPlayerList()
    {
        TemporarilyRemoved.Clear();
        if (Bots.Count == 0)
            return;
        List<Player> players = PlayerHandler.Get().PlayerList();
        foreach (Player bot in Bots)
        {
            if (players.Remove(bot))
                TemporarilyRemoved.Add(bot);
        }
    }

    internal static void RestoreBotsToPlayerList()
    {
        if (TemporarilyRemoved.Count == 0)
            return;
        List<Player> players = PlayerHandler.Get().PlayerList();
        foreach (Player bot in TemporarilyRemoved)
        {
            // A level load during the hidden window may have dropped the bot for good.
            if (Bots.Contains(bot) && !players.Contains(bot))
                players.Add(bot);
        }
        TemporarilyRemoved.Clear();
    }
}

/// <summary>
/// GameSessionHandler.UpdateSim decides when the round is won/lost by counting players and teams, and
/// EndGameImmediately (leaving the match) picks winners from the player list.
/// Hide bots while they run, so killing a bot doesn't end the round and bots never count as winners.
/// </summary>
[HarmonyPatch]
internal static class ExcludeBotsFromRoundLogicPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(GameSessionHandler), nameof(GameSessionHandler.UpdateSim));
        yield return AccessTools.Method(typeof(GameSessionHandler), nameof(GameSessionHandler.EndGameImmediately));
    }

    private static void Prefix() => BotSpawner.HideBotsFromPlayerList();

    private static Exception Finalizer(Exception __exception)
    {
        BotSpawner.RestoreBotsToPlayerList();
        return __exception;
    }
}

[HarmonyPatch(typeof(Updater), nameof(Updater.PreLevelLoad))]
internal static class RemoveBotsOnLevelLoadPatch
{
    private static void Prefix() => BotSpawner.RemoveAllBots();
}
