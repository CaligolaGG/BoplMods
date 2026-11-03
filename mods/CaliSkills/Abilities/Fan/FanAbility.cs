using System;
using System.Collections.Generic;
using System.Linq;
using BoplFixedMath;
using UnityEngine;

namespace CaliSkills.Abilities.Fan;

/// <summary>
/// Fan: an instant ability (the game keeps the slime; it only looks like a fan, see <see cref="FanVisuals"/>).
/// - On use the slime turns into an electric fan (<see cref="FanSettings.TransformTime"/>). Like the Beam: it can't
///   move, jump or use abilities (input suppressed), it isn't affected by gravity (in the air it stops and hovers, only
///   pushes move it; on a platform it stays on it), and it turns towards where it aims
///   (<see cref="FanSettings.RotationSpeed"/>), starting from where it faced.
/// - Then it blows for <see cref="FanSettings.Duration"/>: a trapezoid of wind in front of it (short side at the fan),
///   pushing everything in it away every tick, strongest at the fan and fading to the far end: other players (slimes
///   and ability forms), projectiles and items (anything with a body: arrows, grenades, mines, pinballs...), platforms
///   (a share of the push) and smoke clouds (they drift).
/// - Then it turns back into a slime and the cooldown starts (its timer is pinned to 0 until then).
/// Sim code: inputs, Fix math and objects in a fixed order; the fan and wind are visuals drawn from this state.
/// </summary>
public class FanAbility : AbilityMonoBehaviour
{
    internal enum State
    {
        Idle,
        Transforming,
        Blowing,
        Returning,
    }

    private SlimeController _caster;
    private Player _player;
    private int _slot = -1;
    private bool _inputWasSuppressed;
    private bool _gravityWasOn = true;

    internal State CurrentState { get; private set; }

    /// <summary>Seconds in the current state.</summary>
    internal Fix StateTime { get; private set; }

    /// <summary>Where the fan blows (unit vector).</summary>
    internal Vec2 Direction { get; private set; } = Vec2.right;

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
        _inputWasSuppressed = _caster.suppressInput;
        _caster.suppressInput = true;
        // No gravity, like the Beam: in the air the fan stops where it is.
        _gravityWasOn = _caster.playerPhysics.ApplyGravity;
        _caster.playerPhysics.ApplyGravity = false;
        if (!_caster.playerPhysics.IsGrounded())
            _caster.body.selfImposedVelocity = Vec2.zero;
        SetState(State.Transforming);
        GetComponent<FanVisuals>()?.OnTransform(toFan: true);
        AudioManager.Get()?.Play("abilityPickup");
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

        // The cooldown starts once the fan is a slime again.
        if (_slot >= 0 && _slot < _caster.abilityCooldownTimers.Length)
            _caster.abilityCooldownTimers[_slot] = Fix.Zero;
        _caster.suppressInput = true;
        _caster.playerPhysics.ApplyGravity = false;

        Fix dt = GameTime.FixedDeltaTime(_player.Id, simDeltaTime);
        StateTime += dt;
        Turn(dt);

        switch (CurrentState)
        {
            case State.Transforming when StateTime >= (Fix)FanSettings.TransformTime:
                SetState(State.Blowing);
                break;
            case State.Blowing:
                Blow(dt);
                if (StateTime >= (Fix)FanSettings.Duration)
                {
                    SetState(State.Returning);
                    GetComponent<FanVisuals>()?.OnTransform(toFan: false);
                }
                break;
            case State.Returning when StateTime >= (Fix)FanSettings.TransformTime:
                Stop();
                break;
        }
    }

    private bool CasterUsable() =>
        _caster != null && !_caster.IsDestroyed && _caster.isActiveAndEnabled && !_caster.isInAbility && _player.IsAlive;

    /// <summary>Back to a slime (also when the caster died or vanished).</summary>
    private void Stop()
    {
        if (_caster != null && !_caster.IsDestroyed)
        {
            _caster.suppressInput = _inputWasSuppressed;
            _caster.playerPhysics.ApplyGravity = _gravityWasOn;
        }
        SetState(State.Idle);
    }

    /// <summary>Turns towards the aim (mouse / stick, else movement), at most RotationSpeed degrees per second.</summary>
    private void Turn(Fix dt)
    {
        Vec2 aim = Vec2.NormalizedSafe(_player.AimVector());
        if (aim == Vec2.zero)
            aim = Vec2.NormalizedSafe(_player.InputVector());
        if (aim == Vec2.zero)
            return;

        Vec2 dir = Direction;
        Fix cross = dir.x * aim.y - dir.y * aim.x;
        Fix dot = Vec2.Dot(dir, aim);
        Fix angle = Fix.Atan2(cross, dot);
        Fix maxStep = (Fix)FanSettings.RotationSpeed * Fix.Pi / (Fix)180L * dt;
        Fix step = Fix.Max(-maxStep, Fix.Min(maxStep, angle));
        Fix cos = Fix.Cos(step), sin = Fix.Sin(step);
        Direction = Vec2.NormalizedSafe(new Vec2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos));
        if (Direction == Vec2.zero)
            Direction = aim;
    }

    // --- The wind area: a trapezoid from the fan's front, along Direction ---

    internal Fix Scale => _caster.body.fixtrans.Scale;

    /// <summary>Away from the platform the fan stands on (straight up in the air).</summary>
    internal Vec2 Up
    {
        get
        {
            PlayerPhysics physics = _caster.playerPhysics;
            StickyRoundedRectangle ground = physics.IsGrounded() ? physics.getAttachedGround() : null;
            Vec2 normal = ground != null && !ground.IsDestroyed ? Vec2.NormalizedSafe(ground.currentNormal(_caster.body)) : Vec2.zero;
            return normal != Vec2.zero ? normal : Vec2.up;
        }
    }

    /// <summary>
    /// The fan's head (motor): its stand's foot (0.4 fan sizes below it) rests where the slime's bottom was, so the
    /// bigger fan stands on the floor instead of sinking into it.
    /// </summary>
    internal Vec2 HeadPosition
    {
        get
        {
            Fix radius = _caster.playerPhysics.radius;
            Fix size = Fix.Max((Fix)FanSettings.FanSize, (Fix)0.1f) * radius;
            return _caster.body.position + Up * (size * (Fix)0.4f - radius);
        }
    }

    /// <summary>The short side's middle: at the fan's cage.</summary>
    internal Vec2 WindOrigin =>
        HeadPosition + Direction * (Fix.Max((Fix)FanSettings.FanSize, (Fix)0.1f) * _caster.playerPhysics.radius * (Fix)0.2f);

    internal Fix WindLength => Fix.Max((Fix)FanSettings.WindLength, (Fix)0.1f) * Scale;

    /// <summary>Half the wind's width at <paramref name="along"/> 0..1 (fan → far end).</summary>
    internal Fix HalfWidth(Fix along01) =>
        Lerp((Fix)FanSettings.WindWidthNear, (Fix)FanSettings.WindWidthFar, along01) * Scale / (Fix)2L;

    private static Fix Lerp(Fix a, Fix b, Fix t) => a + (b - a) * t;

    /// <summary>Where <paramref name="point"/> is in the wind (0 at the fan, 1 at the far end), or -1 if it isn't in it.</summary>
    private Fix InWind(Vec2 point, Fix margin)
    {
        Vec2 local = point - WindOrigin;
        Fix length = WindLength;
        Fix along = Vec2.Dot(local, Direction);
        if (along < -margin || along > length + margin)
            return -Fix.One;
        Fix along01 = Fix.Max(Fix.Zero, Fix.Min(Fix.One, along / length));
        Fix side = Vec2.Dot(local, new Vec2(-Direction.y, Direction.x));
        return Fix.Abs(side) <= HalfWidth(along01) + margin ? along01 : -Fix.One;
    }

    /// <summary>Speed the wind adds this tick at <paramref name="along01"/>.</summary>
    private Vec2 Push(Fix along01, Fix dt) =>
        Direction * (Lerp((Fix)FanSettings.StrengthNear, (Fix)FanSettings.StrengthEdge, along01) * dt);

    private void Blow(Fix dt)
    {
        int owner = _player.Id;

        // Players: slimes and ability forms (not the fan's owner).
        foreach (PlayerCollision target in Players(owner))
        {
            Fix radius = target.physics != null ? target.physics.radius : Fix.Zero;
            Fix along = InWind(target.body.position, radius);
            if (along < Fix.Zero)
                continue;
            if (target.GetComponent<Ability>() is Ability form && !form.isPushable)
                continue;
            PushPlayer(target.gameObject, Push(along, dt));
        }

        // Projectiles and items: anything flying with a body.
        foreach (BoplBody body in Bodies())
        {
            Fix along = InWind(body.position, Fix.Zero);
            if (along >= Fix.Zero)
                body.velocity += Push(along, dt);
        }

        // Platforms that can move: a share of the push, where the wind hits them.
        Fix platformShare = (Fix)FanSettings.PlatformPush;
        if (platformShare > Fix.Zero)
        {
            foreach (StickyRoundedRectangle platform in Platforms())
            {
                if (!PlatformInWind(platform, out Vec2 point, out Fix along))
                    continue;
                DPhysicsRoundedRect collider = platform.GetComponent<DPhysicsRoundedRect>();
                if (collider == null)
                    continue;
                PhysicsBody physics = collider.GetBody();
                if (physics.inverseMass <= Fix.Zero)
                    continue;
                collider.AddForceAtPosition(Push(along, dt) * platformShare / physics.inverseMass, point, ForceMode2D.Force);
            }
        }

        // Smoke clouds drift with the wind.
        Fix smokeShare = (Fix)FanSettings.SmokeDrift;
        if (smokeShare > Fix.Zero)
        {
            foreach (FlammableSmoke smoke in Smokes())
            {
                Fix along = InWind(smoke.fixTrans.position, Fix.Zero);
                if (along >= Fix.Zero)
                    smoke.fixTrans.position += Push(along, dt) * smokeShare;
            }
        }
    }

    /// <summary>A platform is in the wind if its closest point to some point along the wind's axis is.</summary>
    private bool PlatformInWind(StickyRoundedRectangle platform, out Vec2 point, out Fix along)
    {
        for (int i = 0; i <= 4; i++)
        {
            Vec2 probe = WindOrigin + Direction * (WindLength * (Fix)(long)i / (Fix)4L);
            point = platform.GetClosestPoint(probe);
            along = InWind(point, (Fix)0.1f);
            if (along >= Fix.Zero)
                return true;
        }
        point = Vec2.zero;
        along = -Fix.One;
        return false;
    }

    /// <summary>
    /// Pushes a player's body. Slimes: in the air, added to their speed; stuck to a platform, they slide along it, and
    /// leave it when the wind blows them off it (mostly away from the surface).
    /// </summary>
    private void PushPlayer(GameObject target, Vec2 change)
    {
        PlayerPhysics physics = target.GetComponent<PlayerPhysics>();
        PlayerBody body = target.GetComponent<PlayerBody>();
        if (physics != null && body != null)
        {
            StickyRoundedRectangle ground = physics.IsGrounded() ? physics.getAttachedGround() : null;
            if (ground == null || ground.IsDestroyed)
            {
                body.externalVelocity += change;
                return;
            }
            Vec2 normal = Vec2.NormalizedSafe(ground.currentNormal(body));
            var tangent = new Vec2(normal.y, -normal.x);
            if (Vec2.Dot(Direction, normal) > (Fix)0.5f)
            {
                // Blown off the platform.
                Vec2 velocity = tangent * physics.groundedSpeed + change;
                physics.UnGround(nullRotation: true, playSoundEffect: false);
                body.selfImposedVelocity = velocity;
                body.externalVelocity = Vec2.zero;
                return;
            }
            physics.groundedSpeed += Vec2.Dot(change, tangent);
            return;
        }
        if (body != null)
        {
            body.externalVelocity += change;
            return;
        }
        BoplBody bopl = target.GetComponent<BoplBody>();
        if (bopl != null)
            bopl.velocity += change;
    }

    /// <summary>Other players' active bodies, in an order that's the same on every machine.</summary>
    private static List<PlayerCollision> Players(int owner) =>
        FindObjectsOfType<PlayerCollision>()
            .Where(b => b.gameObject.activeInHierarchy && b.body != null && b.GetPlayerId() != owner &&
                        PlayerHandler.Get().GetPlayer(b.GetPlayerId()) is Player p && p.IsAlive &&
                        (b.GetComponent<SlimeController>() is not SlimeController s || s.hasSpawned))
            .OrderBy(b => b.GetPlayerId())
            .ThenBy(b => b.body.position.x.m_rawValue)
            .ThenBy(b => b.body.position.y.m_rawValue)
            .ToList();

    /// <summary>
    /// Flying bodies (projectiles, items), in an order that's the same on every machine: not players, platforms, smoke,
    /// black holes or items still in a hand.
    /// </summary>
    private static List<BoplBody> Bodies() =>
        FindObjectsOfType<BoplBody>()
            .Where(b => !b.IsDestroyed && b.gameObject.activeInHierarchy && b.HasBeenInitialized &&
                        b.GetComponent<PlayerCollision>() == null && b.GetComponent<AbilityMonoBehaviour>() == null &&
                        b.GetComponent<StickyRoundedRectangle>() == null && b.GetComponent<FlammableSmoke>() == null &&
                        b.GetComponent<BlackHole>() == null &&
                        (b.GetComponent<Grenade>() is not Grenade g || g.hasBeenThrown))
            .OrderBy(b => b.HierarchyNumber)
            .ToList();

    private static List<StickyRoundedRectangle> Platforms() =>
        FindObjectsOfType<StickyRoundedRectangle>()
            .Where(p => !p.IsDestroyed && p.gameObject.activeInHierarchy)
            .OrderBy(p => p.transform.GetSiblingIndex())
            .ThenBy(p => p.GetComponent<FixTransform>() is FixTransform t ? t.position.x.m_rawValue : 0L)
            .ThenBy(p => p.GetComponent<FixTransform>() is FixTransform t ? t.position.y.m_rawValue : 0L)
            .ToList();

    private static List<FlammableSmoke> Smokes() =>
        FindObjectsOfType<FlammableSmoke>()
            .Where(s => !s.IsDestroyed && s.gameObject.activeInHierarchy && s.fixTrans != null)
            .OrderBy(s => s.fixTrans.position.x.m_rawValue)
            .ThenBy(s => s.fixTrans.position.y.m_rawValue)
            .ToList();

    public override void ExitAbility(AbilityExitInfo info)
    {
    }

    public override Fix GetCooldown() => (Fix)FanSettings.AbilityCooldown;

    public override bool IsInstant() => true;

    public override bool IsCastable(Player player) => CurrentState == State.Idle;

    public override void UpdateCooldowns(PlayerBody playerBody)
    {
    }

    public override PlayerCollision GetPlayerCollision() => null;

    public override SlimeController GetSlimeController() => _caster;
}
