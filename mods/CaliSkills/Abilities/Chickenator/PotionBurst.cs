using CaliSkills.Core;
using System.Collections.Generic;
using System.Linq;
using BoplFixedMath;
using CaliSkills.Abilities.NothingPersonal;
using UnityEngine;

namespace CaliSkills.Abilities.Chickenator;

/// <summary>
/// The Chickenator potion's burst, in place of the grenade's explosion (the Grenade component calls
/// <see cref="Detonate"/> when its fuse runs out, also in the thrower's hand). It also bursts on contact, once thrown:
/// with a player (same rules as GrenadeExplode: not its thrower right after the throw, not whoever holds it with a
/// Magnet gun), a platform or wall (only <see cref="ChickenatorSettings.ArmTime"/> after the throw), an explosion, or a projectile that ignites explosives.
/// Every player whose body is in <see cref="ChickenatorSettings.BurstRadius"/> (× the potion's scale), the thrower too,
/// becomes a chicken (<see cref="ChickenCurse"/>) unless a platform is between them and the burst, also a player in an ability form (the thrower is still in the
/// Grenade's throwing form for a moment after the throw, and while holding the potion): their slime does. Sim code; the pink ring and smoke are visuals.
/// </summary>
public class PotionBurst : MonoUpdatable, IGrenadeDetonation, ICollisionCallback
{
    private Grenade _grenade;
    private Item _item;
    private BoplBody _body;
    private bool _burst;

    private void Awake()
    {
        Updater.RegisterUpdatable(this);
        _grenade = GetComponent<Grenade>();
        _item = GetComponent<Item>();
        _body = GetComponent<BoplBody>();
        GetComponent<IPhysicsCollider>().RegisterCollisionCallback(this);
    }

    public override void Init()
    {
    }

    /// <summary>Seconds since the throw (same clock as the grenade's fuse, so time stop pauses it).</summary>
    [SerializeField] private Fix _sinceThrown;

    public override void UpdateSim(Fix simDeltaTime)
    {
        if (_grenade != null && _grenade.hasBeenThrown)
            _sinceThrown += simDeltaTime * GameTime.PlayerTimeScale;
    }

    public void Detonate()
    {
        if (_burst || IsDestroyed)
            return;
        _burst = true;

        Vec2 center = _body.position;
        Fix radius = Fix.Max((Fix)ChickenatorSettings.BurstRadius, (Fix)0.1f) * _body.Scale;
        Fix duration = (Fix)ChickenatorSettings.ChickenDuration;
        // Every player's active body: a slime, or an ability form (e.g. the thrower still in the Grenade's throwing
        // form, or holding the potion when it bursts in the hand): then their slime becomes a chicken.
        var hit = new HashSet<int>();
        foreach (PlayerCollision target in FindObjectsOfType<PlayerCollision>()
                     .Where(b => !b.IsDestroyed && b.gameObject.activeInHierarchy && b.body != null &&
                                 PlayerHandler.Get().GetPlayer(b.GetPlayerId()) is Player p && p.IsAlive)
                     .OrderBy(b => b.GetPlayerId())
                     .ThenBy(b => b.body.position.x.m_rawValue)
                     .ThenBy(b => b.body.position.y.m_rawValue))
        {
            SlimeController slime = target.GetComponent<SlimeController>() ??
                                    target.GetComponent<AbilityMonoBehaviour>()?.GetSlimeController();
            if (slime == null || slime.IsDestroyed || !slime.hasSpawned || hit.Contains(target.GetPlayerId()))
                continue;
            Fix bodyRadius = target.physics != null ? target.physics.radius : slime.playerPhysics.radius;
            Fix reach = radius + bodyRadius;
            if (Vec2.SqrMagnitude(target.body.position - center) > reach * reach ||
                !InLineOfSight(center, target.body.position, bodyRadius))
                continue;
            hit.Add(target.GetPlayerId());
            ChickenCurse.Apply(slime, duration);
        }

        BurstVisual.Spawn(center, (float)radius);
        AudioManager.Get()?.Play("smokeBombExplosion");
        Updater.DestroyFix(gameObject);
    }

    /// <summary>
    /// Whether no platform shields the target from the burst: a ray from the potion's center to the target's center or
    /// to either side of it (most of its radius out) must reach it without hitting a platform first (as Omae's poke).
    /// The potion's center is outside the platform it touched, so that one doesn't block players standing on it.
    /// </summary>
    private static bool InLineOfSight(Vec2 origin, Vec2 target, Fix radius)
    {
        Vec2 toTarget = target - origin;
        Vec2 side = Vec2.NormalizedSafe(new Vec2(-toTarget.y, toTarget.x)) * (radius * (Fix)0.8f);
        foreach (Vec2 point in new[] { target, target + side, target - side })
        {
            Vec2 ray = point - origin;
            Fix distance = Vec2.Magnitude(ray);
            if (distance <= Fix.Zero)
                return true;
            RaycastInformation platform = DetPhysics.Get().RaycastToClosestRoundedRect(origin, ray / distance, distance);
            if (!platform || platform.nearDist >= distance)
                return true;
        }
        return false;
    }

    public void OnCollide(CollisionInformation collision)
    {
        if (_burst || _grenade == null || !_grenade.hasBeenThrown)
            return;

        FixTransform other = collision.colliderPP.fixTrans;
        if (collision.layer == Layers.Player)
        {
            IPlayerIdHolder idHolder = other != null ? other.GetComponent<IPlayerIdHolder>() : null;
            if (idHolder == null)
                return;
            if (idHolder.GetPlayerId() == _item.OwnerId && !_grenade.DetonatesOnOwner)
                return;
            if (_body.IsBeingHeld() && _body.magnetGunHolder != null &&
                _body.magnetGunHolder.hurtbox.HierarchyNumber == collision.colliderPP.hierarchyNumber)
                return;
            Detonate();
        }
        else if (collision.layer == Layers.Projectile)
        {
            Projectile projectile = other != null ? other.GetComponent<Projectile>() : null;
            if (projectile != null && projectile.IgnitesExplosives)
                Detonate();
        }
        else if (collision.layer == Layers.Explosion)
        {
            Detonate();
        }
        else if (other != null && (other.GetComponent<StickyRoundedRectangle>() != null ||
                                   collision.layer == Layers.Wall ||
                                   collision.layer == Layers.NonLethalTerrain ||
                                   collision.layer == Layers.LethalTerrain))
        {
            // A platform or wall (not while a Magnet gun carries it along), once armed: thrown from the floor, it touches
            // that floor at once.
            if (!_body.IsBeingHeld() && _sinceThrown >= (Fix)ChickenatorSettings.ArmTime)
                Detonate();
        }
    }
}

/// <summary>The burst's look: a pink ring growing to the burst's size and fading, and a puff of smoke. Visual only.</summary>
internal sealed class BurstVisual : MonoBehaviour
{
    private const float Lifetime = 0.45f;

    private SpriteRenderer _renderer;
    private float _size;
    private float _time;

    internal static void Spawn(Vec2 center, float radius)
    {
        var go = new GameObject("CaliSkills.ChickenatorBurst");
        go.transform.position = (Vector2)center;
        BurstVisual burst = go.AddComponent<BurstVisual>();
        burst._renderer = go.AddComponent<SpriteRenderer>();
        burst._renderer.sprite = ChickenatorSkill.RingSprite;
        burst._renderer.sortingOrder = 20;
        burst._size = radius * 2f;
        burst.Update();
        NothingPersonalVisuals.SpawnSmokeCloud(center, Mathf.Max(0.3f, radius * 0.25f), 0.6f);
    }

    private void Update()
    {
        _time += Time.deltaTime;
        float t = Mathf.Clamp01(_time / Lifetime);
        float size = _size * (0.3f + 0.7f * Mathf.Sqrt(t));
        transform.localScale = new Vector3(size, size, 1f);
        _renderer.color = new Color(1f, 1f, 1f, 1f - t * t);
        if (_time >= Lifetime)
            Destroy(gameObject);
    }
}
