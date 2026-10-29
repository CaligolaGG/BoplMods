using BoplFixedMath;
using HarmonyLib;
using UnityEngine;

namespace CustomRules.Rules;

/// <summary>
/// Player size: 3 = normal, every step above/below is one Growth ray / Shrink ray hit, applied to every player
/// when the level starts (Player.Scale is reset to 1 before every level). Uses the rays' own ScaleChanger prefab,
/// so the size changes exactly like a ray hit: same animated growth, same amount, same 0.3..3 limits, sound and flare.
/// Clones and revives share the Player, so they follow.
/// </summary>
[HarmonyPatch(typeof(GameSessionHandler), nameof(GameSessionHandler.SpawnPlayers))]
internal static class SizeRule
{
    internal const int NormalSize = 3;

    private static ShootScaleChange _growthRay;
    private static ShootScaleChange _shrinkRay;

    private static void Postfix(GameSessionHandler __instance)
    {
        int steps = BasicRules.PlayerSize.ActiveInt - NormalSize;
        if (steps == 0 || !FindRays())
            return;

        ShootScaleChange ray = steps > 0 ? _growthRay : _shrinkRay;
        foreach (SlimeController slime in __instance.slimeControllers)
        {
            Player player = slime != null ? PlayerHandler.Get().GetPlayer(slime.GetPlayerId()) : null;
            if (player == null)
                continue;

            for (int i = 0; i < Mathf.Abs(steps); i++)
            {
                // Same as ShootScaleChange.ApplyScaleChange on a slime.
                ScaleChanger changer = FixTransform.InstantiateFixed(ray.ScaleChangerPrefab, Vec2.zero);
                changer.victim = slime.hurtBox;
                changer.player = player;
            }

            SpriteRenderer sprite = slime.GetComponent<SpriteRenderer>();
            if (ray.flarePrefab != null && sprite != null && sprite.sprite != null)
                Object.Instantiate(ray.flarePrefab).Init(sprite);
        }
        AudioManager.Get()?.Play(steps > 0 ? "grow" : "shrink");
    }

    /// <summary>The rays are told apart by their effect (growth adds scale), not by name.</summary>
    private static bool FindRays()
    {
        if (_growthRay != null && _shrinkRay != null)
            return true;

        NamedSpriteList abilities = SteamManager.instance != null ? SteamManager.instance.abilityIcons : null;
        if (abilities == null)
            return false;

        foreach (NamedSprite entry in abilities.sprites)
        {
            ShootScaleChange ray = entry.associatedGameObject != null
                ? entry.associatedGameObject.GetComponentInChildren<ShootScaleChange>(includeInactive: true)
                : null;
            if (ray == null || ray.ScaleChangerPrefab == null)
                continue;
            if (ray.ScaleChangerPrefab.PlayerMultiplier > Fix.Zero)
                _growthRay ??= ray;
            else
                _shrinkRay ??= ray;
        }

        if (_growthRay == null || _shrinkRay == null)
        {
            Plugin.Log.LogWarning("Player size rule: Growth ray / Shrink ray not found in the ability list.");
            return false;
        }
        return true;
    }
}
