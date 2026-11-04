using System;
using System.Collections.Generic;
using System.Linq;
using BoplFixedMath;
using UnityEngine;

namespace CaliSkills.Abilities.Bat;

/// <summary>
/// Wooden Bat: an instant ability (the slime stays a slime).
/// - Pressed and held: the bat is raised behind the slime, aimed with the movement input (like the Bow). The cooldown
///   waits (its timer is pinned to 0 until the swing ends).
/// - Released: the bat sweeps <see cref="BatSettings.SwingArc"/> degrees through the aimed direction in
///   <see cref="BatSettings.SwingTime"/> seconds, top to bottom on the side the slime aims at, top and bottom in the
///   slime's own frame (upside down on a ceiling, it swings towards the ceiling).
/// The bat is a solid moving capsule from the slime's center outwards, simulated here (not a game platform, so nothing
/// sticks to it):
/// - other players' bodies it touches are pushed out of it and knocked along the swing (once per swing, a share of the
///   bat's speed there) — unless a platform is right behind them: then they're squished between the two (with
///   <see cref="BatSettings.NoSquashing"/>: only knocked, and the bat passes through them);
/// - flying projectiles it touches (any thrown/shot BoplBody: arrows, grenades, pinballs, missiles...) are hit back and
///   become the slime's (<see cref="BatSettings.DeflectProjectiles"/>);
/// - if the bat hits a platform, the swing stops there and the slime bounces away from it (pogo);
/// - the slime holding it is never hit by it.
/// Sim code: inputs and Fix math only, bodies in a fixed order. The bat sprite is a visual drawn from the sim state.
/// Skill vs ability: this class is the behaviour in a match (one copy per player per round, run by the game's
/// simulation); its definition (name, icon, prefab, offensive flag) is <see cref="BatSkill"/>.
/// </summary>
public class BatAbility : AbilityMonoBehaviour
{
    private enum State
    {
        Idle,
        Holding,
        Swinging,
    }

    /// <summary>Seconds a deflected item (grenade, pinball...) spares its new owner, the batter.</summary>
    private static readonly Fix OwnerGraceTime = (Fix)0.3f;

    /// <summary>
    /// The swing is checked in sub-steps so the bat's tip moves at most this far (units) between two checks: a fast
    /// swing would otherwise jump over players, or reach the floor (pogo) in the same tick it reaches a player on it.
    /// </summary>
    private static readonly Fix MaxTipStep = (Fix)0.35f;
    private const int MaxSubSteps = 32;

    /// <summary>
    /// Squished when, pushed out of the bat, a player would sink this share of its radius into a platform: there's no
    /// room to get out of the way. (A slime standing on a floor touches it already; less than this is just a shove.)
    /// </summary>
    private static readonly Fix SquishDepth01 = (Fix)0.3f;

    private SlimeController _caster;
    private Player _player;
    private int _slot = -1;
    private State _state;

    // Swing: bat direction = _start rotated by _sign * _swept.
    private Vec2 _start;
    private Fix _sign;
    private Fix _swept;
    private Fix _arc;
    private Fix _speed;
    private Vec2 _dir;
    private readonly HashSet<int> _knocked = new();
    private readonly HashSet<int> _squished = new();
    private readonly HashSet<int> _deflected = new();
    private PhysicsParent[] _platformHits = new PhysicsParent[4];

    private SpriteRenderer _visual;

    public override bool IsRope => false;
    public override bool CooldownStoppedDuringTimeStop => false;
    public override bool IgnoreNextCooldown => false;

    private void Awake()
    {
        // Same as the game's InstantAbility: abilities are simulation objects driven by Updater.
        Updater.RegisterUpdatable(this);

        var visual = new GameObject("WoodenBat");
        visual.transform.SetParent(transform, worldPositionStays: false);
        _visual = visual.AddComponent<SpriteRenderer>();
        _visual.sprite = BatSkill.BatSprite;
        _visual.enabled = false;
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
        if (_caster == null || _player == null || _state != State.Idle)
            return;
        _state = State.Holding;
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        if (_state == State.Idle)
            return;
        if (!CasterUsable())
        {
            _state = State.Idle;
            return;
        }

        // The cooldown starts when the swing is over.
        if (_slot >= 0 && _slot < _caster.abilityCooldownTimers.Length)
            _caster.abilityCooldownTimers[_slot] = Fix.Zero;

        if (_state == State.Holding)
        {
            if (!_player.AbilityButtonIsDown(_slot))
                StartSwing();
            return;
        }

        Fix dt = GameTime.FixedDeltaTime(_player.Id, simDeltaTime);
        Fix from = _swept;
        Fix step = Fix.Min(_swept + _speed * dt, _arc) - from;
        Fix tipTravel = step * (Fix)BatSettings.BatLength * Scale;
        int steps = 1;
        while (steps < MaxSubSteps && tipTravel > MaxTipStep * (Fix)(long)steps)
            steps++;

        List<PlayerCollision> targets = Targets();
        List<BoplBody> projectiles = BatSettings.DeflectProjectiles ? Projectiles() : null;
        for (int i = 1; i <= steps; i++)
        {
            _swept = from + step * (Fix)(long)i / (Fix)(long)steps;
            _dir = Rotate(_start, _sign * _swept);
            // Players and projectiles first: one caught between the bat and the floor is squished in the same step
            // the bat reaches the floor.
            if (projectiles != null)
                DeflectProjectiles(projectiles);
            HitPlayers(targets);
            if (Pogo())
                return;
        }
        if (_swept >= _arc)
            _state = State.Idle;
    }

    private bool CasterUsable() =>
        _caster != null && !_caster.IsDestroyed && _caster.isActiveAndEnabled && !_caster.isInAbility && _player.IsAlive;

    /// <summary>
    /// Aim direction (input, else facing), and which way round the bat goes: from the slime's top to its bottom on that
    /// side, in the slime's own frame (its up = the platform's normal, or its rotation in the air). A slime upside down
    /// on a ceiling raises the bat away from the ceiling and swings it into the ceiling, as a standing slime swings into
    /// the floor, instead of starting the swing inside the platform.
    /// </summary>
    private void Aim(out Vec2 aim, out Fix sign)
    {
        Vec2 up = SlimeUp();
        var forward = new Vec2(up.y, -up.x); // the slime's right
        bool facingRight = _caster.isFacingRight();
        aim = Vec2.NormalizedSafe(_player.AimVector());
        if (aim == Vec2.zero)
            aim = facingRight ? forward : -forward;
        // Up on the aim's left (counter-clockwise of it) = go clockwise, like aiming right when standing.
        Fix cross = aim.x * up.y - aim.y * up.x;
        bool clockwise = cross > Fix.Zero || (cross == Fix.Zero && facingRight);
        sign = clockwise ? -Fix.One : Fix.One;
    }

    /// <summary>The slime's up: away from the platform it's stuck to, else its own rotation (world up as a fallback).</summary>
    private Vec2 SlimeUp()
    {
        PlayerPhysics physics = _caster.playerPhysics;
        StickyRoundedRectangle ground = physics.IsGrounded() ? physics.getAttachedGround() : null;
        Vec2 up = ground != null && !ground.IsDestroyed ? ground.currentNormal(_caster.body) : _caster.body.up;
        up = Vec2.NormalizedSafe(up);
        return up == Vec2.zero ? Vec2.up : up;
    }

    private void StartSwing()
    {
        Aim(out Vec2 aim, out _sign);
        _arc = Fix.Max((Fix)BatSettings.SwingArc, Fix.One) * Fix.Pi / (Fix)180L;
        _start = Rotate(aim, -_sign * _arc / (Fix)2L);
        _speed = _arc / Fix.Max((Fix)BatSettings.SwingTime, (Fix)0.02f);
        _swept = Fix.Zero;
        _dir = _start;
        _knocked.Clear();
        _squished.Clear();
        _deflected.Clear();
        _state = State.Swinging;
        AudioManager.Get()?.Play("dash");
    }

    private static Vec2 Rotate(Vec2 v, Fix angle)
    {
        Fix cos = Fix.Cos(angle), sin = Fix.Sin(angle);
        return new Vec2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    private static Vec2 Perpendicular(Vec2 v) => new(-v.y, v.x);

    private Fix Scale => _caster.body.fixtrans.Scale;

    /// <summary>The bat: from the slime's hand (half its radius out) to the tip.</summary>
    private void Segment(Vec2 dir, out Vec2 from, out Vec2 to)
    {
        Vec2 center = _caster.body.position;
        from = center + dir * (_caster.playerPhysics.radius / (Fix)2L);
        to = center + dir * ((Fix)BatSettings.BatLength * Scale);
    }

    /// <summary>The bat hit a platform: the swing ends and the slime bounces away from it.</summary>
    private bool Pogo()
    {
        Segment(_dir, out Vec2 from, out Vec2 to);
        Fix length = Vec2.Magnitude(to - from);
        RaycastInformation hit = DetPhysics.Get().RaycastToClosestRoundedRect(from, _dir, length);
        if (!hit)
            return false;

        AddVelocity(_caster.playerPhysics, _caster.body, -_dir * (Fix)BatSettings.PogoSpeed);
        _state = State.Idle;
        AudioManager.Get()?.Play("spikeRock");
        return true;
    }

    private void HitPlayers(List<PlayerCollision> targets)
    {
        Segment(_dir, out Vec2 from, out Vec2 to);
        Vec2 center = _caster.body.position;
        Fix thickness = (Fix)BatSettings.BatThickness * Scale;
        Fix angularSpeed = _sign * _speed;

        foreach (PlayerCollision target in targets)
        {
            if (target == null || target.IsDestroyed || !target.gameObject.activeInHierarchy || _squished.Contains(target.GetPlayerId()))
                continue;
            Vec2 c = target.body.position;
            Fix radius = target.physics != null ? target.physics.radius : Scale;
            Vec2 closest = ClosestOnSegment(from, to, c);
            Vec2 offset = c - closest;
            Fix distance = Vec2.Magnitude(offset);
            Fix overlap = radius + thickness - distance;
            if (overlap <= Fix.Zero)
                continue;

            // Bat speed where it touches (rotation around the slime's center); only its leading side pushes.
            Vec2 batVelocity = Perpendicular(closest - center) * angularSpeed;
            Vec2 normal = distance > Fix.Zero ? offset / distance : Vec2.NormalizedSafe(batVelocity);
            Fix approach = Vec2.Dot(batVelocity, normal);
            if (approach <= Fix.Zero || normal == Vec2.zero)
                continue;

            // Caught between the bat and a platform: out of the bat, they'd be pushed into it.
            bool caught = DetPhysics.Get().circleCastRoundedRect(c + normal * overlap, radius * (Fix.One - SquishDepth01), ref _platformHits) > 0;
            if (caught && !BatSettings.NoSquashing)
            {
                if (target.killPlayer(_player.Id, spawnEffect: true, ignoreInvulnerability: false, CauseOfDeath.Squished))
                    _squished.Add(target.GetPlayerId());
                continue;
            }

            int id = target.GetPlayerId();
            PlayerPhysics physics = target.GetComponent<PlayerPhysics>();
            if (_knocked.Add(id))
            {
                Vec2 knock = normal * Fix.Max(approach * (Fix)BatSettings.HitKnock, (Fix)BatSettings.MinKnock);
                if (physics != null)
                    AddVelocity(physics, target.body, knock);
                else
                    target.body.externalVelocity += knock;
                AudioManager.Get()?.Play("hookshotOnHit");
            }
            // Solid: out of the bat. Caught (and not squishable): the bat goes through them instead.
            if (!caught)
                target.body.position += normal * overlap;
        }
    }

    /// <summary>
    /// Projectiles touching the bat's leading side bounce off it: their speed across the bat is reversed and at least
    /// <see cref="BatSettings.DeflectPower"/> × the bat's speed there, their speed along it is kept. Once per swing each
    /// one plays a hit sound and changes owner to this slime (so it can hit the one who threw it, not us).
    /// </summary>
    private void DeflectProjectiles(List<BoplBody> projectiles)
    {
        Segment(_dir, out Vec2 from, out Vec2 to);
        Vec2 center = _caster.body.position;
        Fix thickness = (Fix)BatSettings.BatThickness * Scale;
        Fix angularSpeed = _sign * _speed;

        foreach (BoplBody projectile in projectiles)
        {
            if (projectile == null || projectile.IsDestroyed)
                continue;
            Vec2 p = projectile.position;
            Fix radius = projectile.physicsCollider is DPhysicsCircle circle ? circle.radius : projectile.Scale / (Fix)2L;
            Vec2 closest = ClosestOnSegment(from, to, p);
            Vec2 offset = p - closest;
            Fix distance = Vec2.Magnitude(offset);
            Fix overlap = radius + thickness - distance;
            if (overlap <= Fix.Zero)
                continue;

            Vec2 batVelocity = Perpendicular(closest - center) * angularSpeed;
            Vec2 normal = distance > Fix.Zero ? offset / distance : Vec2.NormalizedSafe(batVelocity);
            if (normal == Vec2.zero)
                continue;
            Vec2 velocity = projectile.velocity;
            // Only when they close in on each other (a projectile already flying away faster than the bat is left alone).
            if (Vec2.Dot(velocity - batVelocity, normal) >= Fix.Zero)
                continue;

            Fix across = Vec2.Dot(velocity, normal);
            Fix outward = Fix.Max(Fix.Abs(across), Vec2.Dot(batVelocity, normal) * (Fix)BatSettings.DeflectPower);
            Vec2 deflected = velocity + normal * (outward - across);
            projectile.velocity = deflected;
            projectile.position = p + normal * overlap;
            // Missiles push themselves along their nose: point it the new way (arrows re-aim along their speed anyway).
            if (projectile.GetComponent<Missile>() != null && deflected != Vec2.zero)
                projectile.up = Vec2.NormalizedSafe(deflected);

            if (_deflected.Add(projectile.HierarchyNumber))
            {
                if (projectile.GetComponent<Item>() is Item item)
                    item.OwnerId = _player.Id;
                // Like a fresh throw: spared for a moment (it's still next to the bat), then it can hit you again.
                if (projectile.GetComponent<Grenade>() is Grenade grenade)
                {
                    grenade.DetonatesOnOwner = false;
                    grenade.DelayedEnableHurtOwner(OwnerGraceTime);
                }
                if (projectile.GetComponent<Projectile>() is Projectile shot)
                    shot.PlayerOwnerId = _player.Id;
                AudioManager.Get()?.Play("hookshotOnHit");
            }
        }
    }

    /// <summary>
    /// Flying projectiles, in an order that's the same on every machine: active, initialized BoplBodies that aren't
    /// players' ability forms, black holes or items still held in a hand (a grenade before it's thrown).
    /// </summary>
    private static List<BoplBody> Projectiles() =>
        FindObjectsOfType<BoplBody>()
            .Where(b => !b.IsDestroyed && b.gameObject.activeInHierarchy && b.HasBeenInitialized &&
                        b.GetComponent<PlayerCollision>() == null && b.GetComponent<AbilityMonoBehaviour>() == null &&
                        b.GetComponent<BlackHole>() == null &&
                        (b.GetComponent<Grenade>() is not Grenade g || g.hasBeenThrown))
            .OrderBy(b => b.HierarchyNumber)
            .ToList();

    /// <summary>Other players' active bodies, in an order that's the same on every machine.</summary>
    private List<PlayerCollision> Targets() =>
        FindObjectsOfType<PlayerCollision>()
            .Where(b => b.gameObject.activeInHierarchy && b.body != null && !b.IsInvulnerable && b.GetPlayerId() != _player.Id &&
                        PlayerHandler.Get().GetPlayer(b.GetPlayerId()) is Player p && p.IsAlive &&
                        (b.GetComponent<SlimeController>() is not SlimeController s || s.hasSpawned))
            .OrderBy(b => b.GetPlayerId())
            .ThenBy(b => b.body.position.x.m_rawValue)
            .ThenBy(b => b.body.position.y.m_rawValue)
            .ToList();

    private static Vec2 ClosestOnSegment(Vec2 a, Vec2 b, Vec2 p)
    {
        Vec2 ab = b - a;
        Fix lengthSqr = Vec2.Dot(ab, ab);
        if (lengthSqr <= Fix.Zero)
            return a;
        Fix t = Fix.Max(Fix.Zero, Fix.Min(Fix.One, Vec2.Dot(p - a, ab) / lengthSqr));
        return a + ab * t;
    }

    /// <summary>
    /// Adds speed to a slime: in the air, to its velocity; stuck to a platform, it leaves it when pushed away (a push
    /// into the platform is reflected), keeping the speed it had along it.
    /// </summary>
    private static void AddVelocity(PlayerPhysics physics, PlayerBody body, Vec2 change)
    {
        StickyRoundedRectangle ground = physics.IsGrounded() ? physics.getAttachedGround() : null;
        if (ground == null || ground.IsDestroyed)
        {
            body.selfImposedVelocity += change;
            return;
        }

        Vec2 normal = Vec2.NormalizedSafe(ground.currentNormal(body));
        var tangent = new Vec2(normal.y, -normal.x);
        Fix into = Vec2.Dot(change, normal);
        if (into < Fix.Zero)
            change -= normal * ((Fix)2L * into);
        Vec2 velocity = tangent * physics.groundedSpeed + change;
        physics.UnGround(nullRotation: true, playSoundEffect: false);
        body.selfImposedVelocity = velocity;
        body.externalVelocity = Vec2.zero;
    }

    // --- Visual ---

    private void LateUpdate()
    {
        bool show = _state != State.Idle && _caster != null && !_caster.IsDestroyed && _caster.isActiveAndEnabled;
        _visual.enabled = show;
        if (!show)
            return;

        Vec2 dir = _dir;
        if (_state == State.Holding)
        {
            // Raised, ready to swing at what's aimed at.
            Aim(out Vec2 aim, out Fix sign);
            Fix arc = Fix.Max((Fix)BatSettings.SwingArc, Fix.One) * Fix.Pi / (Fix)180L;
            dir = Rotate(aim, -sign * arc / (Fix)2L);
        }
        Segment(dir, out Vec2 from, out Vec2 to);

        var a = (Vector2)from;
        var b = (Vector2)to;
        Transform t = _visual.transform;
        t.position = new Vector3((a.x + b.x) / 2f, (a.y + b.y) / 2f, 0f);
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
        t.localScale = new Vector3(Vector2.Distance(a, b), BatSettings.BatThickness * (float)Scale / BatSkill.SpriteHalfThickness, 1f);

        // Show hitboxes: the bat's capsule (only hits while swinging).
        if (_state == State.Swinging)
            Core.Hitboxes.Capsule(a, b, BatSettings.BatThickness * (float)Scale, _caster.spriteRen);

        SpriteRenderer slime = _caster.spriteRen;
        if (slime != null)
        {
            _visual.sortingLayerID = slime.sortingLayerID;
            _visual.sortingOrder = slime.sortingOrder + 1;
            _visual.color = new Color(1f, 1f, 1f, slime.color.a);
        }
    }

    public override void ExitAbility(AbilityExitInfo info)
    {
    }

    public override Fix GetCooldown() => (Fix)BatSettings.AbilityCooldown;

    public override bool IsInstant() => true;

    public override bool IsCastable(Player player) => _state == State.Idle;

    public override void UpdateCooldowns(PlayerBody playerBody)
    {
    }

    public override PlayerCollision GetPlayerCollision() => null;

    public override SlimeController GetSlimeController() => _caster;
}
