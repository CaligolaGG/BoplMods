using BoplFixedMath;
using UnityEngine;

namespace CaliSkills.Abilities.Pinball;

/// <summary>Knocks a player back without killing them. Works for slimes and for ability forms (bow, rock...).</summary>
internal static class PinballKnock
{
    internal static void Apply(GameObject victim, PlayerBody victimBody, Vec2 impulse)
    {
        // Slimes stick to any side of a platform (floor, wall, ceiling). Detach first, and make sure the knock
        // points away from that surface: otherwise the landing raycast re-attaches them on the next tick.
        PlayerPhysics physics = victim.GetComponent<PlayerPhysics>();
        if (physics != null && physics.IsGrounded())
        {
            StickyRoundedRectangle ground = physics.getAttachedGround();
            if (ground != null && !ground.IsDestroyed && victimBody != null)
                impulse = AwayFromSurface(impulse, Vec2.NormalizedSafe(ground.currentNormal(victimBody)));
            physics.UnGround();
        }

        // Normal slime: ragdoll it like the Magnet gun's throw does.
        SlimeController slime = victim.GetComponent<SlimeController>();
        if (slime != null && slime.isActiveAndEnabled)
        {
            slime.ApplyRagDoll((Fix)PinballSettings.KnockDuration, impulse, goFast: true);
            return;
        }

        // Player is inside an ability form: just push it.
        if (victimBody != null)
            victimBody.externalVelocity += impulse;
    }

    /// <summary>
    /// Keeps the impulse's strength, but reflects any part going into the surface and guarantees at least
    /// <see cref="PinballSettings.MinDetachFraction"/> of it points outward along the surface normal.
    /// </summary>
    private static Vec2 AwayFromSurface(Vec2 impulse, Vec2 normal)
    {
        if (normal == Vec2.zero)
            return impulse;

        Fix strength = Vec2.Magnitude(impulse);
        Fix along = Vec2.Dot(impulse, normal);
        if (along < Fix.Zero)
            impulse -= normal * ((Fix)2L * along);

        Fix minOutward = strength * (Fix)PinballSettings.MinDetachFraction;
        along = Vec2.Dot(impulse, normal);
        if (along < minOutward)
            impulse += normal * (minOutward - along);

        Vec2 dir = Vec2.NormalizedSafe(impulse);
        return dir == Vec2.zero ? normal * strength : dir * strength;
    }
}
