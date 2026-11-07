using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;

namespace CustomRules.Rules;

/// <summary>
/// Player bouncing mode. Vanilla slimes pass through each other; here a <see cref="PlayerBouncer"/> (one per level)
/// makes every pair of touching slimes bounce apart (× PlayerBouncePower), except a slime big enough to eat the other.
/// Sub rule "bounce on everything": slimes never stick to platforms, they bounce off them instead (× BouncePower), and
/// each bounce gives back one jump to use in the air; see <see cref="BounceOnEverythingRule"/> for the exceptions.
/// Only slimes take part, not ability forms (Bow, Rock...), whose own physics are left alone.
/// </summary>
[HarmonyPatch(typeof(GameSessionHandler), nameof(GameSessionHandler.SpawnPlayers))]
internal static class PlayerBouncingRule
{
    /// <summary>Two slimes that touch always separate at least this fast (× BouncePower), so a slow touch still bounces.</summary>
    internal static readonly Fix MinBounceSpeed = (Fix)15L;

    /// <summary>
    /// Slimes hitting each other at least this fast (closing speed, about a full-speed run) knock a slime stuck to a
    /// platform off it, instead of just making it slide.
    /// </summary>
    internal static readonly Fix DetachImpactSpeed = (Fix)18L;

    private static void Postfix()
    {
        if (BasicRules.PlayerBouncing.IsOn)
            new GameObject("CustomRules.PlayerBouncer").AddComponent<PlayerBouncer>();
    }

    /// <summary>Platform bounces (bounce on everything).</summary>
    internal static Fix BouncePower => BasicRules.BouncePower.ActiveFix;

    /// <summary>Slime vs slime bounces.</summary>
    internal static Fix PlayerBouncePower => BasicRules.PlayerBouncePower.ActiveFix;

    /// <summary>
    /// Whether <paramref name="eater"/> is big enough to eat <paramref name="food"/> (vanilla rule in
    /// <c>SlimeController.OnCollide</c>: scale difference ≥ <c>scaleDiffBeforeNomNom</c>, other team).
    /// </summary>
    internal static bool CanEat(SlimeController eater, SlimeController food)
    {
        if (eater.body.fixtrans.Scale - food.body.fixtrans.Scale < eater.scaleDiffBeforeNomNom)
            return false;
        Player a = PlayerHandler.Get().GetPlayer(eater.playerNumber);
        Player b = PlayerHandler.Get().GetPlayer(food.playerNumber);
        return a != null && b != null && a.Team != b.Team;
    }

    /// <summary>
    /// Gives a slime a velocity change, whether it's in the air or stuck to a platform. A <paramref name="hardHit"/>
    /// always knocks a stuck slime off its platform (the part of the push going into the platform bounces back out).
    /// </summary>
    internal static void AddVelocity(SlimeController slime, Vec2 change, bool hardHit)
    {
        Vec2 delta = change;
        PlayerPhysics physics = slime.playerPhysics;
        PlayerBody body = slime.body;
        StickyRoundedRectangle ground = physics.IsGrounded() ? physics.getAttachedGround() : null;
        if (ground == null || ground.IsDestroyed)
        {
            body.selfImposedVelocity += delta;
            return;
        }

        Vec2 normal = Vec2.NormalizedSafe(ground.currentNormal(body));
        var tangent = new Vec2(normal.y, -normal.x);
        Fix intoPlatform = Vec2.Dot(delta, normal);
        if (intoPlatform <= Fix.Zero)
        {
            if (!hardHit)
            {
                // Gently pushed into the platform: slide along it.
                physics.groundedSpeed += Vec2.Dot(delta, tangent);
                return;
            }
            delta -= normal * ((Fix)2L * intoPlatform);
        }

        // Pushed away from the platform: leave it, keeping the speed it had along it.
        Vec2 velocity = tangent * physics.groundedSpeed + delta;
        physics.UnGround(nullRotation: true, playSoundEffect: false);
        body.selfImposedVelocity = velocity;
        body.externalVelocity = Vec2.zero;
    }
}

/// <summary>Slime vs slime bounces, once per tick, in a fixed order (sim code, Fix math only).</summary>
internal sealed class PlayerBouncer : MonoUpdatable
{
    private void Awake() => Updater.RegisterUpdatable(this);

    /// <summary>The slimes, collected as they register with the Updater (spawn).</summary>
    private static readonly List<SlimeController> Slimes = new();

    [HarmonyPatch(typeof(Updater), nameof(Updater.RegisterUpdatable))]
    private static class CollectSlimes
    {
        private static void Postfix(MonoUpdatable updatable)
        {
            if (updatable is SlimeController slime && !Slimes.Contains(slime))
                Slimes.Add(slime);
        }
    }

    public override void Init()
    {
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        // Every slime ever spawned (no scene search every tick); the order isn't the same on every machine: sort.
        Slimes.RemoveAll(s => s == null);
        List<SlimeController> slimes = Slimes
            .Where(IsBouncy)
            .OrderBy(s => s.playerNumber)
            .ThenBy(s => s.body.position.x.m_rawValue)
            .ThenBy(s => s.body.position.y.m_rawValue)
            .ToList();

        for (int i = 0; i < slimes.Count; i++)
        {
            for (int j = i + 1; j < slimes.Count; j++)
                Bounce(slimes[i], slimes[j]);
        }
    }

    private static bool IsBouncy(SlimeController slime)
    {
        if (!slime.isActiveAndEnabled || !slime.hasSpawned || slime.body == null || !slime.body.simulated ||
            slime.playerPhysics == null)
            return false;
        Player player = PlayerHandler.Get().GetPlayer(slime.playerNumber);
        return player != null && player.IsAlive;
    }

    private static void Bounce(SlimeController a, SlimeController b)
    {
        // A slime big enough to eat the other lets the game's bite happen instead.
        if (PlayerBouncingRule.CanEat(a, b) || PlayerBouncingRule.CanEat(b, a))
            return;

        Vec2 offset = b.body.position - a.body.position;
        Fix distance = Vec2.Magnitude(offset);
        Fix touching = a.playerPhysics.radius + b.playerPhysics.radius;
        if (distance >= touching || distance == Fix.Zero)
            return;

        Vec2 normal = offset / distance;
        Fix approach = Vec2.Dot(Velocity(b) - Velocity(a), normal);
        Fix power = PlayerBouncingRule.PlayerBouncePower;
        Fix separation = Fix.Max(-approach * power, PlayerBouncingRule.MinBounceSpeed * power);
        if (approach >= separation)
            return; // already moving apart fast enough

        // Equal masses: each slime takes half of the change.
        Vec2 half = normal * ((separation - approach) / (Fix)2L);
        bool hardHit = -approach >= PlayerBouncingRule.DetachImpactSpeed;
        PlayerBouncingRule.AddVelocity(a, -half, hardHit);
        PlayerBouncingRule.AddVelocity(b, half, hardHit);

        // Un-overlap the ones in the air (platforms hold the others in place).
        Vec2 push = normal * ((touching - distance) / (Fix)2L);
        if (!a.playerPhysics.IsGrounded())
            a.body.position -= push;
        if (!b.playerPhysics.IsGrounded())
            b.body.position += push;
    }

    private static Vec2 Velocity(SlimeController slime)
    {
        PlayerPhysics physics = slime.playerPhysics;
        StickyRoundedRectangle ground = physics.IsGrounded() ? physics.getAttachedGround() : null;
        if (ground == null || ground.IsDestroyed)
            return slime.body.Velocity;
        Vec2 normal = Vec2.NormalizedSafe(ground.currentNormal(slime.body));
        return new Vec2(normal.y, -normal.x) * physics.groundedSpeed;
    }
}

/// <summary>
/// "Bounce on everything" jumping: vanilla only jumps when grounded, which never happens in this mode. Each platform
/// bounce gives the slime one jump, used by pressing jump in the air (an upright jump, like an air jump).
/// </summary>
[HarmonyPatch(typeof(SlimeController), nameof(SlimeController.OldUpdate))]
internal static class BounceJumpPatch
{
    private sealed class JumpCharge
    {
        public bool Available;
    }

    // Weak keys: entries vanish together with the destroyed slime.
    private static readonly ConditionalWeakTable<SlimeController, JumpCharge> Charges = new();

    /// <summary>Touching a platform counts as landing: the bounce jump and the triple-jump air jumps come back.</summary>
    internal static void GiveJump(SlimeController slime)
    {
        Charges.GetOrCreateValue(slime).Available = true;
        AirJumpsRule.ResetAirJumps(slime);
    }

    // Before AirJumpsRule's postfix: a press after a bounce uses the bounce jump first, the air jumps after.
    [HarmonyPriority(Priority.High)]
    private static void Postfix(SlimeController __instance)
    {
        if (!BasicRules.BounceOnEverything.IsOn || !Charges.TryGetValue(__instance, out JumpCharge charge) || !charge.Available)
            return;
        PlayerPhysics physics = __instance.playerPhysics;
        if (physics.IsGrounded() || physics.jumpedThisFrame || !AirJumpsRule.CanAirJump(__instance))
            return;

        Player player = PlayerHandler.Get().GetPlayer(__instance.playerNumber);
        if (player == null || !player.jumpButton_PressedThisFrame())
            return;

        charge.Available = false;
        AirJumpsRule.AirJump(__instance);
    }
}

/// <summary>
/// "Bounce on everything": where a slime would stick to a platform, it bounces off it instead
/// (velocity reflected on the surface normal, × BouncePower), and gets a jump (<see cref="BounceJumpPatch"/>).
/// Exception: while the button of a ready ability that can only be used on the ground (Spike, Tesla coil...) is held,
/// the slime sticks as usual, so the ability goes off (<see cref="WantsToStick"/>).
/// </summary>
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CollideWithGround))]
internal static class BounceOnEverythingRule
{
    private static readonly Fix SurfaceGap = (Fix)0.05f;

    /// <summary>Holding the button of a ready ability that needs the ground: let the slime stick to cast it.</summary>
    internal static bool WantsToStick(SlimeController slime)
    {
        if (slime.suppressInput || slime.abilities == null)
            return false;
        Player player = PlayerHandler.Get().GetPlayer(slime.playerNumber);
        if (player == null || !player.CanUseAbilities)
            return false;
        int count = System.Math.Min(slime.abilities.Count, slime.abilityCooldownTimers.Length);
        for (int i = 0; i < count; i++)
        {
            AbilityMonoBehaviour ability = slime.abilities[i];
            if (ability != null && ability.OnlyCastableOnGround && player.AbilityButtonIsDown(i) && slime.isAbilityReady(i) &&
                ability.IsCastable(player))
                return true;
        }
        return false;
    }

    private static bool Prefix(PlayerPhysics __instance, ref RaycastInformation collision, Vec2 location)
    {
        if (!BasicRules.BounceOnEverything.IsOn)
            return true;
        SlimeController slime = __instance.GetComponent<SlimeController>();
        if (slime == null)
            return true; // ability forms keep their own behaviour
        if (WantsToStick(slime))
            return true;

        StickyRoundedRectangle wall = collision.pp.fixTrans != null ? collision.pp.fixTrans.GetComponent<StickyRoundedRectangle>() : null;
        if (wall == null || wall.IsDestroyed)
            return true;

        BounceOff(slime, __instance, wall, location);
        return false;
    }

    private static void BounceOff(SlimeController slime, PlayerPhysics physics, StickyRoundedRectangle wall, Vec2 location)
    {
        PlayerBody body = slime.body;

        // Where it would have been placed when sticking, then just off the surface.
        Fix radius = physics.radius;
        body.position = wall.PositionFromLocalPlayerPos(wall.LocalPlayerPosFromPos(location, radius), radius);
        Vec2 normal = Vec2.NormalizedSafe(wall.currentNormal(body));
        body.position += normal * SurfaceGap;

        Fix restitution = Fix.One + PlayerBouncingRule.BouncePower;
        body.selfImposedVelocity = Reflect(body.selfImposedVelocity, normal, restitution);
        body.externalVelocity = Reflect(body.externalVelocity, normal, restitution);

        // Slimes never stand on anything in this mode, so touching a platform gives them their jump back.
        BounceJumpPatch.GiveJump(slime);
    }

    /// <summary>Removes the part going into the surface and sends back <paramref name="restitution"/> - 1 of it.</summary>
    private static Vec2 Reflect(Vec2 velocity, Vec2 normal, Fix restitution)
    {
        Fix into = Vec2.Dot(velocity, normal);
        return into < Fix.Zero ? velocity - normal * (restitution * into) : velocity;
    }
}

/// <summary>
/// "Bounce on everything": a slime can still end up stuck to a platform without landing on it: leaving an ability
/// form that was on the ground (Roll, Meteor, Spike...), a ragdoll (gun hits) landing, a respawn, or sticking on
/// purpose to cast a ground ability. Before its update reads the inputs, such a slime hops off the platform (keeping
/// its speed along it) and gets its bounce jump, so the mode never stops and jumping keeps working. Not while it
/// still wants to stick (<see cref="BounceOnEverythingRule.WantsToStick"/>), during its spawn animation, a ragdoll or
/// a bite.
/// </summary>
[HarmonyPatch(typeof(SlimeController), nameof(SlimeController.OldUpdate))]
internal static class BounceOffGroundPatch
{
    /// <summary>Speed of the hop off the platform, away from it.</summary>
    internal static readonly Fix HopSpeed = (Fix)8L;

    private static void Prefix(SlimeController __instance)
    {
        if (!BasicRules.BounceOnEverything.IsOn)
            return;
        PlayerPhysics physics = __instance.playerPhysics;
        if (physics == null || !physics.IsGrounded() || !__instance.hasSpawned || __instance.body == null)
            return;
        if (__instance.currentState is SlimeController.State.ragdoll or SlimeController.State.biting or SlimeController.State.nothing)
            return;
        if (BounceOnEverythingRule.WantsToStick(__instance))
            return;
        StickyRoundedRectangle ground = physics.getAttachedGround();
        if (ground == null || ground.IsDestroyed)
            return;

        PlayerBody body = __instance.body;
        Vec2 normal = Vec2.NormalizedSafe(ground.currentNormal(body));
        var tangent = new Vec2(normal.y, -normal.x);
        Vec2 velocity = tangent * physics.groundedSpeed + normal * HopSpeed;
        physics.UnGround(nullRotation: true, playSoundEffect: false);
        body.selfImposedVelocity = velocity;
        body.externalVelocity = Vec2.zero;
        BounceJumpPatch.GiveJump(__instance);
    }
}

/// <summary>
/// "Bounce on everything": vanilla only lets a slime bite (eat a smaller slime) while it stands on a platform, which
/// never happens in this mode. In the air, the same check as <c>SlimeController.OnCollide</c> runs here without the
/// grounded part; on a platform vanilla handles it.
/// </summary>
[HarmonyPatch(typeof(SlimeController), nameof(SlimeController.OnCollide))]
internal static class BounceEatPatch
{
    // By ref only to read the struct without the analyzer's "modified copy" warning; nothing is changed.
    private static void Prefix(SlimeController __instance, ref CollisionInformation collision)
    {
        if (!BasicRules.BounceOnEverything.IsOn || collision.layer != LayerMask.NameToLayer("Player"))
            return;
        if (__instance.currentState == SlimeController.State.biting || __instance.playerPhysics == null ||
            __instance.playerPhysics.IsGrounded() || GameTime.IsTimeStoppedFor(__instance.playerNumber))
            return;
        FixTransform other = collision.colliderPP.fixTrans;
        if (other == null || __instance.body.fixtrans.Scale - other.Scale < __instance.scaleDiffBeforeNomNom)
            return;
        PlayerCollision victim = other.GetComponent<PlayerCollision>();
        if (victim == null)
            return;
        Player eater = PlayerHandler.Get().GetPlayer(__instance.playerNumber);
        Player food = PlayerHandler.Get().GetPlayer(victim.GetPlayerId());
        if (eater != null && food != null && eater.Team != food.Team)
            __instance.Bite(victim);
    }
}
