using CaliSkills.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using BoplFixedMath;
using UnityEngine;

namespace CaliSkills.Abilities.Flamethrower;

/// <summary>
/// Flamethrower: an instant ability (the slime stays a slime, keeps moving and jumping, and holds the flamethrower,
/// see <see cref="FlamethrowerVisuals"/>).
/// - While the button is held, for at most <see cref="FlamethrowerSettings.MaxDuration"/>, it throws flames in a cone
///   where it aims (mouse / right stick; it keeps the last direction while the stick is centered; starts at the facing).
///   The cone starts at the slime's center, <see cref="FlamethrowerSettings.FlameLength"/> long and
///   <see cref="FlamethrowerSettings.FlameAngle"/> wide, and platforms stop it.
/// - Every other player whose body touches the cone, with no platform in between, dies (slimes and ability forms).
///   Smoke clouds in it are set off, and mines in it blow up.
/// - Platforms the flames reach, depending on the map: normal maps → they catch fire where hit
///   (<see cref="PlatformFire"/>; not on space maps); ice maps → they melt, shrinking while hit and growing back later (<see cref="IceMelt"/>).
/// - Space maps: the cone is <see cref="FlamethrowerSettings.SpaceConeShrink"/> smaller and the flames push the slime
///   the other way, like a jetpack; platforms don't burn and the cooldown is <see cref="FlamethrowerSettings.SpaceCooldown"/>.
/// - The cooldown starts when it stops (its timer is pinned to 0 until then). Using another ability stops it.
/// Sim code: inputs, Fix math and objects in a fixed order; the flames are visuals drawn from this state.
/// Skill vs ability: the definition (name, icon, prefab) is <see cref="FlamethrowerSkill"/>.
/// </summary>
public class FlamethrowerAbility : AbilityMonoBehaviour
{
    /// <summary>Rays cast across the cone each tick to find the platforms it reaches.</summary>
    internal const int RayCount = 9;

    private SlimeController _caster;
    private Player _player;
    private int _slot = -1;
    private Fix _time;

    internal bool Firing { get; private set; }

    /// <summary>Where the flames go (unit vector).</summary>
    internal Vec2 Direction { get; private set; } = Vec2.right;

    internal SlimeController Caster => _caster;

    /// <summary>Distance each ray of the cone reaches (its length, or where a platform stops it), edge to edge.</summary>
    internal readonly Fix[] RayReach = new Fix[RayCount];

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
        if (_caster == null || _player == null || Firing)
            return;

        Direction = _caster.isFacingRight() ? Vec2.right : Vec2.left;
        Aim();
        _time = Fix.Zero;
        Firing = true;
        GetComponent<FlamethrowerVisuals>()?.OnStart();
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        if (!Firing)
            return;
        if (!CasterUsable() || !_player.AbilityButtonIsDown(_slot))
        {
            Stop();
            return;
        }

        // The cooldown starts once the flames stop.
        if (_slot >= 0 && _slot < _caster.abilityCooldownTimers.Length)
            _caster.abilityCooldownTimers[_slot] = Fix.Zero;

        Fix dt = GameTime.FixedDeltaTime(_player.Id, simDeltaTime);
        _time += dt;
        Aim();
        Burn(dt);
        if (_time >= (Fix)FlamethrowerSettings.MaxDuration)
            Stop();
    }

    private bool CasterUsable() =>
        _caster != null && !_caster.IsDestroyed && _caster.isActiveAndEnabled && !_caster.isInAbility && _player.IsAlive;

    private void Stop()
    {
        Firing = false;
        Hooked = false;
    }

    /// <summary>Points the flames where the player aims (mouse / right stick); keeps the direction while it's centered.</summary>
    private void Aim()
    {
        Vec2 aim = _player.AimVector();
        if (Vec2.Magnitude(aim) > Constants.DEADZONE)
            Direction = Vec2.NormalizedSafe(aim);
    }

    // --- The cone ---

    internal Fix Scale => _caster.body.fixtrans.Scale;

    internal static bool IsSpace => Constants.leveltype == LevelType.space;

    internal static bool IsIce => Constants.leveltype == LevelType.snow;

    private static Fix SpaceFactor => IsSpace ? Fix.One - Fix.Clamp((Fix)FlamethrowerSettings.SpaceConeShrink, Fix.Zero, (Fix)0.95f) : Fix.One;

    /// <summary>The cone's apex: the slime's center.</summary>
    internal Vec2 Origin => _caster.body.position;

    internal Fix Length => Fix.Max((Fix)FlamethrowerSettings.FlameLength, (Fix)0.1f) * Scale * SpaceFactor;

    /// <summary>Half the cone's opening, in radians.</summary>
    internal Fix HalfAngle =>
        (Fix)Mathf.Clamp(FlamethrowerSettings.FlameAngle, 1f, 179f) / (Fix)2L * Fix.Pi / (Fix)180L * SpaceFactor;

    /// <summary>The direction of ray <paramref name="i"/> (0 and RayCount-1 are the cone's edges).</summary>
    internal Vec2 RayDirection(int i)
    {
        Fix angle = -HalfAngle + HalfAngle * (Fix)2L * (Fix)(long)i / (Fix)(long)(RayCount - 1);
        Fix cos = Fix.Cos(angle), sin = Fix.Sin(angle);
        return new Vec2(Direction.x * cos - Direction.y * sin, Direction.x * sin + Direction.y * cos);
    }

    private void Burn(Fix dt)
    {
        int owner = _player.Id;
        Vec2 origin = Origin;
        Fix length = Length;

        // Platforms the flames reach, ray by ray.
        for (int i = 0; i < RayCount; i++)
        {
            Vec2 dir = RayDirection(i);
            RaycastInformation hit = DetPhysics.Get().RaycastToClosestRoundedRect(origin, dir, length);
            if (!hit)
            {
                RayReach[i] = length;
                continue;
            }
            RayReach[i] = Fix.Max(hit.nearDist, Fix.Zero);
            StickyRoundedRectangle platform = hit.pp.fixTrans != null ? hit.pp.fixTrans.GetComponent<StickyRoundedRectangle>() : null;
            if (platform == null || platform.IsDestroyed)
                continue;
            if (IsIce)
                IceMelt.Hit(platform, hit.nearPos);
            else if (!IsSpace && FlamethrowerSettings.PlatformsCatchFire)
                PlatformFire.Ignite(platform, hit.nearPos, owner);
        }

        // Players in the flames die (one kill per player per tick; Duplicator clones are separate bodies).
        Fix cosHalf = Fix.Cos(HalfAngle);
        foreach (PlayerCollision target in Players(owner))
        {
            Fix radius = target.physics != null ? target.physics.radius : Fix.Zero;
            Vec2 position = target.body.position;
            if (!InCone(position - origin, Direction, length, cosHalf, radius) || !InLineOfSight(origin, position, radius))
                continue;
            target.killPlayer(owner, spawnEffect: true, ignoreInvulnerability: false, CauseOfDeath.Other);
        }

        // Smoke clouds go off.
        if (FlamethrowerSettings.IgniteSmoke)
        {
            foreach (FlammableSmoke smoke in Smokes())
            {
                Fix radius = smoke.circleCollider != null ? smoke.circleCollider.radius : Fix.Zero;
                if (smoke.timeSinceDetonated < Fix.Zero && InCone(smoke.fixTrans.position - origin, Direction, length, cosHalf, radius))
                    smoke.Ignite();
            }
        }

        // Mines blow up (in line of sight, like players).
        if (FlamethrowerSettings.DetonateMines)
        {
            foreach (Mine mine in Mines())
            {
                Vec2 position = mine.body.position;
                Fix radius = mine.GetComponent<DPhysicsCircle>() is DPhysicsCircle circle ? circle.radius : (Fix)0.5f;
                if (!mine.IsDestroyed && InCone(position - origin, Direction, length, cosHalf, radius) && InLineOfSight(origin, position, radius))
                    mine.Detonate();
            }
        }

        // Space: the flames push the slime the other way.
        if (IsSpace)
            Jetpack(-Direction * ((Fix)FlamethrowerSettings.JetpackPush * dt));

        // Hooked to a platform with a grappling hook: the fire runs down the rope to it, however long the rope is
        // (it burns / melts there; not on space maps).
        StickyRoundedRectangle hooked = null;
        Vec2 hookPoint = Vec2.zero;
        Hooked = !IsSpace && HookedPlatform(out hooked, out hookPoint);
        if (Hooked && FlamethrowerSettings.RopeCarriesFlames)
        {
            if (IsIce)
                IceMelt.Hit(hooked, hookPoint);
            else if (FlamethrowerSettings.PlatformsCatchFire)
                PlatformFire.Ignite(hooked, hookPoint, owner);
        }
    }

    /// <summary>Whether the caster was hooked to a platform with a grappling hook in the last tick (for the visuals).</summary>
    internal bool Hooked { get; private set; }

    /// <summary>
    /// The platform the caster's grappling hook rope is attached to (Hookshot: the slime holds one end of the rope,
    /// a <see cref="RopeAttachment"/> on a platform the other), and where.
    /// </summary>
    private bool HookedPlatform(out StickyRoundedRectangle platform, out Vec2 point)
    {
        platform = null;
        point = Vec2.zero;
        RopeBody rope = _caster.body.ropeBody;
        if (rope == null || !rope.enabled || !rope.hookHasArrived || rope.hasBeenDettached)
            return false;
        foreach (RopeAttachment attachment in SceneScan.All<RopeAttachment>())
        {
            if (attachment == null || attachment.IsDestroyed || !attachment.gameObject.activeInHierarchy || attachment.GetInitializedRopeBody() != rope || !attachment.IsPlatformAttachment() ||
                attachment.topAttachment == _caster.body.topAttachment)
                continue;
            platform = attachment.GetPlatformParent();
            point = platform.PositionFromLocalPlayerPos(attachment.LocalPlatformPosition, Fix.Zero);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Pushes the caster like the Fan pushes slimes: in the air, added to its speed; on a platform, it slides along it,
    /// and leaves it when pushed mostly away from the surface.
    /// </summary>
    private void Jetpack(Vec2 change)
    {
        PlayerPhysics physics = _caster.playerPhysics;
        PlayerBody body = _caster.body;
        StickyRoundedRectangle ground = physics.IsGrounded() ? physics.getAttachedGround() : null;
        if (ground == null || ground.IsDestroyed)
        {
            body.externalVelocity += change;
            return;
        }
        Vec2 normal = Vec2.NormalizedSafe(ground.currentNormal(body));
        var tangent = new Vec2(normal.y, -normal.x);
        if (Vec2.Dot(Vec2.NormalizedSafe(change), normal) > (Fix)0.5f)
        {
            Vec2 velocity = tangent * physics.groundedSpeed + change;
            physics.UnGround(nullRotation: true, playSoundEffect: false);
            body.selfImposedVelocity = velocity;
            body.externalVelocity = Vec2.zero;
            return;
        }
        physics.groundedSpeed += Vec2.Dot(change, tangent);
    }

    /// <summary>Whether a circle at <paramref name="offset"/> from the apex touches the cone (approximation at the edges).</summary>
    private static bool InCone(Vec2 offset, Vec2 dir, Fix range, Fix cosHalf, Fix radius)
    {
        Fix distance = Vec2.Magnitude(offset);
        if (distance - radius > range)
            return false;
        if (distance <= radius)
            return true;
        return Vec2.Dot(offset, dir) >= distance * cosHalf - radius;
    }

    /// <summary>
    /// Whether no platform stands between the apex and the target: a ray to its center or to either side of it (most of
    /// its radius out) must reach it without hitting a platform first (like Omae's poke).
    /// </summary>
    private static bool InLineOfSight(Vec2 origin, Vec2 target, Fix radius)
    {
        Vec2 toTarget = target - origin;
        Vec2 side = Vec2.NormalizedSafe(new Vec2(-toTarget.y, toTarget.x)) * (radius * (Fix)0.8f);
        foreach (Vec2 point in new[] { target, target + side, target - side })
        {
            Vec2 ray = point - origin;
            Fix distance = Vec2.Magnitude(ray);
            if (distance <= (Fix)0.0001f)
                return true;
            RaycastInformation platform = DetPhysics.Get().RaycastToClosestRoundedRect(origin, ray / distance, distance);
            if (!platform || platform.nearDist >= distance)
                return true;
        }
        return false;
    }

    /// <summary>Other players' active bodies (slimes and ability forms), in an order that's the same on every machine.</summary>
    internal static List<PlayerCollision> Players(int owner) =>
        SceneScan.All<PlayerCollision>()
            .Where(b => b.gameObject.activeInHierarchy && b.body != null && b.GetPlayerId() != owner &&
                        PlayerHandler.Get().GetPlayer(b.GetPlayerId()) is Player p && p.IsAlive &&
                        (b.GetComponent<SlimeController>() is not SlimeController s || s.hasSpawned))
            .OrderBy(b => b.GetPlayerId())
            .ThenBy(b => b.body.position.x.m_rawValue)
            .ThenBy(b => b.body.position.y.m_rawValue)
            .ToList();

    private static List<FlammableSmoke> Smokes() =>
        SceneScan.All<FlammableSmoke>()
            .Where(s => !s.IsDestroyed && s.gameObject.activeInHierarchy && s.fixTrans != null)
            .OrderBy(s => s.fixTrans.position.x.m_rawValue)
            .ThenBy(s => s.fixTrans.position.y.m_rawValue)
            .ToList();

    private static List<Mine> Mines() =>
        SceneScan.All<Mine>()
            .Where(m => !m.IsDestroyed && m.gameObject.activeInHierarchy && m.body != null)
            .OrderBy(m => m.body.HierarchyNumber)
            .ToList();

    public override void ExitAbility(AbilityExitInfo info)
    {
    }

    public override Fix GetCooldown() => (Fix)(IsSpace ? FlamethrowerSettings.SpaceCooldown : FlamethrowerSettings.AbilityCooldown);

    public override bool IsInstant() => true;

    public override bool IsCastable(Player player) => !Firing;

    public override void UpdateCooldowns(PlayerBody playerBody)
    {
    }

    public override PlayerCollision GetPlayerCollision() => null;

    public override SlimeController GetSlimeController() => _caster;
}
