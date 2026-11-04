using CaliSkills.Core;
using UnityEngine;

namespace CaliSkills.Abilities.Charge;

/// <summary>Charge: load up, then dash where you aim and knock everyone in the way (see <see cref="ChargeAbility"/>).</summary>
internal sealed class ChargeSkill : Skill
{
    /// <summary>The Meteor's hold animation (the slime curled up, shaking), or null if not found.</summary>
    internal static SpriteFrame[] MeteorFrames { get; private set; }

    /// <summary>
    /// Degrees added to the direction's angle to point the meteor sprite along it: the Meteor dives down, so its
    /// sprite's down (-90°) is where it goes.
    /// </summary>
    internal const float MeteorSpriteAngle = 90f;

    /// <summary>A soft dashed line along +x, 1 unit long and wide (tinted and stretched by its renderer).</summary>
    internal static Sprite LineSprite { get; private set; }

    public override string Name => "Charge";

    // Its hits can squash players against platforms.
    public override bool IsOffensive => true;

    public override System.Type SettingsType => typeof(ChargeSettings);

    // Everything is read live.

    public override Sprite CreateIcon(NamedSpriteList abilityList) =>
        SpriteFactory.LikeReference(abilityList.sprites[2].sprite, Name, PaintIcon);

    /// <summary>
    /// The "prefab" the game instantiates for every player that picked the skill: under the inactive prefab root, so its
    /// own Awake never runs; FixTransform.InstantiateFixed activates each clone, which registers it.
    /// </summary>
    public override GameObject CreatePrefab(NamedSpriteList abilityList)
    {
        FindMeteor(abilityList);
        LineSprite = SpriteFactory.Square(64, 1f, "ChargeLine", PaintLine);

        var go = new GameObject(Name);
        go.transform.SetParent(SkillRegistry.PrefabRoot, worldPositionStays: false);
        go.AddComponent<FixTransform>();
        go.AddComponent<ChargeAbility>();
        go.AddComponent<ChargeVisuals>();
        return go;
    }

    private static void FindMeteor(NamedSpriteList list)
    {
        foreach (NamedSprite entry in list.sprites)
        {
            MeteorSmash meteor = entry.associatedGameObject != null
                ? entry.associatedGameObject.GetComponentInChildren<MeteorSmash>(includeInactive: true)
                : null;
            if (meteor == null || meteor.animData == null)
                continue;
            try
            {
                MeteorFrames = meteor.animData.GetAnimation("hold");
                Plugin.Log.LogInfo($"Charge: Meteor hold animation found ({MeteorFrames.Length} frames).");
                return;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"Charge: Meteor hold animation not loaded ({e.Message}).");
            }
        }
        Plugin.Log.LogWarning("Charge: Meteor not found, the charge shows the slime's own sprite.");
    }

    private static Color32 PaintLine(float u, float v)
    {
        // Dashes along x, soft edges across.
        float across = 1f - Mathf.Clamp01(Mathf.Abs(v) / 0.5f);
        float dash = Mathf.Repeat((u + 0.5f) * 8f, 1f) < 0.6f ? 1f : 0f;
        return new Color32(255, 255, 255, (byte)(255 * across * dash));
    }

    /// <summary>A slime ball streaking to the top right, on an orange disc.</summary>
    private static Color32 PaintIcon(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        if (d >= 0.47f)
            return default;
        if (d > 0.45f)
            return new Color32(30, 30, 40, 255);

        // The ball, ahead (top right).
        float bx = u - 0.1f, by = v - 0.1f;
        float bd = Mathf.Sqrt(bx * bx + by * by);
        if (bd < 0.16f)
        {
            if (bd > 0.135f)
                return new Color32(30, 30, 40, 255);
            float hx = bx + 0.05f, hy = by - 0.05f;
            return hx * hx + hy * hy < 0.0012f ? new Color32(255, 255, 255, 255) : new Color32(120, 220, 110, 255);
        }

        // Speed streaks behind it (bottom left), along the diagonal.
        float along = (bx + by) * 0.7071f;
        float across = (by - bx) * 0.7071f;
        if (along < -0.12f && along > -0.42f)
        {
            foreach (float lane in new[] { -0.09f, 0f, 0.09f })
            {
                float width = 0.022f * (1f + (along + 0.12f) * 1.5f);
                if (Mathf.Abs(across - lane) < width && !(lane != 0f && along < -0.34f))
                    return new Color32(255, 250, 220, 255);
            }
        }
        return new Color32(245, 140, 50, 255);
    }
}
