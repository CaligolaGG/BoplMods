using System.Collections.Generic;
using CaliSkills.Core;
using UnityEngine;

namespace CaliSkills.Abilities.Fan;

/// <summary>Fan: turn into an electric fan that blows everything away (see <see cref="FanAbility"/>).</summary>
internal sealed class FanSkill : Skill
{
    /// <summary>The fan's parts, 1 unit across, the fan's pivot (motor) in the middle, blowing towards +x.</summary>
    internal static Sprite StandSprite { get; private set; }
    internal static Sprite HousingSprite { get; private set; }
    internal static Sprite CageSprite { get; private set; }
    internal static Sprite BladeSprite { get; private set; }

    /// <summary>A wind streak along +x, 1 unit long.</summary>
    internal static Sprite StreakSprite { get; private set; }

    private static readonly Dictionary<int, Sprite> AreaSprites = new();

    public override string Name => "Fan";

    // Pushes, never kills by itself.
    public override bool IsOffensive => false;

    public override System.Type SettingsType => typeof(FanSettings);

    // Everything is read live.

    public override Sprite CreateIcon(NamedSpriteList abilityList) =>
        SpriteFactory.LikeReference(abilityList.sprites[2].sprite, Name, PaintIcon);

    /// <summary>
    /// The "prefab" the game instantiates for every player that picked the skill: under the inactive prefab root, so its
    /// own Awake never runs; FixTransform.InstantiateFixed activates each clone, which registers it.
    /// </summary>
    public override GameObject CreatePrefab(NamedSpriteList abilityList)
    {
        StandSprite = SpriteFactory.Square(256, 1f, "FanStand", (u, v) => Stand(u, v) ?? default);
        HousingSprite = SpriteFactory.Square(256, 1f, "FanHousing", (u, v) => Housing(u, v) ?? default);
        CageSprite = SpriteFactory.Square(256, 1f, "FanCage", (u, v) => Cage(u, v) ?? default);
        BladeSprite = SpriteFactory.Square(128, 1f, "FanBlade", PaintBlade);
        StreakSprite = SpriteFactory.Square(128, 1f, "FanStreak", PaintStreak);

        var go = new GameObject(Name);
        go.transform.SetParent(SkillRegistry.PrefabRoot, worldPositionStays: false);
        go.AddComponent<FixTransform>();
        go.AddComponent<FanAbility>();
        go.AddComponent<FanVisuals>();
        return go;
    }

    /// <summary>
    /// The wind area: a trapezoid along +x, 1 unit long and 1 unit wide at the far end, <paramref name="nearRatio"/> wide
    /// at the fan; light, fading out towards the far end. Cached per ratio (rounded).
    /// </summary>
    internal static Sprite AreaSprite(float nearRatio)
    {
        int key = Mathf.Clamp(Mathf.RoundToInt(nearRatio * 50f), 0, 50);
        if (AreaSprites.TryGetValue(key, out Sprite sprite) && sprite != null)
            return sprite;
        float near = key / 50f;
        sprite = SpriteFactory.Square(128, 1f, "FanArea" + key, (u, v) =>
        {
            float along = u + 0.5f;
            float half = Mathf.Lerp(near, 1f, along) * 0.5f;
            if (Mathf.Abs(v) > half)
                return default;
            float edge = Mathf.Clamp01((half - Mathf.Abs(v)) / 0.04f);
            float alpha = Mathf.Lerp(0.2f, 0.03f, along) * edge;
            return new Color32(220, 240, 255, (byte)(255 * alpha));
        });
        AreaSprites[key] = sprite;
        return sprite;
    }

    private static readonly Color32 Outline = new(30, 30, 40, 255);
    private static readonly Color32 Background = new(120, 190, 245, 255);
    private static readonly Color32 Plastic = new(235, 235, 240, 255);
    private static readonly Color32 Metal = new(150, 160, 175, 255);

    /// <summary>The stand: a round foot at the bottom and a pole up to the motor (middle); null outside it.</summary>
    internal static Color32? Stand(float u, float v)
    {
        // Foot: a flat ellipse.
        float fx = u / 0.3f, fy = (v + 0.4f) / 0.07f;
        float foot = fx * fx + fy * fy;
        if (foot < 1f)
            return foot > 0.6f ? Outline : Plastic;
        // Pole.
        if (v > -0.4f && v < 0f && Mathf.Abs(u) < 0.045f)
            return Mathf.Abs(u) > 0.025f ? Outline : Plastic;
        return null;
    }

    /// <summary>The motor housing behind the cage (white, tinted with the player's color); null outside it.</summary>
    internal static Color32? Housing(float u, float v)
    {
        // A rounded capsule from x -0.3 to 0.08, 0.13 tall each way.
        float x = Mathf.Clamp(u, -0.2f, 0f);
        float dx = u - x, dy = v;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        if (d > 0.13f)
            return null;
        if (d > 0.105f)
            return Outline;
        // A bit of shading, lighter on top.
        byte c = (byte)Mathf.Lerp(200f, 255f, Mathf.Clamp01(0.5f + v / 0.2f));
        return new Color32(c, c, c, 255);
    }

    /// <summary>The cage, seen from the side: a tall thin oval with grill bars, in front of the motor; null outside it.</summary>
    internal static Color32? Cage(float u, float v)
    {
        float cx = 0.14f;
        float ex = (u - cx) / 0.09f, ey = v / 0.43f;
        float e = ex * ex + ey * ey;
        if (e > 1f)
            return null;
        if (e > 0.78f)
            return Outline;
        // Grill bars across the cage, and the hub in the middle.
        float hub = Mathf.Sqrt((u - cx) * (u - cx) + v * v);
        if (hub < 0.035f)
            return hub > 0.022f ? Outline : Metal;
        if (Mathf.Repeat(v + 0.5f, 0.09f) < 0.016f)
            return new Color32(90, 95, 105, 230);
        return default;
    }

    /// <summary>One blade seen edge-on: a light lens shape, see-through (it's spinning).</summary>
    private static Color32 PaintBlade(float u, float v)
    {
        float ex = u / 0.12f, ey = v / 0.48f;
        float e = ex * ex + ey * ey;
        if (e > 1f)
            return default;
        return new Color32(210, 225, 240, (byte)(140 * (1f - e * 0.6f)));
    }

    /// <summary>A wind streak: a thin white line fading at both ends.</summary>
    private static Color32 PaintStreak(float u, float v)
    {
        float thickness = 0.035f * (1f - Mathf.Abs(u) * 1.6f);
        if (Mathf.Abs(v) > thickness || thickness <= 0f)
            return default;
        float alpha = Mathf.Clamp01(1f - Mathf.Abs(u) * 2f) * (1f - Mathf.Abs(v) / Mathf.Max(0.001f, thickness));
        return new Color32(255, 255, 255, (byte)(255 * alpha));
    }

    /// <summary>A fan blowing wind lines, on a light blue disc.</summary>
    private static Color32 PaintIcon(float u, float v)
    {
        const float s = 0.62f;
        float x = (u + 0.13f) / s, y = (v - 0.0f) / s;
        Color32? fan = Cage(x, y) is Color32 cage && cage.a > 0 ? cage
            : Housing(x, y) is Color32 housing ? (Color32?)Tint(housing, new Color32(240, 90, 80, 255))
            : Stand(x, y);
        if (fan.HasValue)
            return fan.Value;

        // Wind lines on the right.
        foreach ((float y0, float len) in new[] { (0.12f, 0.2f), (0f, 0.26f), (-0.12f, 0.2f) })
        {
            float x0 = 0.12f;
            if (u > x0 && u < x0 + len && Mathf.Abs(v - y0 - 0.02f * Mathf.Sin((u - x0) * 25f)) < 0.018f)
                return new Color32(255, 255, 255, 255);
        }

        float d = Mathf.Sqrt(u * u + v * v);
        if (d < 0.47f)
            return d > 0.45f ? Outline : Background;
        return default;
    }

    private static Color32 Tint(Color32 c, Color32 tint) =>
        new((byte)(c.r * tint.r / 255), (byte)(c.g * tint.g / 255), (byte)(c.b * tint.b / 255), c.a);
}
