using System.Collections.Generic;
using CaliSkills.Abilities.Chickenator;
using HarmonyLib;
using UnityEngine;

namespace CaliSkills.Abilities.Chad;

/// <summary>
/// Game patches for Chad mode:
/// - in Chad mode the slime's real abilities can't be cast (<see cref="SlimeController.isAbilityCastable"/> → false,
///   so the game never enters them and its movement isn't interrupted); the three icons show the moves instead (the
///   game cancels icon overrides every tick, so the override is swapped in where it does it), full, lit while unused;
/// - while shining (Aura farm): no ragdoll, no pushes (<see cref="PlayerPhysics.AddForce"/>), no chicken curse.
/// </summary>
internal static class ChadPatches
{
    private static readonly Dictionary<AbilityReadyIndicator, (ChadAbility Chad, int Move)> Icons = new();

    /// <summary>Starts or stops showing the moves on the caster's ability icons.</summary>
    internal static void MapIcons(ChadAbility chad, bool on)
    {
        var stale = new List<AbilityReadyIndicator>();
        foreach (KeyValuePair<AbilityReadyIndicator, (ChadAbility Chad, int Move)> entry in Icons)
        {
            if (entry.Value.Chad == chad || entry.Key == null)
                stale.Add(entry.Key);
        }
        foreach (AbilityReadyIndicator indicator in stale)
            Icons.Remove(indicator);

        SlimeController slime = chad != null ? chad.Caster : null;
        if (!on || slime == null || slime.IsDestroyed || slime.AbilityReadyIndicators == null)
            return;
        for (int i = 0; i < slime.AbilityReadyIndicators.Length && i < 3; i++)
        {
            if (slime.AbilityReadyIndicators[i] != null)
                Icons[slime.AbilityReadyIndicators[i]] = (chad, i);
        }
    }

    private static bool InMode(SlimeController slime) =>
        slime != null && ChadAbility.Active.TryGetValue(slime, out ChadAbility chad) && chad != null && chad.InMode;

    [HarmonyPatch(typeof(SlimeController), nameof(SlimeController.isAbilityCastable))]
    private static class NoRealAbilities
    {
        private static void Postfix(SlimeController __instance, int abilityIndex, ref bool __result)
        {
            if (!__result)
                return;
            if (InMode(__instance) || HeldSinceMode(__instance, abilityIndex))
                __result = false;
        }
    }

    /// <summary>Slots whose button was held when Chad mode ended: not castable until that button is released.</summary>
    private static readonly Dictionary<SlimeController, bool[]> Held = new();

    internal static void BlockUntilReleased(SlimeController slime)
    {
        Player player = PlayerHandler.Get().GetPlayer(slime.GetPlayerId());
        if (player == null)
            return;
        var held = new bool[3];
        for (int i = 0; i < 3; i++)
            held[i] = player.AbilityButtonIsDown(i);
        Held[slime] = held;
    }

    private static bool HeldSinceMode(SlimeController slime, int index)
    {
        if (Held.Count == 0 || index < 0 || index >= 3 || !Held.TryGetValue(slime, out bool[] held) || !held[index])
            return false;
        Player player = PlayerHandler.Get().GetPlayer(slime.GetPlayerId());
        if (player != null && player.AbilityButtonIsDown(index))
            return true;
        held[index] = false;
        if (!held[0] && !held[1] && !held[2])
            Held.Remove(slime);
        return false;
    }

    [HarmonyPatch(typeof(AbilityReadyIndicator), nameof(AbilityReadyIndicator.UpdateOverriden))]
    private static class MoveIcons
    {
        private static void Prefix(AbilityReadyIndicator __instance, ref Sprite overrideSprite, ref bool shouldOverride)
        {
            if (!Icons.TryGetValue(__instance, out (ChadAbility Chad, int Move) entry) || entry.Chad == null || !entry.Chad.InMode)
                return;
            Sprite icon = ChadSkill.MoveIcons != null && entry.Move < ChadSkill.MoveIcons.Length ? ChadSkill.MoveIcons[entry.Move] : null;
            if (icon == null)
                return;
            overrideSprite = icon;
            shouldOverride = true;
        }
    }

    [HarmonyPatch(typeof(AbilityReadyIndicator), nameof(AbilityReadyIndicator.SetCooldownProgress))]
    private static class MoveIconsFull
    {
        private static void Prefix(AbilityReadyIndicator __instance, ref float pDone)
        {
            if (Icons.TryGetValue(__instance, out (ChadAbility Chad, int Move) entry) && entry.Chad != null && entry.Chad.InMode)
                pDone = 1f;
        }
    }

    /// <summary>The game dims uncastable icons: unused moves are lit again (with the usual invisibility fade).</summary>
    [HarmonyPatch(typeof(SlimeController), "LateUpdate")]
    private static class UnusedMovesLit
    {
        private static void Postfix(SlimeController __instance)
        {
            if (!ChadAbility.Active.TryGetValue(__instance, out ChadAbility chad) || chad == null || !chad.InMode)
                return;
            Player player = PlayerHandler.Get().GetPlayer(__instance.GetPlayerId());
            AbilityReadyIndicator[] indicators = __instance.AbilityReadyIndicators;
            if (player == null || indicators == null)
                return;
            for (int i = 0; i < indicators.Length && i < 3; i++)
            {
                SpriteRenderer renderer = indicators[i] != null ? indicators[i].GetSpriteRen() : null;
                if (renderer != null && !chad.IsUsed(i))
                    Invisibility.SetAlphaBasedOnPlayerInvisibiltyTime(player, renderer);
            }
        }
    }

    /// <summary>
    /// In Chad mode the move icons sit under the slime: the game's own arrangement and order (each icon's fixed offset
    /// from the slime, applied every frame by <see cref="FollowTransform"/>), moved down together until the top icon is
    /// below the slime's bottom (by 0.6 icon width). This moves each icon right after the game places it.
    /// </summary>
    [HarmonyPatch(typeof(FollowTransform))]
    private static class MoveIconsLayout
    {
        private static readonly AccessTools.FieldRef<FollowTransform, Vector3> OffsetOf =
            AccessTools.FieldRefAccess<FollowTransform, Vector3>("PositionOffset");

        [HarmonyPostfix]
        [HarmonyPatch(nameof(FollowTransform.AlignPosition))]
        private static void AfterAlign(FollowTransform __instance) => Place(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(nameof(FollowTransform.AlignPositionInstantly))]
        private static void AfterAlignInstantly(FollowTransform __instance) => Place(__instance);

        private static void Place(FollowTransform follow)
        {
            if (Icons.Count == 0)
                return;
            AbilityReadyIndicator indicator = follow.GetComponent<AbilityReadyIndicator>();
            if (indicator == null || !Icons.TryGetValue(indicator, out (ChadAbility Chad, int Move) entry) ||
                entry.Chad == null || !entry.Chad.InMode)
                return;
            SlimeController slime = entry.Chad.Caster;
            SpriteRenderer renderer = indicator.GetSpriteRen();
            if (slime == null || slime.IsDestroyed || renderer == null || renderer.sprite == null || slime.AbilityReadyIndicators == null)
                return;
            // The game's own arrangement (each icon's offset), moved down as a whole until its top icon is under the slime.
            float width = renderer.sprite.bounds.size.x * (0.7f + indicator.extraScale);
            float top = float.MinValue;
            foreach (AbilityReadyIndicator other in slime.AbilityReadyIndicators)
            {
                FollowTransform otherFollow = other != null ? other.GetComponent<FollowTransform>() : null;
                if (otherFollow != null)
                    top = Mathf.Max(top, OffsetOf(otherFollow).y);
            }
            float radius = (float)slime.playerPhysics.radius;
            float shift = Mathf.Min(0f, -(radius + 0.6f * width) - top);
            Vector3 offset = OffsetOf(follow);
            Vector3 target = (Vector2)slime.body.position + new Vector2(offset.x, offset.y + shift);
            target.z = follow.transform.position.z;
            Rigidbody2D body = follow.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = target;
                body.MovePosition(target);
            }
            follow.transform.position = target;
        }
    }

    [HarmonyPatch(typeof(SlimeController), nameof(SlimeController.ApplyRagDoll))]
    private static class NoRagdollWhileShining
    {
        private static bool Prefix(SlimeController __instance) => !ChadAbility.Shining.Contains(__instance);
    }

    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.AddForce))]
    private static class NoPushWhileShining
    {
        private static bool Prefix(PlayerPhysics __instance) =>
            ChadAbility.Shining.Count == 0 || !ChadAbility.Shining.Contains(__instance.GetComponent<SlimeController>());
    }

    /// <summary>Called by <see cref="ChickenCurse.Apply"/>: shining slimes can't be turned into chickens.</summary>
    internal static bool BlocksCurse(SlimeController slime) => slime != null && ChadAbility.Shining.Contains(slime);
}
