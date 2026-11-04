using System;
using System.Collections.Generic;
using System.Linq;
using BoplFixedMath;
using CaliSkills.Abilities.Pinball;
using UnityEngine;

namespace CaliSkills.Abilities.Charge;

/// <summary>
/// Charge: an instant ability (the game keeps the slime; it only looks like the meteor, see <see cref="ChargeVisuals"/>).
/// - Holding loads the charge for up to <see cref="ChargeSettings.MaxChargeTime"/>: like the Meteor's hold, the slime
///   can't move, jump or use abilities (input suppressed) and hangs in the air (no gravity); it grows up to
///   <see cref="ChargeSettings.MaxGrowth"/> and points where the player aims (aim, else movement; starts at the facing).
/// - Releasing (or a full charge) launches it that way, in a straight line, distance and knockback growing with the
///   charge. A platform in the way stops it (it lands on it). On a platform, aiming into it slides along it.
/// - Every other player it passes through is knocked away (ragdolled, like the Magnet gun's throw) and, if they then hit
///   a platform fast enough, squashed (<see cref="ChargeKnocks"/>); flying things in the path are knocked away too.
/// Sim code: inputs, Fix math and objects in a fixed order; the meteor look is drawn from this state.
/// </summary>
public class ChargeAbility : AbilityMonoBehaviour
{
    internal enum State
    {
        Idle,
        Charging,
        Dashing,
    }

    private SlimeController _caster;
    private Player _player;
    private int _slot = -1;
    private bool _inputWasSuppressed;
    private bool _gravityWasOn = true;
    private Fix _distance;
    private Fix _traveled;
    private readonly HashSet<int> _hit = new();

    internal State CurrentState { get; private set; }

    /// <summary>Seconds in the current state.</summary>
    internal Fix StateTime { get; private set; }

    /// <summary>Where the slime points / dashes (unit vector).</summary>
    internal Vec2 Direction { get; private set; } = Vec2.right;

    /// <summary>How loaded the charge is, 0..1 (frozen at the release during the dash).</summary>
    internal Fix Charge01 { get; private set; }

    internal SlimeController Caster => _caster;

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
        _slot = playerInfo.AbilityButtonUsedIndex012;
        if (_caster == null || _player == null || CurrentState != State.Idle)
            return;

        Direction = _caster.isFacingRight() ? Vec2.right : Vec2.left;
        Charge01 = Fix.Zero;
        _inputWasSuppressed = _caster.suppressInput;
        _caster.suppressInput = true;
        // Hangs in the air while charging, like the Meteor.
        _gravityWasOn = _caster.playerPhysics.ApplyGravity;
        _caster.playerPhysics.ApplyGravity = false;
        if (!_caster.playerPhysics.IsGrounded())
        {
            _caster.body.selfImposedVelocity = Vec2.zero;
            _caster.body.externalVelocity = Vec2.zero;
        }
        Aim();
        SetState(State.Charging);
        AudioManager.Get()?.Play("meteor_jump");
    }

    private void SetState(State state)
    {
        CurrentState = state;
        StateTime = Fix.Zero;
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        if (CurrentState == State.Idle)
            return;
        if (!CasterUsable())
        {
            Stop();
            return;
        }

        // The cooldown starts once the dash is over.
        if (_slot >= 0 && _slot < _caster.abilityCooldownTimers.Length)
            _caster.abilityCooldownTimers[_slot] = Fix.Zero;
        _caster.suppressInput = true;
        _caster.playerPhysics.ApplyGravity = false;

        Fix dt = GameTime.FixedDeltaTime(_player.Id, simDeltaTime);
        if (dt <= Fix.Zero)
            return;
        StateTime += dt;

        if (CurrentState == State.Charging)
        {
            if (!_caster.playerPhysics.IsGrounded())
            {
                _caster.body.selfImposedVelocity = Vec2.zero;
                _caster.body.externalVelocity *= (Fix)0.8f;
            }
            Aim();
            Fix max = Fix.Max((Fix)ChargeSettings.MaxChargeTime, (Fix)0.01f);
            Charge01 = Fix.Min(Fix.One, StateTime / max);
            if (StateTime >= max || !_player.AbilityButtonIsDown(_slot))
                Launch();
            return;
        }

        Dash(dt, simDeltaTime);
    }

    private bool CasterUsable() =>
        _caster != null && !_caster.IsDestroyed && _caster.isActiveAndEnabled && !_caster.isInAbility && _player.IsAlive;

    /// <summary>Points where the player aims (mouse / stick, else movement); keeps the last direction otherwise.</summary>
    private void Aim()
    {
        Vec2 aim = Vec2.NormalizedSafe(_player.AimVector());
        if (aim == Vec2.zero)
            aim = Vec2.NormalizedSafe(_player.InputVector());
        if (aim != Vec2.zero)
            Direction = aim;
    }

    internal Fix Scale => _caster.body.fixtrans.Scale;

    /// <summary>The slime's size factor now (1 = normal, up to 1 + MaxGrowth at a full charge).</summary>
    internal Fix Growth => Fix.One + Fix.Max((Fix)ChargeSettings.MaxGrowth, Fix.Zero) * Charge01;

    /// <summary>Dash length for the current charge.</summary>
    internal Fix DashDistance =>
        Lerp((Fix)ChargeSettings.MinDistance, (Fix)ChargeSettings.MaxDistance, Charge01) * Scale;

    private static Fix Lerp(Fix a, Fix b, Fix t) => a + (b - a) * t;

    private void Launch()
    {
        _distance = Fix.Max(DashDistance, Fix.Zero);
        _traveled = Fix.Zero;
        _hit.Clear();

        PlayerPhysics physics = _caster.playerPhysics;
        if (physics.IsGrounded())
        {
            StickyRoundedRectangle ground = physics.getAttachedGround();
            Vec2 normal = ground != null && !ground.IsDestroyed ? Vec2.NormalizedSafe(ground.currentNormal(_caster.body)) : Vec2.zero;
            Fix into = Vec2.Dot(Direction, normal);
            if (normal != Vec2.zero && into < Fix.Zero)
            {
                // Aimed into the platform: slide along it instead (straight into it, it goes nowhere).
                Vec2 along = Direction - normal * into;
                if (Vec2.Magnitude(along) > (Fix)0.2f)
                    Direction = Vec2.NormalizedSafe(along);
            }
            physics.UnGround(nullRotation: true, playSoundEffect: false);
        }
        _caster.body.selfImposedVelocity = Vec2.zero;
        _caster.body.externalVelocity = Vec2.zero;
        SetState(State.Dashing);
        AudioManager.Get()?.Play("dash");
        GetComponent<ChargeVisuals>()?.OnLaunch();
    }

    /// <summary>
    /// Moves straight along <see cref="Direction"/> (the slime's own velocity stays 0, so its physics don't drag or
    /// bend it), hitting what's in the way; a platform ahead ends the dash with the slime stuck to it.
    /// </summary>
    private void Dash(Fix dt, Fix simDeltaTime)
    {
        _caster.body.selfImposedVelocity = Vec2.zero;
        _caster.body.externalVelocity = Vec2.zero;
        PlayerPhysics physics = _caster.playerPhysics;
        Fix speed = Fix.Max((Fix)ChargeSettings.DashSpeed, (Fix)1L) * Scale;
        Fix step = Fix.Min(speed * dt, _distance - _traveled);
        Vec2 from = _caster.body.position;

        // A platform within this step: stop on it (the game's own landing raycasts: attaches the slime to it).
        bool landed = !physics.IsGrounded() && step > Fix.Zero &&
                      physics.CollisionRaycasts(attachToGroundIfHit: true, Direction * (step / dt), simDeltaTime);
        Vec2 to = landed ? _caster.body.position : from + Direction * step;
        HitAlong(from, to);

        if (landed || physics.IsGrounded())
        {
            KnockPlatform(physics.getAttachedGround());
            AudioManager.Get()?.Play("meteor_dash");
            End(keepMomentum: false);
            return;
        }
        _caster.body.position = to;
        _traveled += step;
        if (_traveled >= _distance)
            End(keepMomentum: true);
    }

    private void End(bool keepMomentum)
    {
        if (keepMomentum && !_caster.playerPhysics.IsGrounded())
        {
            Fix speed = (Fix)ChargeSettings.DashSpeed * Scale * Fix.Max((Fix)ChargeSettings.EndMomentum, Fix.Zero);
            _caster.body.selfImposedVelocity = Direction * speed;
        }
        Stop();
    }

    /// <summary>Back to normal (also when the caster died or vanished).</summary>
    private void Stop()
    {
        if (_caster != null && !_caster.IsDestroyed)
        {
            _caster.suppressInput = _inputWasSuppressed;
            _caster.playerPhysics.ApplyGravity = _gravityWasOn;
        }
        Charge01 = Fix.Zero;
        SetState(State.Idle);
    }

    // --- Hits ---

    private static readonly ChargeKnockTuning KnockTuning = new();

    /// <summary>Knocks every other player and flying thing the slime (grown) touches on its way from → to, once per dash.</summary>
    private void HitAlong(Vec2 from, Vec2 to)
    {
        int owner = _player.Id;
        Fix reach = _caster.playerPhysics.radius * Growth;
        Fix knock = Lerp((Fix)ChargeSettings.MinKnockback, (Fix)ChargeSettings.MaxKnockback, Charge01);
        Vec2 side = new(-Direction.y, Direction.x);

        foreach (PlayerCollision target in Players(owner))
        {
            int id = target.GetPlayerId();
            if (_hit.Contains(id))
                continue;
            Fix radius = target.physics != null ? target.physics.radius : Fix.Zero;
            Vec2 offset = target.body.position - ClosestOnSegment(from, to, target.body.position);
            if (Vec2.SqrMagnitude(offset) > (reach + radius) * (reach + radius))
                continue;
            if (target.GetComponent<Ability>() is Ability form && !form.isPushable)
                continue;
            _hit.Add(id);

            // Mostly straight ahead, a bit aside: out of the way, to the side it was on.
            Fix aside = Vec2.Dot(offset, side) >= Fix.Zero ? Fix.One : -Fix.One;
            Vec2 impulse = Vec2.NormalizedSafe(Direction + side * (aside * (Fix)ChargeSettings.SideKnock)) * knock;
            PinballKnock.Apply(target.gameObject, target.body, impulse, KnockTuning);
            if (target.GetComponent<SlimeController>() is SlimeController slime && slime.isActiveAndEnabled)
                ChargeKnocks.Track(slime, owner, (Fix)ChargeSettings.KnockDuration + (Fix)0.3f);
            AudioManager.Get()?.Play("rockBounce");
        }

        foreach (BoplBody body in Bodies())
        {
            Vec2 offset = body.position - ClosestOnSegment(from, to, body.position);
            Fix range = reach + (Fix)0.5f;
            if (Vec2.SqrMagnitude(offset) > range * range)
                continue;
            Fix speed = Fix.Max(Vec2.Magnitude(body.velocity), knock);
            body.velocity = Direction * speed;
        }
    }

    /// <summary>
    /// The platform the dash slammed into is pushed along the dash where it was hit (like the Meteor's impact, scaled by
    /// the charge) and shakes.
    /// </summary>
    private void KnockPlatform(StickyRoundedRectangle platform)
    {
        if (platform == null || platform.IsDestroyed)
            return;
        Fix maxKnock = Fix.Max((Fix)ChargeSettings.MaxKnockback, (Fix)0.01f);
        Fix share = Lerp((Fix)ChargeSettings.MinKnockback, (Fix)ChargeSettings.MaxKnockback, Charge01) / maxKnock;
        Fix force = (Fix)ChargeSettings.PlatformKnockback * share;
        BoplBody ground = platform.GetGroundBody();
        if (ground != null && force > Fix.Zero)
            ground.AddForceAtPosition(Direction * force, platform.GetClosestPoint(_caster.body.position));
        // The shake is how far the platform jitters (units): keep it small.
        platform.GetComponent<ShakablePlatform>()?.AddShake((Fix)0.25f, (Fix)0.1f + (Fix)0.2f * Charge01, 0);
    }

    private static Vec2 ClosestOnSegment(Vec2 a, Vec2 b, Vec2 p)
    {
        Vec2 ab = b - a;
        Fix length2 = Vec2.SqrMagnitude(ab);
        if (length2 <= Fix.Zero)
            return a;
        Fix t = Fix.Max(Fix.Zero, Fix.Min(Fix.One, Vec2.Dot(p - a, ab) / length2));
        return a + ab * t;
    }

    /// <summary>Other players' active bodies, in an order that's the same on every machine.</summary>
    private static List<PlayerCollision> Players(int owner) =>
        FindObjectsOfType<PlayerCollision>()
            .Where(b => !b.IsDestroyed && b.gameObject.activeInHierarchy && b.body != null && b.GetPlayerId() != owner &&
                        PlayerHandler.Get().GetPlayer(b.GetPlayerId()) is Player p && p.IsAlive &&
                        (b.GetComponent<SlimeController>() is not SlimeController s || s.hasSpawned))
            .OrderBy(b => b.GetPlayerId())
            .ThenBy(b => b.body.position.x.m_rawValue)
            .ThenBy(b => b.body.position.y.m_rawValue)
            .ToList();

    /// <summary>Flying bodies (projectiles, items), same filter as the Fan's.</summary>
    private static List<BoplBody> Bodies() =>
        FindObjectsOfType<BoplBody>()
            .Where(b => !b.IsDestroyed && b.gameObject.activeInHierarchy && b.HasBeenInitialized &&
                        b.GetComponent<PlayerCollision>() == null && b.GetComponent<AbilityMonoBehaviour>() == null &&
                        b.GetComponent<StickyRoundedRectangle>() == null && b.GetComponent<FlammableSmoke>() == null &&
                        b.GetComponent<BlackHole>() == null &&
                        (b.GetComponent<Grenade>() is not Grenade g || g.hasBeenThrown))
            .OrderBy(b => b.HierarchyNumber)
            .ToList();

    public override void ExitAbility(AbilityExitInfo info)
    {
    }

    public override Fix GetCooldown() => (Fix)ChargeSettings.AbilityCooldown;

    public override bool IsInstant() => true;

    public override bool IsCastable(Player player) => CurrentState == State.Idle;

    public override void UpdateCooldowns(PlayerBody playerBody)
    {
    }

    public override PlayerCollision GetPlayerCollision() => null;

    public override SlimeController GetSlimeController() => _caster;

    /// <summary>The Pinball's knock helper, with the Charge's tumble time.</summary>
    private sealed class ChargeKnockTuning : PinballTuning
    {
        public override float KnockDuration => ChargeSettings.KnockDuration;
        public override float MinDetachFraction => 0.3f;
    }
}
