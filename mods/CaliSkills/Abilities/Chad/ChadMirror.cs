using System.Collections.Generic;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;

namespace CaliSkills.Abilities.Chad;

/// <summary>
/// Aura farm's mirror for beams and rays, and its reveal of invisible slimes:
/// - Beams (Beam ability): every tick, before the game traces its beams (<see cref="DetPhysics.UpdateBeams"/>), each
///   beam that reaches a shining chad's area before any platform is cut there (postfix on the per-beam raycast) and a
///   new beam, owned by the chad, starts where it hit, going the reflected way (the game then traces, draws and hurts
///   with it like any beam; it's re-added every tick while the beam keeps hitting, with a stable id). The game's own
///   reflection only exists for water.
/// - Rays (Growth / Shrink ray: <see cref="ShootScaleChange"/>, Blink gun: <see cref="ShootQuantum"/>): a ray hitting a
///   shining slime is shot again from where it hit, reflected off the slime's round body (like the game does off
///   water), as the chad's ray.
/// - Invisible slimes within <see cref="ChadSettings.RevealRadius"/> of a shining chad are drawn fully visible
///   (visual only, like the game's own fade).
/// Sim code (beams, rays): Fix math, chads in player id order.
/// </summary>
internal static class ChadMirror
{
    /// <summary>Beam index → distance at which it reaches a shining area (this tick).</summary>
    private static readonly Dictionary<int, Fix> Cuts = new();

    private const int MaxMirroredBeams = 16;
    private const int MaxRayBounces = 3;
    private static int _rayDepth;

    private static bool Usable(ChadAbility chad) =>
        chad != null && chad.IsShining && chad.Caster != null && !chad.Caster.IsDestroyed && chad.Caster.isActiveAndEnabled;

    /// <summary>Where a ray from <paramref name="origin"/> along unit <paramref name="dir"/> enters a circle; false if it doesn't (or starts inside).</summary>
    private static bool EntersCircle(Vec2 origin, Vec2 dir, Vec2 center, Fix radius, out Fix t)
    {
        t = Fix.Zero;
        Vec2 m = origin - center;
        Fix b = Vec2.Dot(m, dir);
        Fix c = Vec2.Dot(m, m) - radius * radius;
        if (c <= Fix.Zero || b > Fix.Zero)
            return false;
        Fix disc = b * b - c;
        if (disc < Fix.Zero)
            return false;
        t = -b - Fix.Sqrt(disc);
        return t >= Fix.Zero;
    }

    private static Vec2 Reflect(Vec2 dir, Vec2 normal) => dir - normal * ((Fix)2L * Vec2.Dot(dir, normal));

    // --- Beams ---

    [HarmonyPatch(typeof(DetPhysics), nameof(DetPhysics.UpdateBeams))]
    private static class MirrorBeams
    {
        private static void Prefix(DetPhysics __instance)
        {
            Cuts.Clear();
            if (ChadAbility.ShiningByPlayer.Count == 0)
                return;
            int added = 0;
            int original = __instance.beamsThisFrame;
            for (int i = 0; i < __instance.beamsThisFrame && i < original + MaxMirroredBeams; i++)
            {
                DetPhysics.BeamBody beam = __instance.beamBodies[i];
                Vec2 dir = Vec2.NormalizedSafe(beam.direction);
                if (dir == Vec2.zero)
                    continue;
                RaycastInformation wall = __instance.RaycastToClosest(beam.position, dir, __instance.maxBeamDistance, __instance.beamHitMask);
                Fix limit = wall ? Fix.Min(wall.nearDist, __instance.maxBeamDistance) : __instance.maxBeamDistance;

                ChadAbility hit = null;
                Fix best = limit;
                foreach (ChadAbility chad in ChadAbility.ShiningByPlayer.Values)
                {
                    if (!Usable(chad) || chad.PlayerId == beam.ownerId)
                        continue;
                    if (EntersCircle(beam.position, dir, chad.AuraCenter, chad.AuraReach, out Fix t) && t < best)
                    {
                        best = t;
                        hit = chad;
                    }
                }
                if (hit == null)
                    continue;
                Cuts[i] = best;
                if (added >= MaxMirroredBeams || __instance.beamsThisFrame >= __instance.beamBodies.Length / 2)
                    continue;

                Vec2 point = beam.position + dir * best;
                Vec2 normal = Vec2.NormalizedSafe(point - hit.AuraCenter);
                if (normal == Vec2.zero)
                    normal = -dir;
                __instance.AddBeamBody(new DetPhysics.BeamBody
                {
                    // Stable while the same beam keeps hitting the same chad (the game matches beams frame to frame by id).
                    id = unchecked(500000 + beam.id * 8 + hit.PlayerId),
                    ownerId = hit.PlayerId,
                    position = point + normal * (Fix)0.05f,
                    direction = Vec2.NormalizedSafe(Reflect(dir, normal)),
                    timePassed = beam.timePassed,
                    scale = beam.scale,
                    colors = beam.colors,
                });
                added++;
            }
        }

        private static void Postfix() => Cuts.Clear();
    }

    /// <summary>A beam that reaches a shining area stops there (its reflection is the beam added above).</summary>
    [HarmonyPatch(typeof(DetPhysics), "RaycastBeam", typeof(int))]
    private static class CutBeams
    {
        private static void Postfix(DetPhysics __instance, int ind)
        {
            if (Cuts.Count == 0 || !Cuts.TryGetValue(ind, out Fix cut))
                return;
            if (__instance.beamIsReflected[ind])
            {
                // Bounced off water first: only cut if the shining area comes before the water.
                Fix first = __instance.beamBodies[ind].distance - __instance.beamReflectionDistance[ind];
                if (cut >= first)
                    return;
                __instance.beamIsReflected[ind] = false;
                __instance.beamReflectionDistance[ind] = Fix.Zero;
            }
            if (cut < __instance.beamBodies[ind].distance)
                __instance.beamBodies[ind].distance = cut;
        }
    }

    // --- Rays ---

    /// <summary>
    /// If <paramref name="hit"/> is a shining slime, where the ray goes on from (just outside its body) and which way.
    /// </summary>
    private static bool Mirror(RaycastInformation hit, Vec2 direction, out Vec2 start, out Vec2 reflected, out int chadId)
    {
        start = Vec2.zero;
        reflected = Vec2.zero;
        chadId = -1;
        if (_rayDepth >= MaxRayBounces || ChadAbility.ShiningByPlayer.Count == 0 || hit.layer != LayerMask.NameToLayer("Player") ||
            hit.pp.fixTrans == null)
            return false;
        SlimeController slime = hit.pp.fixTrans.GetComponent<SlimeController>();
        if (slime == null || !ChadAbility.Shining.Contains(slime))
            return false;
        Vec2 dir = Vec2.NormalizedSafe(direction);
        Vec2 normal = Vec2.NormalizedSafe(hit.nearPos - slime.body.position);
        if (normal == Vec2.zero)
            normal = -dir;
        reflected = Vec2.NormalizedSafe(Reflect(dir, normal));
        start = hit.nearPos + normal * (slime.playerPhysics.radius * (Fix)0.3f);
        chadId = slime.GetPlayerId();
        return reflected != Vec2.zero;
    }

    [HarmonyPatch(typeof(ShootScaleChange), nameof(ShootScaleChange.ApplyScaleChange))]
    private static class MirrorScaleRays
    {
        private static bool Prefix(ShootScaleChange __instance, RaycastInformation hit, Vec2 firepoint, Vec2 direction, ref bool hasFired)
        {
            if (!Mirror(hit, direction, out Vec2 start, out Vec2 reflected, out int chadId))
                return true;
            // Like the game's bounce off water: the ray up to here, then a new shot.
            __instance.spawnRayCastEffect((Vector2)firepoint, (Vector2)direction, (float)hit.nearDist, didHit: false, reflected: true);
            __instance.rayParticle.Stop();
            AudioManager.Get()?.Play("hookshotOnHit");
            _rayDepth++;
            try
            {
                __instance.Shoot(start, reflected, ref hasFired, chadId);
            }
            finally
            {
                _rayDepth--;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ShootQuantum), nameof(ShootQuantum.BlinkObject))]
    private static class MirrorBlinkRays
    {
        private static bool Prefix(ShootQuantum __instance, RaycastInformation hit, Vec2 firepoint, Vec2 direction, Vec2 normal, ref bool hasFired)
        {
            if (!Mirror(hit, direction, out Vec2 start, out Vec2 reflected, out int chadId))
                return true;
            __instance.spawnRayCastEffect((Vector2)firepoint, (Vector2)direction, (float)hit.nearDist, didHit: false, normal, reflected: true);
            __instance.rayParticle.Stop();
            AudioManager.Get()?.Play("hookshotOnHit");
            _rayDepth++;
            try
            {
                __instance.Shoot(start, reflected, ref hasFired, chadId);
            }
            finally
            {
                _rayDepth--;
            }
            return false;
        }
    }

    // --- Reveal ---

    /// <summary>Invisible slimes near a shining chad are drawn fully visible.</summary>
    [HarmonyPatch(typeof(Invisibility), nameof(Invisibility.SetAlphaBasedOnPlayerInvisibiltyTime))]
    private static class RevealInvisible
    {
        private static void Postfix(Player player, SpriteRenderer spriteRen)
        {
            if (player == null || spriteRen == null || !player.isInvisible || ChadAbility.ShiningByPlayer.Count == 0)
                return;
            foreach (ChadAbility chad in ChadAbility.ShiningByPlayer.Values)
            {
                if (!Usable(chad) || chad.PlayerId == player.Id)
                    continue;
                Fix radius = Fix.Max((Fix)ChadSettings.RevealRadius, Fix.Zero) * chad.Scale;
                if (Vec2.SqrMagnitude(player.Position - chad.AuraCenter) > radius * radius)
                    continue;
                Color color = spriteRen.color;
                color.a = 1f;
                spriteRen.color = color;
                return;
            }
        }
    }
}
