using System.Collections.Generic;
using BoplFixedMath;
using CaliSkills.Core;
using HarmonyLib;
using UnityEngine;

namespace CaliSkills.Abilities.Rps;

/// <summary>
/// A Rock Paper Scissors duel between the challenger and the challenged slime (scene object: gone with the level).
/// - Time stops for everyone, like the Time stop ability (same global time scale and shader effect, its sphere), and
///   the two duelists are frozen too: their slimes don't update (<see cref="FreezeDuelists"/>), their bodies aren't
///   simulated, and they can't die (<see cref="NoDeathInDuel"/>). They stay protected from the time stop only so the
///   game keeps reading their inputs, so everything else of theirs is frozen by hand too: their other slimes (Duplicator
///   copies), ability forms, abilities in use, projectiles, items and mines (<see cref="FreezeOwned"/>).
/// - <see cref="RpsSettings.VsDuration"/>: the VS screen. Then <see cref="RpsSettings.ChoiceTime"/>: their ability
///   buttons are rock / paper / scissors (the last press counts; none = random). Then the picks are shown
///   (<see cref="RpsSettings.RevealTime"/>), time starts again and the loser is squashed (killed by the winner). A draw
///   puts all of the challenged player's abilities on cooldown.
/// A running vanilla Time stop is paused meanwhile and goes on afterwards.
/// Sim code: synced inputs, sim time and the game's seeded RNG; <see cref="RpsScreen"/> draws it.
/// </summary>
internal sealed class RpsDuel : MonoUpdatable
{
    internal enum Phase
    {
        Versus,
        Choice,
        Reveal,
    }

    internal static RpsDuel Current { get; private set; }

    internal SlimeController Challenger { get; private set; }
    internal SlimeController Challenged { get; private set; }
    internal Phase CurrentPhase { get; private set; }

    /// <summary>Seconds since the duel started (sim time).</summary>
    internal Fix Elapsed { get; private set; }

    internal Fix VsEnd { get; private set; }
    internal Fix ChoiceEnd { get; private set; }
    internal Fix RevealEnd { get; private set; }

    /// <summary>Picks (0 rock, 1 paper, 2 scissors; -1 none yet), challenger first.</summary>
    internal readonly int[] Choices = { -1, -1 };

    /// <summary>Winner: 0 challenger, 1 challenged, -1 draw (known from the reveal on).</summary>
    internal int Winner { get; private set; } = -1;

    private readonly bool[,] _wasDown = new bool[2, 3];
    private Fix _savedTimeScale;
    private readonly Dictionary<Player, bool> _savedProtection = new();
    private float _savedShader;
    private bool _ended;

    /// <summary>The duelist players' slimes frozen so far (copies included), unfrozen at the end.</summary>
    private readonly HashSet<SlimeController> _frozen = new();

    /// <summary>Owner player id of each updatable asked about during this duel (-1 = nobody's).</summary>
    private readonly Dictionary<MonoUpdatable, int> _owners = new();
    private RpsScreen _screen;

    /// <summary>A slime that can take part in a duel right now.</summary>
    internal static bool CanDuel(SlimeController slime) =>
        slime != null && !slime.IsDestroyed && slime.isActiveAndEnabled && slime.hasSpawned && !slime.isInAbility &&
        slime.body != null && slime.playerCollision != null && !slime.playerCollision.IsInvulnerable &&
        PlayerHandler.Get().GetPlayer(slime.GetPlayerId()) is Player p && p.IsAlive;

    internal static void Begin(SlimeController challenger, SlimeController challenged)
    {
        if (Current != null || GameSessionHandler.HasGameEnded() || !CanDuel(challenger) || !CanDuel(challenged))
            return;
        var go = new GameObject("CaliSkills.RpsDuel");
        RpsDuel duel = go.AddComponent<RpsDuel>();
        duel.Setup(challenger, challenged);
    }

    private void Awake()
    {
        Updater.RegisterUpdatable(this);
    }

    public override void Init()
    {
    }

    private void Setup(SlimeController challenger, SlimeController challenged)
    {
        Current = this;
        Challenger = challenger;
        Challenged = challenged;
        VsEnd = Fix.Max((Fix)RpsSettings.VsDuration, Fix.Zero);
        ChoiceEnd = VsEnd + Fix.Max((Fix)RpsSettings.ChoiceTime, (Fix)0.1f);
        RevealEnd = ChoiceEnd + Fix.Max((Fix)RpsSettings.RevealTime, Fix.Zero);

        _savedTimeScale = GameTime.PlayerTimeScale;
        foreach (Player player in PlayerHandler.Get().PlayerList())
            _savedProtection[player] = player.isProtectedFromTimeStop;
        _savedShader = Shader.GetGlobalFloat("_Timestop01");
        StopTime();
        Shader.SetGlobalFloat("_Timestop01", 0f);

        FreezeSlimes();

        if (RpsSkill.TimeStopSound != null)
            AudioManager.Get()?.Play(RpsSkill.TimeStopSound);
        _screen = RpsScreen.Create(this);
    }

    /// <summary>Everyone's time stops; only the duelists stay "protected", which keeps their inputs coming.</summary>
    private void StopTime()
    {
        GameTime.PlayerTimeScale = Fix.Zero;
        int a = Challenger.GetPlayerId(), b = Challenged.GetPlayerId();
        foreach (Player player in PlayerHandler.Get().PlayerList())
            player.isProtectedFromTimeStop = player.Id == a || player.Id == b;
    }

    /// <summary>Every slime of the two duelist players (the duelists and their Duplicator copies) is frozen.</summary>
    private void FreezeSlimes()
    {
        foreach (SlimeController slime in SceneScan.All<SlimeController>())
        {
            if (slime != null && !slime.IsDestroyed && IsDuelist(slime.playerNumber) && _frozen.Add(slime))
                Freeze(slime, true);
        }
    }

    /// <summary>
    /// The player owning <paramref name="updatable"/>: an ability (form or in use) → the slime using it; a projectile,
    /// mine or anything else holding a player id; an item (grenade...) → its owner. -1 for nobody's.
    /// </summary>
    private int OwnerOf(MonoUpdatable updatable)
    {
        if (_owners.TryGetValue(updatable, out int owner))
            return owner;
        owner = -1;
        if (updatable is AbilityMonoBehaviour ability && ability.GetSlimeController() is SlimeController slime && slime != null)
            owner = slime.playerNumber;
        else if (updatable is IPlayerIdHolder holder)
            owner = holder.GetPlayerId();
        else if (updatable.GetComponent<Ability>() is Ability form && form != null)
            owner = form.GetPlayerId();
        else if (updatable.GetComponent<Item>() is Item item && item != null)
            owner = item.OwnerId;
        else if (updatable.GetComponent<IPlayerIdHolder>() is IPlayerIdHolder parentHolder)
            owner = parentHolder.GetPlayerId();
        _owners[updatable] = owner;
        return owner;
    }

    /// <summary>Body out of the simulation (no movement, gravity or physics updates); the slime itself is skipped by the patch.</summary>
    private static void Freeze(SlimeController slime, bool frozen)
    {
        if (slime == null || slime.IsDestroyed)
            return;
        slime.body.simulated = !frozen;
        slime.playerPhysics.enabled = !frozen;
    }

    internal bool IsDuelist(SlimeController slime) => !_ended && (slime == Challenger || slime == Challenged);

    internal bool IsDuelist(int playerId) =>
        !_ended && (Challenger != null && Challenger.GetPlayerId() == playerId || Challenged != null && Challenged.GetPlayerId() == playerId);

    internal Player PlayerOf(int duelist) =>
        PlayerHandler.Get().GetPlayer((duelist == 0 ? Challenger : Challenged).GetPlayerId());

    public override void UpdateSim(Fix simDeltaTime)
    {
        if (_ended)
            return;
        if (GameSessionHandler.HasGameEnded() || !Present(Challenger) || !Present(Challenged) ||
            PlayerOf(0) == null || PlayerOf(1) == null)
        {
            End(resolve: false);
            return;
        }

        StopTime();
        FreezeSlimes();
        AudioManager.Get()?.FadeOutMusicTick(0.2f);
        Elapsed += simDeltaTime;

        if (CurrentPhase == Phase.Versus && Elapsed >= VsEnd)
        {
            CurrentPhase = Phase.Choice;
            // A button already held when the choice opens needs a new press.
            for (int d = 0; d < 2; d++)
            for (int i = 0; i < 3; i++)
                _wasDown[d, i] = PlayerOf(d).AbilityButtonIsDown(i);
            Plugin.Log.LogInfo($"RPS: choice opens. P{PlayerOf(0).Id} (protected {PlayerOf(0).isProtectedFromTimeStop}) vs " +
                               $"P{PlayerOf(1).Id} (protected {PlayerOf(1).isProtectedFromTimeStop}), time scale {(float)GameTime.PlayerTimeScale}.");
        }

        if (CurrentPhase == Phase.Choice)
        {
            ReadChoices();
            if (Elapsed >= ChoiceEnd)
                Reveal();
        }

        if (CurrentPhase == Phase.Reveal && Elapsed >= RevealEnd)
            End(resolve: true);
    }

    private static bool Present(SlimeController slime) => slime != null && !slime.IsDestroyed && slime.gameObject.activeInHierarchy;

    private void ReadChoices()
    {
        for (int d = 0; d < 2; d++)
        {
            Player player = PlayerOf(d);
            for (int i = 0; i < 3; i++)
            {
                bool down = player.AbilityButtonIsDown(i);
                if (down && !_wasDown[d, i])
                {
                    Choices[d] = i;
                    AudioManager.Get()?.Play("abilityPickup");
                    Plugin.Log.LogInfo($"RPS: P{player.Id} picks {RpsSkill.ChoiceNames[i]}.");
                }
                _wasDown[d, i] = down;
            }
        }
    }

    private void Reveal()
    {
        for (int d = 0; d < 2; d++)
        {
            if (Choices[d] < 0)
                Choices[d] = Updater.RandomInt(0, 3);
        }
        // Paper (1) beats rock (0), scissors (2) beat paper, rock beats scissors.
        int diff = (Choices[0] - Choices[1] + 3) % 3;
        Plugin.Log.LogInfo($"RPS: {RpsSkill.ChoiceNames[Choices[0]]} vs {RpsSkill.ChoiceNames[Choices[1]]}.");
        Winner = diff == 0 ? -1 : diff == 1 ? 0 : 1;
        CurrentPhase = Phase.Reveal;
        AudioManager.Get()?.Play("swap");
    }

    private void End(bool resolve)
    {
        if (_ended)
            return;
        _ended = true;
        if (Current == this)
            Current = null;

        // Time as it was (a vanilla Time stop that was running goes on).
        GameTime.PlayerTimeScale = _savedTimeScale;
        foreach (KeyValuePair<Player, bool> saved in _savedProtection)
            saved.Key.isProtectedFromTimeStop = saved.Value;
        Shader.SetGlobalFloat("_Timestop01", _savedShader);
        foreach (SlimeController slime in _frozen)
            Freeze(slime, false);
        _frozen.Clear();
        _owners.Clear();
        _screen?.Finish();

        if (resolve)
            Resolve();
        Updater.DestroyFix(gameObject);
    }

    private void Resolve()
    {
        if (Winner < 0)
        {
            // Draw: the challenged player's abilities all start their cooldown.
            if (Present(Challenged))
            {
                for (int i = 0; i < Challenged.abilityCooldownTimers.Length; i++)
                    Challenged.abilityCooldownTimers[i] = Fix.Zero;
            }
            return;
        }

        SlimeController winner = Winner == 0 ? Challenger : Challenged;
        SlimeController loser = Winner == 0 ? Challenged : Challenger;
        if (Present(loser))
            loser.playerCollision.killPlayer(winner.GetPlayerId(), spawnEffect: true, ignoreInvulnerability: false, CauseOfDeath.Squished);
    }

    private void OnDestroy()
    {
        // Level unloaded mid-duel: the next level resets time itself.
        if (Current == this)
            Current = null;
        _screen?.Finish();
    }

    /// <summary>
    /// The duelist players' slimes (copies included) don't update at all during the duel (no input, abilities,
    /// cooldowns, movement).
    /// </summary>
    [HarmonyPatch(typeof(SlimeController), nameof(SlimeController.UpdateSim))]
    private static class FreezeDuelists
    {
        private static bool Prefix(SlimeController __instance) => Current == null || !Current.IsDuelist(__instance.playerNumber);
    }

    /// <summary>
    /// Everything else of the duelist players stands still too (their time isn't stopped, to keep their inputs coming):
    /// the Updater skips their ability forms, abilities in use, projectiles, items and mines while the duel runs.
    /// </summary>
    [HarmonyPatch]
    private static class FreezeOwned
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MonoUpdatable), nameof(MonoUpdatable.IsEnabled));
            yield return AccessTools.Method(typeof(PlayerBody), nameof(PlayerBody.IsEnabled));
        }

        private static void Postfix(MonoUpdatable __instance, ref bool __result)
        {
            RpsDuel duel = Current;
            if (!__result || duel == null || duel._ended || __instance is RpsDuel)
                return;
            if (duel.IsDuelist(duel.OwnerOf(__instance)))
                __result = false;
        }
    }

    /// <summary>
    /// The duelists' ability icons stay lit during the duel: the game dims uncastable slots every frame
    /// (SlimeController.LateUpdate), and the RPS slot isn't castable while a duel runs. Their slimes don't update, so
    /// nothing gets cast.
    /// </summary>
    [HarmonyPatch(typeof(SlimeController), nameof(SlimeController.isAbilityCastable))]
    private static class IconsLitInDuel
    {
        private static void Postfix(SlimeController __instance, ref bool __result)
        {
            if (Current != null && Current.IsDuelist(__instance))
                __result = true;
        }
    }

    /// <summary>Nothing kills a duelist before the duel is over.</summary>
    [HarmonyPatch(typeof(PlayerCollision), nameof(PlayerCollision.killPlayer))]
    private static class NoDeathInDuel
    {
        private static bool Prefix(PlayerCollision __instance, ref bool __result)
        {
            if (Current == null || !Current.IsDuelist(__instance.GetPlayerId()))
                return true;
            __result = false;
            return false;
        }
    }

    /// <summary>A vanilla Time stop pauses during the duel (it would reset who's protected every tick).</summary>
    [HarmonyPatch(typeof(TimeStop), nameof(TimeStop.UpdateSim))]
    private static class PauseTimeStop
    {
        private static bool Prefix() => Current == null;
    }
}
