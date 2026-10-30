using System;
using BoplFixedMath;
using HarmonyLib;

namespace CaliSkills.Abilities.Run;

/// <summary>
/// Run: an instant ability (the slime stays a slime) that makes it faster while the button is held.
/// - Pressed: the slime's max speed and accelerations are multiplied by <see cref="RunSettings.SpeedMultiplier"/>.
/// - Held: the cooldown timer is kept at 0, so the cooldown starts when the run ends.
/// - Ends on release, or when the slime uses another ability (<see cref="StopRunOnOtherAbilityPatch"/>); after being
///   stopped by another ability it needs a release before it can run again.
/// The speed change lives on the slime's PlayerPhysics only while running, and is undone exactly when it stops.
/// Sim code only: driven by synced inputs and Fix math.
/// </summary>
public class RunAbility : AbilityMonoBehaviour
{
    private SlimeController _caster;
    private Player _player;
    private int _slot = -1;
    private bool _running;
    private bool _waitForRelease;

    private Fix _baseMaxSpeed;
    private Fix _baseAccel;
    private Fix _baseAirAccel;

    public override bool IsRope => false;
    public override bool CooldownStoppedDuringTimeStop => false;
    public override bool IgnoreNextCooldown => false;

    private void Awake()
    {
        // Same as the game's InstantAbility: abilities are simulation objects driven by Updater.
        Updater.RegisterUpdatable(this);
    }

    public override void Init()
    {
    }

    public override void EnterAbility(PlayerInfo playerInfo, Action<AbilityExitInfo> onExitAbility)
    {
        // Like InstantAbility: ability objects are spawned inactive, and Updater skips inactive ones (UpdateSim must
        // run from now on to watch the button).
        gameObject.SetActive(true);
        _caster = playerInfo.slimeController;
        _player = PlayerHandler.Get().GetPlayer(playerInfo.playerId);
        _slot = playerInfo.AbilityButtonUsedIndex012;
        if (_caster == null || _caster.playerPhysics == null || _running)
            return;

        PlayerPhysics physics = _caster.playerPhysics;
        _baseMaxSpeed = physics.maxSpeed;
        _baseAccel = physics.accel;
        _baseAirAccel = physics.airAccel;
        Fix multiplier = (Fix)RunSettings.SpeedMultiplier;
        physics.maxSpeed = _baseMaxSpeed * multiplier;
        physics.accel = _baseAccel * multiplier;
        physics.airAccel = _baseAirAccel * multiplier;
        _running = true;
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        bool held = _player != null && _slot >= 0 && _player.AbilityButtonIsDown(_slot);
        if (_waitForRelease && !held)
            _waitForRelease = false;
        if (!_running)
            return;

        if (!held || _caster == null || _caster.IsDestroyed || !_caster.isActiveAndEnabled || _caster.isInAbility)
        {
            StopRunning();
            return;
        }

        // Cooldown starts when the run ends.
        if (_slot < _caster.abilityCooldownTimers.Length)
            _caster.abilityCooldownTimers[_slot] = Fix.Zero;
    }

    /// <summary>Another ability was used: stop, and don't start again until the button is released.</summary>
    internal void StopFromOtherAbility()
    {
        if (!_running)
            return;
        StopRunning();
        _waitForRelease = true;
    }

    private void StopRunning()
    {
        _running = false;
        if (_caster == null || _caster.IsDestroyed || _caster.playerPhysics == null)
            return;
        PlayerPhysics physics = _caster.playerPhysics;
        physics.maxSpeed = _baseMaxSpeed;
        physics.accel = _baseAccel;
        physics.airAccel = _baseAirAccel;
    }

    public override void ExitAbility(AbilityExitInfo info)
    {
    }

    public override Fix GetCooldown() => (Fix)RunSettings.AbilityCooldown;

    public override bool IsInstant() => true;

    public override bool IsCastable(Player player) => !_running && !_waitForRelease;

    public override void UpdateCooldowns(PlayerBody playerBody)
    {
    }

    public override PlayerCollision GetPlayerCollision() => null;

    public override SlimeController GetSlimeController() => _caster;
}

/// <summary>Using any other ability stops a running slime's Run.</summary>
[HarmonyPatch(typeof(SlimeController), nameof(SlimeController.EnterAbility))]
internal static class StopRunOnOtherAbilityPatch
{
    private static void Prefix(SlimeController __instance, int abilityIndex)
    {
        for (int i = 0; i < __instance.abilities.Count; i++)
        {
            if (i != abilityIndex && __instance.abilities[i] is RunAbility run)
                run.StopFromOtherAbility();
        }
    }
}
