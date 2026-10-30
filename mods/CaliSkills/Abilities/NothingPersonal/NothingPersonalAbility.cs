using System;
using System.Collections.Generic;
using System.Linq;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;

namespace CaliSkills.Abilities.NothingPersonal;

/// <summary>
/// "Nothing Personal Kid": an instant ability (the slime stays a slime). On use it picks the alive player furthest away
/// and teleports behind them (behind = opposite to where the target faces), like the Teleport ability:
/// - departure: the slime vanishes (hidden, frozen, no input, untouchable) leaving a smoke cloud, a portal marker shows
///   where it will appear and the Teleport light beam runs between the two places;
/// - <see cref="NothingPersonalSettings.TravelTime"/> later it appears at the marker (which followed the target), facing
///   the target, then stays immune and untouchable like after a respawn (PlayerCollision invulnerability + hurtbox off)
///   and can't use other abilities for a moment (<see cref="AbilityLockPatch"/>).
/// Sim code: decisions use simulated state and Fix math only; the marker, beam and smoke are visuals.
/// </summary>
public class NothingPersonalAbility : AbilityMonoBehaviour
{
    /// <summary>Our own invulnerability bit, so ending it doesn't clear the game's (respawn uses 4).</summary>
    private const uint ImmunityMask = 1u << 10;

    private SlimeController _caster;
    private Player _player;

    private bool _travelling;
    private bool _inputWasSuppressed;
    private int _targetId = -1;
    private Fix _travelLeft;
    private Fix _immunityLeft;
    private Fix _lockLeft;
    private GameObject _marker;

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
        // Ability objects are spawned inactive and Updater skips inactive ones: UpdateSim must run from now on.
        gameObject.SetActive(true);
        _caster = playerInfo.slimeController;
        _player = PlayerHandler.Get().GetPlayer(playerInfo.playerId);
        if (_caster == null || _player == null || _travelling)
            return;

        PlayerCollision target = FurthestTarget(_caster);
        if (target == null)
            return;

        _targetId = target.GetPlayerId();
        Depart(BehindPosition(_caster, target));
        if (_travelLeft <= Fix.Zero)
            Arrive();
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        Fix dt = _player != null ? GameTime.FixedDeltaTime(_player.Id, simDeltaTime) : simDeltaTime;

        if (_travelling)
        {
            _travelLeft -= dt;
            if (_travelLeft <= Fix.Zero || !CasterUsable())
            {
                Arrive();
            }
            else
            {
                PlayerCollision target = TargetBody(_targetId) ?? FurthestTarget(_caster);
                if (target != null)
                    NothingPersonalVisuals.MoveMarker(_marker, BehindPosition(_caster, target));
            }
        }

        if (_immunityLeft > Fix.Zero && !_travelling)
        {
            _immunityLeft -= dt;
            if (_immunityLeft <= Fix.Zero)
                SetImmune(false);
        }
        if (_lockLeft > Fix.Zero && !_travelling)
            _lockLeft -= dt;
    }

    private bool CasterUsable() =>
        _caster != null && !_caster.IsDestroyed && _caster.isActiveAndEnabled && !_caster.isInAbility && _player.IsAlive;

    /// <summary>Vanish: hidden, frozen and untouchable (like a slime waiting to be revived), smoke and beam.</summary>
    private void Depart(Vec2 destination)
    {
        PlayerBody body = _caster.body;
        Vec2 from = body.position;

        if (body.ropeBody != null)
            body.AttachRope(null, body.topAttachment);
        _caster.playerPhysics.UnGround(nullRotation: true, playSoundEffect: false);
        body.selfImposedVelocity = Vec2.zero;
        body.externalVelocity = Vec2.zero;
        _caster.playerPhysics.enabled = false;
        body.simulated = false;
        _inputWasSuppressed = _caster.suppressInput;
        _caster.suppressInput = true;
        if (_caster.spriteRen != null)
            _caster.spriteRen.enabled = false;
        SetImmune(true);

        _travelling = true;
        _travelLeft = (Fix)NothingPersonalSettings.TravelTime;
        _lockLeft = Fix.Zero;
        _immunityLeft = Fix.Zero;

        DestroyMarker();
        _marker = NothingPersonalVisuals.SpawnMarker(destination, _player);
        NothingPersonalVisuals.SpawnSmoke(from, _player.Scale);
        NothingPersonalVisuals.PlayTeleportEffects(from, destination, _player.Scale);
        AudioManager.Get()?.Play("swap");
    }

    /// <summary>Appear behind the target (wherever it is now), facing it, then stay immune for a moment.</summary>
    private void Arrive()
    {
        _travelling = false;
        DestroyMarker();
        if (_caster == null || _caster.IsDestroyed)
            return;

        PlayerBody body = _caster.body;
        PlayerCollision target = TargetBody(_targetId) ?? FurthestTarget(_caster);
        if (target != null)
        {
            Vec2 to = BehindPosition(_caster, target);
            body.position = to;
            body.transform.position = (Vector3)(Vector2)to;
            if (_caster.flipTrans != null)
                _caster.flipTrans.alignTransform(target.body.position.x > to.x);
        }
        body.selfImposedVelocity = Vec2.zero;
        body.externalVelocity = Vec2.zero;

        // Back to normal (what SlimeController.Spawn restores after a revive).
        _caster.playerPhysics.enabled = true;
        body.simulated = true;
        _caster.suppressInput = _inputWasSuppressed;
        if (_caster.spriteRen != null)
            _caster.spriteRen.enabled = true;

        _immunityLeft = (Fix)NothingPersonalSettings.ImmunityDuration;
        _lockLeft = (Fix)NothingPersonalSettings.AbilityLockDuration;
        SetImmune(_immunityLeft > Fix.Zero);
    }

    /// <summary>Immune like after a respawn: the game's invulnerability (own bit) and no hurtbox, so nothing touches us.</summary>
    private void SetImmune(bool immune)
    {
        if (_caster == null || _caster.IsDestroyed)
            return;
        _caster.playerCollision?.SetInvulnerable(immune, ImmunityMask);
        if (_caster.hurtBox is MonoBehaviour hurtBox && hurtBox != null)
            hurtBox.enabled = !immune;
    }

    /// <summary>True while this caster can't use other abilities.</summary>
    internal bool LocksAbilities => _travelling || _lockLeft > Fix.Zero;

    /// <summary>The alive enemy body furthest from the caster (ties: lower player id, then position).</summary>
    private static PlayerCollision FurthestTarget(SlimeController caster)
    {
        int casterId = caster.GetPlayerId();
        Vec2 origin = caster.body.position;
        return Bodies()
            .Where(b => b.GetPlayerId() != casterId)
            .OrderByDescending(b => Vec2.SqrMagnitude(b.body.position - origin).m_rawValue)
            .ThenBy(b => b.GetPlayerId())
            .FirstOrDefault();
    }

    private static PlayerCollision TargetBody(int playerId) =>
        playerId < 0 ? null : Bodies().FirstOrDefault(b => b.GetPlayerId() == playerId);

    /// <summary>Active bodies of alive players, in an order that's the same on every machine.</summary>
    private static IEnumerable<PlayerCollision> Bodies() =>
        FindObjectsOfType<PlayerCollision>()
            .Where(b => b.gameObject.activeInHierarchy && b.body != null && !b.IsInvulnerable &&
                        PlayerHandler.Get().GetPlayer(b.GetPlayerId()) is Player p && p.IsAlive)
            .OrderBy(b => b.GetPlayerId())
            .ThenBy(b => b.body.position.x.m_rawValue)
            .ThenBy(b => b.body.position.y.m_rawValue);

    /// <summary>
    /// Just behind the target, a bit above its center: opposite to where it faces (along its platform if it stands on
    /// one), lifted along the platform's normal.
    /// </summary>
    private static Vec2 BehindPosition(SlimeController caster, PlayerCollision target)
    {
        Vec2 targetPos = target.body.position;
        Vec2 forward, up;
        SlimeController slime = target.GetComponent<SlimeController>();
        if (slime != null)
        {
            PlayerPhysics physics = slime.playerPhysics;
            StickyRoundedRectangle ground = physics.IsGrounded() ? physics.getAttachedGround() : null;
            bool hasGround = ground != null && !ground.IsDestroyed;
            forward = hasGround ? Vec2.NormalizedSafe(ground.forwardsDirection(slime.body)) : Vec2.right;
            if (!slime.isFacingRight())
                forward = -forward;
            up = hasGround ? Vec2.NormalizedSafe(ground.currentNormal(slime.body)) : Vec2.up;
        }
        else
        {
            // Ability form: no facing to read, assume it faces the caster.
            forward = Vec2.NormalizedSafe(caster.body.position - targetPos);
            if (forward == Vec2.zero)
                forward = Vec2.right;
            up = Vec2.up;
        }

        Fix casterRadius = caster.playerPhysics.radius;
        Fix targetRadius = target.physics != null ? target.physics.radius : casterRadius;
        Fix scale = caster.body.fixtrans.Scale;
        Fix gap = (Fix)NothingPersonalSettings.BehindGap * scale;
        Fix lift = (Fix)NothingPersonalSettings.BehindLift * scale;
        Vec2 position = targetPos - forward * (casterRadius + targetRadius + gap) + up * lift;
        return SceneBounds.ClampInsideCameraMaxBounds(position, new Vec2(casterRadius, casterRadius));
    }

    private void DestroyMarker()
    {
        if (_marker != null)
            Destroy(_marker);
        _marker = null;
    }

    private void OnDestroy() => DestroyMarker();

    public override void ExitAbility(AbilityExitInfo info)
    {
    }

    public override Fix GetCooldown() => (Fix)NothingPersonalSettings.AbilityCooldown;

    public override bool IsInstant() => true;

    // Needs someone to teleport behind, and not already on the way.
    public override bool IsCastable(Player player) => !_travelling && OtherPlayerAlive(player);

    private static bool OtherPlayerAlive(Player player) =>
        PlayerHandler.Get().PlayerList().Any(p => p != null && p != player && p.IsAlive);

    public override void UpdateCooldowns(PlayerBody playerBody)
    {
    }

    public override PlayerCollision GetPlayerCollision() => null;

    public override SlimeController GetSlimeController() => _caster;
}

/// <summary>Right after a Nothing Personal teleport, the slime's other abilities can't be cast.</summary>
[HarmonyPatch(typeof(SlimeController), nameof(SlimeController.isAbilityCastable))]
internal static class AbilityLockPatch
{
    private static void Postfix(SlimeController __instance, int abilityIndex, ref bool __result)
    {
        if (!__result)
            return;
        foreach (AbilityMonoBehaviour ability in __instance.abilities)
        {
            if (ability is NothingPersonalAbility npk && npk.LocksAbilities && __instance.abilities[abilityIndex] != ability)
            {
                __result = false;
                return;
            }
        }
    }
}
