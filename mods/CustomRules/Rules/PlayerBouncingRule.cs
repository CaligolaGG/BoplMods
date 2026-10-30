using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;

namespace CustomRules.Rules;

/// <summary>
/// Player bouncing mode. Vanilla slimes pass through each other; here a <see cref="PlayerBouncer"/> (one per level)
/// makes every pair of touching slimes bounce apart. Sub rule "bounce on everything": slimes never stick to platforms,
/// they bounce off them instead, and each bounce gives back one jump to use in the air.
/// Only slimes take part, not ability forms (Bow, Rock...), whose own physics are left alone.
/// </summary>
[HarmonyPatch(typeof(GameSessionHandler), nameof(GameSessionHandler.SpawnPlayers))]
internal static class PlayerBouncingRule
{
    /// <summary>Two slimes that touch always separate at least this fast (× BouncePower), so a slow touch still bounces.</summary>
    internal static readonly Fix MinBounceSpeed = (Fix)15L;

    private static void Postfix()
    {
        if (BasicRules.PlayerBouncing.IsOn)
            new GameObject("CustomRules.PlayerBouncer").AddComponent<PlayerBouncer>();
    }

    internal static Fix BouncePower => BasicRules.BouncePower.ActiveFix;

    /// <summary>Gives a slime a velocity change, whether it's in the air or stuck to a platform.</summary>
    internal static void AddVelocity(SlimeController slime, Vec2 delta)
    {
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
        if (Vec2.Dot(delta, normal) <= Fix.Zero)
        {
            // Pushed into the platform: slide along it.
            physics.groundedSpeed += Vec2.Dot(delta, tangent);
            return;
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

    public override void Init()
    {
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        // FindObjectsOfType's order isn't guaranteed to match between machines: sort.
        List<SlimeController> slimes = FindObjectsOfType<SlimeController>()
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
        Vec2 offset = b.body.position - a.body.position;
        Fix distance = Vec2.Magnitude(offset);
        Fix touching = a.playerPhysics.radius + b.playerPhysics.radius;
        if (distance >= touching || distance == Fix.Zero)
            return;

        Vec2 normal = offset / distance;
        Fix approach = Vec2.Dot(Velocity(b) - Velocity(a), normal);
        Fix power = PlayerBouncingRule.BouncePower;
        Fix separation = Fix.Max(-approach * power, PlayerBouncingRule.MinBounceSpeed * power);
        if (approach >= separation)
            return; // already moving apart fast enough

        // Equal masses: each slime takes half of the change.
        Vec2 half = normal * ((separation - approach) / (Fix)2L);
        PlayerBouncingRule.AddVelocity(a, -half);
        PlayerBouncingRule.AddVelocity(b, half);

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
/// </summary>
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CollideWithGround))]
internal static class BounceOnEverythingRule
{
    private static readonly Fix SurfaceGap = (Fix)0.05f;

    private static bool Prefix(PlayerPhysics __instance, ref RaycastInformation collision, Vec2 location)
    {
        if (!BasicRules.BounceOnEverything.IsOn)
            return true;
        SlimeController slime = __instance.GetComponent<SlimeController>();
        if (slime == null)
            return true; // ability forms keep their own behaviour

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
