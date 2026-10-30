using System.Collections.Generic;
using BoplFixedMath;
using CaliSkills.Core;
using HarmonyLib;
using UnityEngine;

namespace CaliSkills.Abilities.Omae;

/// <summary>
/// The poke: when an Omae ability (a ThrowItem2 with an <see cref="OmaeCone"/>) does its throw motion, every other
/// player whose body touches the cone in front of the slime gets marked. Runs in the simulation (Fire is called from
/// ThrowItem2.UpdateSim), using Fix math only.
/// </summary>
[HarmonyPatch(typeof(ThrowItem2), nameof(ThrowItem2.Fire))]
internal static class OmaePoke
{
    private static void Postfix(ThrowItem2 __instance)
    {
        if (__instance.GetComponent<OmaeCone>() != null)
            Poke(__instance);
    }

    private static void Poke(ThrowItem2 thrower)
    {
        Player owner = thrower.player;
        Vec2 dir = Vec2.NormalizedSafe(thrower.dir);
        if (owner == null || dir == Vec2.zero)
            return;

        Vec2 origin = thrower.body.position;
        Fix range = (Fix)OmaeSettings.HitboxSize * owner.Scale;
        Fix halfAngle = (Fix)Mathf.Clamp(OmaeSettings.HitboxAngle, 1f, 360f) / (Fix)2L * Fix.Pi / (Fix)180L;
        Fix cosHalf = Fix.Cos(halfAngle);

        // A player can have several bodies (Duplicator clones): each player is marked once per poke.
        var hit = new SortedSet<int>();
        foreach (PlayerCollision target in Object.FindObjectsOfType<PlayerCollision>())
        {
            int id = target.GetPlayerId();
            if (id == owner.Id || hit.Contains(id) || !target.gameObject.activeInHierarchy || target.IsInvulnerable)
                continue;
            Player player = PlayerHandler.Get().GetPlayer(id);
            if (player == null || !player.IsAlive || target.body == null)
                continue;

            Fix radius = target.physics != null ? target.physics.radius : player.Scale;
            if (InCone(target.body.position - origin, dir, range, cosHalf, radius))
                hit.Add(id);
        }

        foreach (int id in hit)
            OmaeMarks.Mark(id, owner.Id);

        if (hit.Count > 0)
            SoundPlayer.Play(OmaeSkill.Sound, OmaeSettings.SoundVolume);
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
}
