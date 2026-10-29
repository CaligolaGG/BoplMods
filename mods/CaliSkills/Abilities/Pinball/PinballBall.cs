using System.Collections.Generic;
using BoplFixedMath;
using UnityEngine;

namespace CaliSkills.Abilities.Pinball;

/// <summary>
/// The ball. It is a modified grenade (same "item" layer, Item and Grenade components), so other abilities treat
/// it like one: the Duplicator copies it, the Magnet gun grabs it, Gust/Black hole/explosions push it, etc.
///
/// Two phases:
/// - held: ThrowItem2 keeps it in the slime's hand (physics not initialized yet); it grows with the charge;
/// - thrown: ThrowItem2 initializes its physics and releases it; we give it the charge-scaled speed and the
///   physics engine does the bouncing (bounciness 1, no friction) until <see cref="PinballSettings.AbilityDuration"/>.
///
/// State fields are serialized on purpose: Unity's Instantiate copies serialized fields, so copies made by the
/// Duplicator keep "already thrown", charge and remaining lifetime instead of acting like a fresh throw.
/// Player hits are detected here (like GrenadeExplode.OnCollide) and knock the player back instead of killing.
/// Implements IGrenadeDetonation as a no-op: the Grenade component (and the Duplicator's fuse) expect one.
/// </summary>
public class PinballBall : MonoUpdatable, IGrenadeDetonation, ICollisionCallback
{
    private BoplBody _body;
    private Grenade _grenade;
    private Item _item;

    [SerializeField] private bool _thrown;
    [SerializeField] private Fix _baseScale = -Fix.One;
    [SerializeField] private Fix _charge;
    [SerializeField] private Fix _ageSinceThrow;
    [SerializeField] private Fix _speedMultiplier = Fix.One;
    private readonly Dictionary<int, Fix> _lastHitTimeByPlayer = new();

    internal Vec2 Velocity => _body.HasBeenInitialized ? _body.velocity : _body.StartVelocity;

    internal Vec2 Position => _body.position;

    private Fix Speed => (Fix)PinballSettings.BallSpeed * ChargeScale(PinballSettings.SpeedAtMinCharge, _charge) * _speedMultiplier;

    private Fix Knockback => (Fix)PinballSettings.BallKnockback * ChargeScale(PinballSettings.KnockbackAtMinCharge, _charge);

    /// <summary>Linear from <paramref name="atMinCharge"/> (no charge) to 1 (full charge).</summary>
    private static Fix ChargeScale(float atMinCharge, Fix charge)
    {
        Fix min = (Fix)atMinCharge;
        return min + (Fix.One - min) * charge;
    }

    private void Awake()
    {
        Updater.RegisterUpdatable(this);
        _body = GetComponent<BoplBody>();
        _grenade = GetComponent<Grenade>();
        _item = GetComponent<Item>();
        GetComponent<IPhysicsCollider>().RegisterCollisionCallback(this);
    }

    public override void Init()
    {
    }

    public void Detonate()
    {
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        if (!_thrown)
        {
            // ThrowItem2 sets the holder's scale right after instantiating us.
            if (_baseScale < Fix.Zero)
                _baseScale = _body.Scale;

            ThrowItem2 thrower = _grenade.thrower;
            bool hasThrower = thrower != null && !thrower.IsDestroyed;
            if (hasThrower)
                _charge = CurrentCharge(thrower);

            if (!_grenade.hasBeenThrown)
            {
                // Held: grow in the hand with the charge (the hand position is set by ThrowItem2).
                _body.Scale = _baseScale * ChargeScale(PinballSettings.SizeAtMinCharge, _charge);
                return;
            }
            Launch(hasThrower ? Vec2.NormalizedSafe(thrower.dir) : Vec2.zero);
        }

        // Same clock as vanilla projectiles, so time stop freezes the countdown too.
        _ageSinceThrow += simDeltaTime * GameTime.PlayerTimeScale;
        if (_ageSinceThrow >= (Fix)PinballSettings.AbilityDuration)
            Updater.DestroyFix(gameObject);
    }

    /// <summary>The same 0..1 charge ThrowItem2 uses for its aim indicator and throw force.</summary>
    private static Fix CurrentCharge(ThrowItem2 thrower)
    {
        Fix heldFor = thrower.hasInputtedFire ? thrower.FireInputTimeStamp : thrower.timePassed;
        return Fix.Clamp01(heldFor * thrower.ThrowForceGainSpeed);
    }

    /// <summary>
    /// ThrowItem2 releases the ball with zero force (the Pinball prefab sets its throw forces to 0);
    /// launch it at our charge-scaled speed in the aimed direction instead.
    /// </summary>
    private void Launch(Vec2 dir)
    {
        _body.Scale = _baseScale * ChargeScale(PinballSettings.SizeAtMinCharge, _charge);
        if (dir == Vec2.zero)
            dir = Vec2.up;
        _body.velocity = dir * Speed;
        _thrown = true;
    }

    /// <summary>For balls spawned without a thrower (e.g. CaliGameModes' Pinball mode): release and launch right away.</summary>
    internal void LaunchStandalone(Vec2 dir, Fix charge, Fix speedMultiplier)
    {
        _charge = charge;
        _speedMultiplier = speedMultiplier;
        _baseScale = _body.Scale;
        GetComponent<DPhysicsCircle>().ManualInit();
        _grenade.hasBeenThrown = true;
        Launch(Vec2.NormalizedSafe(dir));
    }

    public void OnCollide(CollisionInformation collision)
    {
        if (!_thrown || collision.layer != LayerMask.NameToLayer("Player"))
            return;

        FixTransform other = collision.colliderPP.fixTrans;
        if (other == null || other.IsDestroyed)
            return;
        IPlayerIdHolder idHolder = other.GetComponent<IPlayerIdHolder>();
        if (idHolder == null)
            return;

        int victimId = idHolder.GetPlayerId();
        Player victim = PlayerHandler.Get().GetPlayer(victimId);
        if (victim == null || !victim.IsAlive)
            return;

        // Same rules as GrenadeExplode: spare the thrower right after the throw, and whoever holds it with a Magnet gun.
        if (victimId == _item.OwnerId && !_grenade.DetonatesOnOwner)
            return;
        if (_body.IsBeingHeld() && _body.magnetGunHolder != null &&
            _body.magnetGunHolder.hurtbox.HierarchyNumber == collision.colliderPP.hierarchyNumber)
            return;

        PlayerCollision playerCollision = other.GetComponent<PlayerCollision>();
        if (playerCollision != null && playerCollision.IsInvulnerable)
            return;

        // Spawning / reviving slime: its spawn animation ends by calling Spawn(), which gives back control.
        // A ragdoll would replace that animation and leave the slime stuck forever.
        SlimeController slime = other.GetComponent<SlimeController>();
        if (slime != null && !slime.hasSpawned)
            return;

        if (!TryRegisterHit(victimId))
            return;

        PlayerBody victimBody = other.GetComponent<PlayerBody>();
        Vec2 victimPos = victimBody != null ? victimBody.position : other.position;

        Vec2 knockDir = Vec2.NormalizedSafe(Velocity);
        if (knockDir == Vec2.zero)
            knockDir = Vec2.NormalizedSafe(victimPos - Position);
        PinballKnock.Apply(other.gameObject, victimBody, knockDir * Knockback);

        // Bumper bounce: reflect the ball's velocity on the line between the two centers.
        Vec2 normal = Vec2.NormalizedSafe(Position - victimPos);
        if (normal != Vec2.zero && _body.HasBeenInitialized)
        {
            Vec2 v = Velocity;
            Vec2 reflected = v - normal * ((Fix)2L * Vec2.Dot(v, normal));
            _body.velocity = Vec2.NormalizedSafe(reflected) * Speed * (Fix)PinballSettings.BouncePower;
        }

        AudioManager.Get().Play("rockBounce");
    }

    /// <returns>False if this ball hit that player too recently.</returns>
    private bool TryRegisterHit(int playerId)
    {
        if (_lastHitTimeByPlayer.TryGetValue(playerId, out Fix lastHit) &&
            _ageSinceThrow - lastHit < (Fix)PinballSettings.HitCooldownPerPlayer)
            return false;

        _lastHitTimeByPlayer[playerId] = _ageSinceThrow;
        return true;
    }
}
