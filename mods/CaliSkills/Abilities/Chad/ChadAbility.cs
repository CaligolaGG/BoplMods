using System;
using System.Collections.Generic;
using System.Linq;
using BoplFixedMath;
using UnityEngine;

namespace CaliSkills.Abilities.Chad;

/// <summary>
/// Chad mode: an instant ability (the game keeps the slime, which moves and jumps as usual; it only looks like the
/// Throw ability's muscly slime, see <see cref="ChadVisuals"/>). Until all three are used, the three ability
/// buttons are three moves instead of the player's abilities (<see cref="ChadPatches"/>: real abilities can't be cast,
/// the icons show the moves), each usable once (a press; buttons held when the mode starts need a new press):
/// - first button, Slam: on a platform a short wind-up then the hit; in the air a straight dive down until landing.
///   Every other player touching the rectangle under the chad (while diving and at the impact) is squashed.
/// - second button, Charge (the "Punch" move; the throw animation): a short dash sideways (screen-horizontal in the
///   air), towards where the player moves (A/D, left stick) or faces, <see cref="ChadSettings.ChargeDistance"/> long; a platform in the way
///   stops it; on a platform it runs along the platform's surface instead. Every other slime it passes through is stunned (<see cref="ChadStun"/>), keeping its momentum.
/// - third button, Aura farm: shining for <see cref="ChadSettings.AuraDuration"/>: invincible, no knockback, ragdoll,
///   stun or chicken curse; projectiles coming at the shining area are reflected (and become the chad's), so are
///   beams and rays (<see cref="ChadMirror"/>); smoke nearby is pushed away; black holes it touches become white holes;
///   invisible slimes nearby are shown (<see cref="ChadPatches"/>). Lasts its full time even if the mode ends.
/// Slam and charge lock movement while they last. The mode has no time limit: it ends once all three are used
/// (after the last move); then the cooldown starts.
/// Sim code: inputs, Fix math and players in a fixed order.
/// </summary>
public class ChadAbility : AbilityMonoBehaviour
{
    internal enum Move
    {
        None = -1,
        Slam = 0,
        Punch = 1,
        Aura = 2,
    }

    private const uint AuraImmunityMask = 1u << 11;

    /// <summary>Slimes in Chad mode, and slimes shining (for the patches).</summary>
    internal static readonly Dictionary<SlimeController, ChadAbility> Active = new();
    internal static readonly HashSet<SlimeController> Shining = new();

    /// <summary>The shining chads by player id (a fixed order for the beam and ray mirrors).</summary>
    internal static readonly SortedDictionary<int, ChadAbility> ShiningByPlayer = new();

    internal int PlayerId => _player != null ? _player.Id : -1;

    private SlimeController _caster;
    private Player _player;
    private int _slot = -1;
    private bool _inputWasSuppressed;
    private bool _gravityWasOn = true;
    private readonly bool[] _used = new bool[3];
    private readonly bool[] _wasDown = new bool[3];
    private readonly HashSet<int> _hit = new();
    private Fix _auraLeft;
    private bool _slamInAir;
    private bool _slamLanded;
    private Fix _landedTime;
    private Fix _punchSign = Fix.One;
    private Vec2 _chargeDir = Vec2.right;
    private Fix _chargeTraveled;
    private Fix _chargeDistance;
    private bool _chargeOnGround;
    private Vec2 _chargeLast;

    internal bool InMode { get; private set; }

    internal Move CurrentMove { get; private set; } = Move.None;

    /// <summary>Seconds since the current move started.</summary>
    internal Fix MoveTime { get; private set; }

    internal bool IsUsed(int move) => move >= 0 && move < 3 && _used[move];

    internal bool IsShining => _auraLeft > Fix.Zero;

    internal SlimeController Caster => _caster;

    /// <summary>Slam: whether it started in the air (diving), and whether it has hit the ground.</summary>
    internal bool SlamInAir => _slamInAir;
    internal bool SlamLanded => _slamLanded;

    /// <summary>
    /// Slam: how far the current phase is, 0..1. Before the impact: the wind-up (on a platform) or the start of the dive
    /// (in the air, then held); after it: the recovery.
    /// </summary>
    internal float SlamPhase01
    {
        get
        {
            if (_slamLanded)
                return Mathf.Clamp01((float)(MoveTime - _landedTime) / Mathf.Max(0.01f, ChadSettings.SlamRecover));
            float time = _slamInAir ? 0.15f : Mathf.Max(0.01f, ChadSettings.SlamWindup);
            return Mathf.Clamp01((float)MoveTime / time);
        }
    }

    /// <summary>Charge: whether it goes along the slime's right (screen right on a floor and in the air), else left.</summary>
    internal bool PunchRight => _punchSign > Fix.Zero;

    /// <summary>How far the current move's animation is, 0..1.</summary>
    internal float MoveProgress01
    {
        get
        {
            switch (CurrentMove)
            {
                case Move.Punch:
                    return _chargeDistance > Fix.Zero ? Mathf.Clamp01((float)(_chargeTraveled / _chargeDistance)) : 1f;
                case Move.Slam when !_slamInAir:
                {
                    float windup = Mathf.Max(0.01f, ChadSettings.SlamWindup);
                    float total = windup + Mathf.Max(0.01f, ChadSettings.SlamRecover);
                    return Mathf.Clamp01((float)MoveTime / total);
                }
                case Move.Slam:
                    // Diving: hold the middle of the animation; after the impact, finish it.
                    return _slamLanded
                        ? 0.5f + 0.5f * Mathf.Clamp01((float)(MoveTime - _landedTime) / Mathf.Max(0.01f, ChadSettings.SlamRecover))
                        : Mathf.Clamp01((float)MoveTime / 0.15f) * 0.5f;
                default:
                    return 0f;
            }
        }
    }

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
        if (_caster == null || _player == null || InMode)
            return;

        InMode = true;
        CurrentMove = Move.None;
        for (int i = 0; i < 3; i++)
        {
            _used[i] = false;
            // A button already held (the one that started the mode) needs a new press.
            _wasDown[i] = _player.AbilityButtonIsDown(i);
        }
        _inputWasSuppressed = _caster.suppressInput;
        _gravityWasOn = _caster.playerPhysics.ApplyGravity;
        Active[_caster] = this;
        ChadPatches.MapIcons(this, true);
        GetComponent<ChadVisuals>()?.OnTransform();
        AudioManager.Get()?.Play("transform1");
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        if (!InMode && _auraLeft <= Fix.Zero)
            return;
        if (!CasterUsable())
        {
            EndAura();
            if (InMode)
                Stop();
            return;
        }
        Fix dt = GameTime.FixedDeltaTime(_player.Id, simDeltaTime);

        if (_auraLeft > Fix.Zero)
        {
            // No knockback while shining (ragdolls and pushes are blocked by ChadPatches).
            _caster.body.externalVelocity = Vec2.zero;
            if (dt > Fix.Zero)
                AuraEffects(dt);
            _auraLeft -= dt;
            if (_auraLeft <= Fix.Zero)
                EndAura();
        }
        if (!InMode)
            return;

        // The cooldown starts once the mode is over.
        if (_slot >= 0 && _slot < _caster.abilityCooldownTimers.Length)
            _caster.abilityCooldownTimers[_slot] = Fix.Zero;

        if (CurrentMove == Move.None)
        {
            for (int i = 0; i < 3; i++)
            {
                bool down = _player.AbilityButtonIsDown(i);
                bool pressed = down && !_wasDown[i];
                _wasDown[i] = down;
                if (pressed && !_used[i] && CurrentMove == Move.None && dt > Fix.Zero)
                    StartMove((Move)i);
            }
        }
        else
        {
            for (int i = 0; i < 3; i++)
                _wasDown[i] = _player.AbilityButtonIsDown(i);
        }

        if (CurrentMove != Move.None)
        {
            MoveTime += dt;
            if (CurrentMove == Move.Slam)
                UpdateSlam(dt);
            else if (CurrentMove == Move.Punch)
                UpdateCharge(dt, simDeltaTime);
        }

        if (CurrentMove == Move.None && _used[0] && _used[1] && _used[2])
            Stop();
    }

    private bool CasterUsable() =>
        _caster != null && !_caster.IsDestroyed && _caster.isActiveAndEnabled && !_caster.isInAbility && _player.IsAlive;

    private void StartMove(Move move)
    {
        _used[(int)move] = true;
        _hit.Clear();
        MoveTime = Fix.Zero;
        switch (move)
        {
            case Move.Slam:
                CurrentMove = Move.Slam;
                _slamInAir = !_caster.playerPhysics.IsGrounded();
                _slamLanded = false;
                _caster.suppressInput = true;
                if (_slamInAir)
                {
                    _caster.playerPhysics.ApplyGravity = false;
                    _caster.body.externalVelocity = Vec2.zero;
                }
                AudioManager.Get()?.Play("meteor_jump");
                break;
            case Move.Punch:
            {
                CurrentMove = Move.Punch;
                _caster.suppressInput = true;
                // Along the platform it sticks to (following its surface), or screen-horizontal in the air; to the side
                // moved towards (A/D, left stick; not the aim), else the facing side.
                Vec2 right = Right();
                Fix side = Vec2.Dot(_player.InputVector(), right);
                _punchSign = Fix.Abs(side) >= (Fix)0.2f ? (side > Fix.Zero ? Fix.One : -Fix.One)
                    : (_caster.isFacingRight() ? Fix.One : -Fix.One);
                _chargeDir = right * _punchSign;
                _chargeTraveled = Fix.Zero;
                _chargeDistance = Fix.Max((Fix)ChadSettings.ChargeDistance, Fix.Zero) * Scale;
                _chargeOnGround = _caster.playerPhysics.IsGrounded();
                _chargeLast = _caster.body.position;
                if (!_chargeOnGround)
                {
                    _caster.playerPhysics.ApplyGravity = false;
                    _caster.body.selfImposedVelocity = Vec2.zero;
                }
                _caster.body.externalVelocity = Vec2.zero;
                AudioManager.Get()?.Play("dash");
                break;
            }
            case Move.Aura:
                // Not a move that locks anything: other moves can follow at once.
                _auraLeft = Fix.Max((Fix)ChadSettings.AuraDuration, (Fix)0.01f);
                _caster.playerCollision?.SetInvulnerable(true, AuraImmunityMask);
                Shining.Add(_caster);
                ShiningByPlayer[_player.Id] = this;
                AudioManager.Get()?.Play("abilityPickup");
                break;
        }
    }

    private void EndMove()
    {
        CurrentMove = Move.None;
        MoveTime = Fix.Zero;
        if (_caster != null && !_caster.IsDestroyed)
        {
            _caster.suppressInput = _inputWasSuppressed;
            _caster.playerPhysics.ApplyGravity = _gravityWasOn;
        }
    }

    private void EndAura()
    {
        _auraLeft = Fix.Zero;
        if (_player != null && ShiningByPlayer.TryGetValue(_player.Id, out ChadAbility shining) && shining == this)
            ShiningByPlayer.Remove(_player.Id);
        if (_caster != null)
        {
            Shining.Remove(_caster);
            if (!_caster.IsDestroyed)
                _caster.playerCollision?.SetInvulnerable(false, AuraImmunityMask);
        }
    }

    /// <summary>Back to a slime (also when the caster died or vanished).</summary>
    private void Stop()
    {
        if (CurrentMove != Move.None)
            EndMove();
        InMode = false;
        if (_caster != null)
        {
            Active.Remove(_caster);
            ChadPatches.MapIcons(this, false);
            // A button still held from the last move must not cast the real ability behind it.
            if (!_caster.IsDestroyed)
                ChadPatches.BlockUntilReleased(_caster);
            if (!_caster.IsDestroyed && _caster.isActiveAndEnabled)
            {
                GetComponent<ChadVisuals>()?.OnTransform();
                AudioManager.Get()?.Play("transform2");
            }
        }
    }

    private void OnDestroy()
    {
        if (_caster != null)
        {
            Active.Remove(_caster);
            Shining.Remove(_caster);
        }
        if (_player != null && ShiningByPlayer.TryGetValue(_player.Id, out ChadAbility shining) && shining == this)
            ShiningByPlayer.Remove(_player.Id);
        ChadPatches.MapIcons(this, false);
    }

    // --- Moves ---

    internal Fix Scale => _caster.body.fixtrans.Scale;

    /// <summary>Away from the platform the chad stands on (straight up in the air).</summary>
    private Vec2 Up()
    {
        PlayerPhysics physics = _caster.playerPhysics;
        StickyRoundedRectangle ground = physics.IsGrounded() ? physics.getAttachedGround() : null;
        Vec2 normal = ground != null && !ground.IsDestroyed ? Vec2.NormalizedSafe(ground.currentNormal(_caster.body)) : Vec2.zero;
        return normal != Vec2.zero ? normal : Vec2.up;
    }

    /// <summary>Along the platform, to the slime's right when standing on it (screen right on a floor and in the air).</summary>
    private Vec2 Right()
    {
        Vec2 up = Up();
        return new Vec2(up.y, -up.x);
    }

    private void UpdateSlam(Fix dt)
    {
        PlayerPhysics physics = _caster.playerPhysics;
        if (_slamInAir && !_slamLanded)
        {
            if (physics.IsGrounded())
            {
                _slamLanded = true;
                _landedTime = MoveTime;
                Impact();
            }
            else
            {
                Fix speed = Fix.Max((Fix)ChadSettings.SlamFallSpeed, (Fix)1L) * Scale;
                _caster.body.selfImposedVelocity = Vec2.down * speed;
                _caster.body.externalVelocity = Vec2.zero;
                SquashUnder(Vec2.up);
                // Fell off the level or never lands: give up after a while.
                if (MoveTime > (Fix)2L)
                    EndMove();
            }
            return;
        }
        if (!_slamInAir && !_slamLanded && MoveTime >= (Fix)ChadSettings.SlamWindup)
        {
            _slamLanded = true;
            _landedTime = MoveTime;
            Impact();
        }
        if (_slamLanded && MoveTime - _landedTime >= (Fix)ChadSettings.SlamRecover)
            EndMove();
    }

    private void Impact()
    {
        SquashUnder(Up());
        StickyRoundedRectangle ground = _caster.playerPhysics.IsGrounded() ? _caster.playerPhysics.getAttachedGround() : null;
        if (ground != null && !ground.IsDestroyed)
            ground.GetComponent<ShakablePlatform>()?.AddShake((Fix)0.2f, (Fix)ChadSettings.SlamShake, 0);
        AudioManager.Get()?.Play("meteor_dash");
        GetComponent<ChadVisuals>()?.OnSlamImpact();
    }

    /// <summary>Squashes every other player whose body touches the circle under the chad.</summary>
    /// <summary>
    /// The slam's squash rectangle right now (players whose body touches it are squashed); for Show hitboxes: its
    /// center, its up (away from the platform, straight up in the air), its half width (along the platform) and half
    /// height (along up).
    /// </summary>
    internal bool SlamHitbox(out Vec2 center, out Vec2 up, out Fix halfWidth, out Fix halfHeight)
    {
        center = Vec2.zero;
        up = Vec2.up;
        halfWidth = halfHeight = Fix.Zero;
        if (CurrentMove != Move.Slam || _caster == null || _caster.IsDestroyed)
            return false;
        up = Up();
        SlamBox(up, out center, out halfWidth, out halfHeight);
        return true;
    }

    /// <summary>
    /// The slam's rectangle: <see cref="ChadSettings.SlamWidth"/> radiuses wide along the platform, from the slime's
    /// middle down to <see cref="ChadSettings.SlamDepth"/> radiuses below its bottom, shifted
    /// <see cref="ChadSettings.SlamForwardOffset"/> radiuses towards the facing side.
    /// </summary>
    private void SlamBox(Vec2 up, out Vec2 center, out Fix halfWidth, out Fix halfHeight)
    {
        Fix radius = _caster.playerPhysics.radius;
        Fix height = radius + Fix.Max((Fix)ChadSettings.SlamDepth, Fix.Zero) * radius;
        halfWidth = Fix.Max((Fix)ChadSettings.SlamWidth, (Fix)0.1f) * radius / (Fix)2L;
        halfHeight = height / (Fix)2L;
        // Shifted towards the facing side, where the animation's hands land.
        Vec2 forward = new Vec2(up.y, -up.x) * (_caster.isFacingRight() ? Fix.One : -Fix.One);
        center = _caster.body.position - up * halfHeight + forward * ((Fix)ChadSettings.SlamForwardOffset * radius) +
                 up * ((Fix)ChadSettings.SlamUpOffset * radius);
    }

    /// <summary>The charge's stun rectangle around the chad right now (its height along <paramref name="up"/>); for Show hitboxes.</summary>
    internal bool ChargeHitbox(out Vec2 center, out Vec2 up, out Fix halfLength, out Fix halfHeight)
    {
        center = Vec2.zero;
        up = Vec2.up;
        halfLength = halfHeight = Fix.Zero;
        if (CurrentMove != Move.Punch || _caster == null || _caster.IsDestroyed)
            return false;
        up = Up();
        center = _caster.body.position + ChargeBoxOffset(Right() * _punchSign, up);
        ChargeBoxSize(out halfLength, out halfHeight);
        return true;
    }

    /// <summary>How far the stun rectangle sits from the chad's center: ahead along <paramref name="forward"/>, up along <paramref name="up"/>.</summary>
    private Vec2 ChargeBoxOffset(Vec2 forward, Vec2 up)
    {
        Fix radius = _caster.playerPhysics.radius;
        return forward * ((Fix)ChadSettings.ChargeForwardOffset * radius) + up * ((Fix)ChadSettings.ChargeUpOffset * radius);
    }

    /// <summary>Half the stun rectangle's length (along the charge) and height (across it).</summary>
    private void ChargeBoxSize(out Fix halfLength, out Fix halfHeight)
    {
        Fix radius = _caster.playerPhysics.radius;
        halfLength = Fix.Max((Fix)ChadSettings.ChargeHitLength, (Fix)0.1f) * radius / (Fix)2L;
        halfHeight = Fix.Max((Fix)ChadSettings.ChargeHitHeight, (Fix)0.1f) * radius / (Fix)2L;
    }

    private void SquashUnder(Vec2 up)
    {
        SlamBox(up, out Vec2 center, out Fix halfWidth, out Fix halfHeight);
        Vec2 across = new(up.y, -up.x);
        foreach (PlayerCollision target in Players(_player.Id))
        {
            int id = target.GetPlayerId();
            if (_hit.Contains(id))
                continue;
            // Distance from the body's center to the rectangle, against its radius.
            Fix theirs = target.physics != null ? target.physics.radius : Fix.Zero;
            Vec2 local = target.body.position - center;
            Fix dx = Fix.Max(Fix.Zero, Fix.Abs(Vec2.Dot(local, across)) - halfWidth);
            Fix dy = Fix.Max(Fix.Zero, Fix.Abs(Vec2.Dot(local, up)) - halfHeight);
            if (dx * dx + dy * dy > theirs * theirs)
                continue;
            _hit.Add(id);
            target.killPlayer(_player.Id, spawnEffect: true, ignoreInvulnerability: false, CauseOfDeath.Squished);
        }
    }

    /// <summary>
    /// The charge. Stuck to a platform: it runs along the platform's surface (its grounded speed is driven, so it
    /// follows walls, slopes and rounded corners like walking does); it ends early if it leaves the platform. In the air:
    /// straight sideways (the slime's own velocity stays 0, like the Charge skill's dash), a platform ahead stops it (the
    /// game's landing raycasts attach the slime). Slimes on the way are stunned.
    /// </summary>
    private void UpdateCharge(Fix dt, Fix simDeltaTime)
    {
        if (dt <= Fix.Zero)
            return;
        PlayerPhysics physics = _caster.playerPhysics;
        _caster.body.externalVelocity = Vec2.zero;
        Fix speed = Fix.Max((Fix)ChadSettings.ChargeSpeed, (Fix)1L) * Scale;

        if (_chargeOnGround)
        {
            // Distance actually covered since last tick (the game moves the slime along the surface).
            Vec2 now = _caster.body.position;
            StunAlong(_chargeLast, now);
            _chargeTraveled += Vec2.Magnitude(now - _chargeLast);
            _chargeLast = now;
            bool done = _chargeTraveled >= _chargeDistance || MoveTime > _chargeDistance / speed * (Fix)2L;
            if (!physics.IsGrounded() || done)
            {
                if (physics.IsGrounded())
                    physics.groundedSpeed = _punchSign * speed * (Fix)0.3f;
                EndMove();
                return;
            }
            physics.groundedSpeed = _punchSign * speed;
            return;
        }

        _caster.body.selfImposedVelocity = Vec2.zero;
        Fix step = Fix.Min(speed * dt, _chargeDistance - _chargeTraveled);
        Vec2 from = _caster.body.position;
        bool landed = !physics.IsGrounded() && step > Fix.Zero &&
                      physics.CollisionRaycasts(attachToGroundIfHit: true, _chargeDir * (step / dt), simDeltaTime);
        Vec2 to = landed ? _caster.body.position : from + _chargeDir * step;
        StunAlong(from, to);
        if (landed || physics.IsGrounded())
        {
            AudioManager.Get()?.Play("rockBounce");
            EndMove();
            return;
        }
        _caster.body.position = to;
        _chargeTraveled += step;
        if (_chargeTraveled >= _chargeDistance)
        {
            // A bit of the speed is kept, then gravity takes over again.
            _caster.body.selfImposedVelocity = _chargeDir * (speed * (Fix)0.3f);
            EndMove();
        }
    }

    /// <summary>Stuns every other slime the chad touches on its way from → to (once per charge).</summary>
    private void StunAlong(Vec2 from, Vec2 to)
    {
        // The rectangle swept from → to: as long as the move plus its own length, as high as it is.
        ChargeBoxSize(out Fix halfLength, out Fix halfHeight);
        // The way it moved; for a tiny (or no) move, the charge's own direction. NormalizedSafe returns a near-zero
        // vector unchanged, and a ~0 axis would put every slime on the map "inside" the box.
        Vec2 moved = to - from;
        Vec2 axis = Vec2.SqrMagnitude(moved) > (Fix)0.0001f ? Vec2.NormalizedSafe(moved) : Right() * _punchSign;
        Vec2 across = new(-axis.y, axis.x);
        Vec2 center = (from + to) / (Fix)2L + ChargeBoxOffset(axis, Up());
        halfLength += Vec2.Magnitude(to - from) / (Fix)2L;
        Fix duration = Fix.Max((Fix)ChadSettings.StunDuration, Fix.Zero);
        foreach (PlayerCollision target in Players(_player.Id))
        {
            int id = target.GetPlayerId();
            if (_hit.Contains(id) || target.GetComponent<SlimeController>() is not SlimeController slime || !slime.isActiveAndEnabled)
                continue;
            Fix theirs = target.physics != null ? target.physics.radius : Fix.Zero;
            Vec2 local = target.body.position - center;
            Fix dx = Fix.Max(Fix.Zero, Fix.Abs(Vec2.Dot(local, axis)) - halfLength);
            Fix dy = Fix.Max(Fix.Zero, Fix.Abs(Vec2.Dot(local, across)) - halfHeight);
            if (dx * dx + dy * dy > theirs * theirs)
                continue;
            _hit.Add(id);
            if (ChadStun.Apply(slime, duration))
                AudioManager.Get()?.Play("rockBounce");
        }
    }

    // --- Aura farm ---

    /// <summary>Radius of the shining area (reflects projectiles and beams, touches black holes).</summary>
    internal Fix AuraReach => Fix.Max((Fix)ChadSettings.AuraSize, (Fix)0.1f) * _caster.playerPhysics.radius;

    /// <summary>Where the shining area is centered (the slime's body).</summary>
    internal Vec2 AuraCenter => _caster.body.position;

    /// <summary>Every tick while shining: reflect projectiles, push smoke away, turn black holes white.</summary>
    private void AuraEffects(Fix dt)
    {
        Vec2 center = AuraCenter;
        Fix reach = AuraReach;

        // Projectiles and items coming at the shining area bounce off it and become ours.
        foreach (BoplBody body in Bodies())
        {
            Vec2 offset = body.position - center;
            Fix distance = Vec2.Magnitude(offset);
            Fix radius = body.physicsCollider is DPhysicsCircle circle ? circle.radius : body.Scale / (Fix)2L;
            if (distance > reach + radius || distance <= Fix.Zero)
                continue;
            Vec2 normal = offset / distance;
            Vec2 velocity = body.velocity;
            Fix along = Vec2.Dot(velocity, normal);
            if (along >= Fix.Zero)
                continue;
            // Bounced back off the area, faster by ReflectPower (at least that × its incoming speed straight out).
            Fix power = Fix.Max((Fix)ChadSettings.ReflectPower, Fix.Zero);
            Vec2 reflected = (velocity - normal * ((Fix)2L * along)) * power;
            body.velocity = reflected;
            body.position = center + normal * (reach + radius);
            if (body.GetComponent<Missile>() != null && reflected != Vec2.zero)
                body.up = Vec2.NormalizedSafe(reflected);
            if (body.GetComponent<Item>() is Item item)
                item.OwnerId = _player.Id;
            if (body.GetComponent<Grenade>() is Grenade grenade)
            {
                grenade.DetonatesOnOwner = false;
                grenade.DelayedEnableHurtOwner((Fix)0.3f);
            }
            if (body.GetComponent<Projectile>() is Projectile shot)
                shot.PlayerOwnerId = _player.Id;
            AudioManager.Get()?.Play("hookshotOnHit");
        }

        // Smoke nearby drifts away.
        Fix smokeRadius = Fix.Max((Fix)ChadSettings.SmokeRadius, Fix.Zero) * Scale;
        Fix smokeStep = Fix.Max((Fix)ChadSettings.SmokePush, Fix.Zero) * dt;
        foreach (FlammableSmoke smoke in Smokes())
        {
            Vec2 offset = smoke.fixTrans.position - center;
            if (Vec2.SqrMagnitude(offset) > smokeRadius * smokeRadius)
                continue;
            Vec2 away = Vec2.NormalizedSafe(offset);
            smoke.fixTrans.position += (away == Vec2.zero ? Vec2.up : away) * smokeStep;
        }

        // Black holes touching the shining area turn into white holes (negative mass, like a shrunk one).
        foreach (BlackHole hole in BlackHoles())
        {
            Fix holeRadius = hole.dCircle.radius;
            Fix touch = reach + holeRadius;
            if (Vec2.SqrMagnitude(hole.dCircle.position - center) > touch * touch || hole.GetMass() <= Fix.Zero)
                continue;
            hole.SetMass(-hole.GetMass());
            AudioManager.Get()?.Play("shrink");
        }
    }

    /// <summary>Flying bodies (projectiles, items), in an order that's the same on every machine (the Bat's filter).</summary>
    private static List<BoplBody> Bodies() =>
        FindObjectsOfType<BoplBody>()
            .Where(b => !b.IsDestroyed && b.gameObject.activeInHierarchy && b.HasBeenInitialized &&
                        b.GetComponent<PlayerCollision>() == null && b.GetComponent<AbilityMonoBehaviour>() == null &&
                        b.GetComponent<StickyRoundedRectangle>() == null && b.GetComponent<FlammableSmoke>() == null &&
                        b.GetComponent<BlackHole>() == null &&
                        (b.GetComponent<Grenade>() is not Grenade g || g.hasBeenThrown))
            .OrderBy(b => b.HierarchyNumber)
            .ToList();

    private static List<FlammableSmoke> Smokes() =>
        FindObjectsOfType<FlammableSmoke>()
            .Where(s => !s.IsDestroyed && s.gameObject.activeInHierarchy && s.fixTrans != null)
            .OrderBy(s => s.fixTrans.position.x.m_rawValue)
            .ThenBy(s => s.fixTrans.position.y.m_rawValue)
            .ToList();

    private static List<BlackHole> BlackHoles() =>
        FindObjectsOfType<BlackHole>()
            .Where(h => !h.IsDestroyed && h.gameObject.activeInHierarchy && h.dCircle != null)
            .OrderBy(h => h.dCircle.position.x.m_rawValue)
            .ThenBy(h => h.dCircle.position.y.m_rawValue)
            .ToList();

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

    public override void ExitAbility(AbilityExitInfo info)
    {
    }

    public override Fix GetCooldown() => (Fix)ChadSettings.AbilityCooldown;

    public override bool IsInstant() => true;

    public override bool IsCastable(Player player) => !InMode;

    public override void UpdateCooldowns(PlayerBody playerBody)
    {
    }

    public override PlayerCollision GetPlayerCollision() => null;

    public override SlimeController GetSlimeController() => _caster;
}
