using CaliSkills.Core;
using UnityEngine;

namespace CaliSkills.Abilities.Chad;

/// <summary>Chad mode: become the Throw ability's muscly slime and get three one-time moves (see <see cref="ChadAbility"/>).</summary>
internal sealed class ChadSkill : Skill
{
    /// <summary>
    /// The Throw ability's muscly slime animations (MachoThrow2: enter, grab, pickup, idle, idleToWalk, walk, turn,
    /// beginThrow, exit), or null.
    /// </summary>
    internal static AnimationData ThrowerAnimations { get; private set; }

    /// <summary>The thrower form's own transform scale (its sprites are drawn at this × the player's scale).</summary>
    internal static float ThrowerScale { get; private set; } = 1f;

    /// <summary>Slam, punch, aura farm icons (ability-indicator sized).</summary>
    internal static Sprite[] MoveIcons { get; private set; }

    /// <summary>A soft golden glow, 1 unit across, and a ring of light rays, 1 unit across.</summary>
    internal static Sprite GlowSprite { get; private set; }
    internal static Sprite RaysSprite { get; private set; }

    /// <summary>A yellow four-pointed star, 1 unit across (stunned slimes' stars).</summary>
    internal static Sprite StarSprite { get; private set; }

    public override string Name => "Chad mode";

    // The slam squashes, the punch blows players up.
    public override bool IsOffensive => true;

    public override System.Type SettingsType => typeof(ChadSettings);

    // Everything is read live.

    /// <summary>The icon is Assets/chad_icon.png (a square crop of the user's picture); a drawn bicep if it can't load.</summary>
    public override Sprite CreateIcon(NamedSpriteList abilityList)
    {
        Sprite reference = abilityList.sprites[2].sprite;
        try
        {
            return EmbeddedAssets.LoadRoundIconLike("chad_icon.png", reference, 0.95f);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"Chad mode: icon not loaded ({e.Message}), using a drawn one.");
            return SpriteFactory.LikeReference(reference, Name, (u, v) => Disc(u, v, Bicep(u, v), new Color32(250, 205, 70, 255)));
        }
    }

    /// <summary>
    /// The "prefab" the game instantiates for every player that picked the skill: under the inactive prefab root, so its
    /// own Awake never runs; FixTransform.InstantiateFixed activates each clone, which registers it.
    /// </summary>
    public override GameObject CreatePrefab(NamedSpriteList abilityList)
    {
        FindThrow(abilityList);
        Sprite reference = abilityList.sprites[2].sprite;
        MoveIcons = new[]
        {
            SpriteFactory.LikeReference(reference, "ChadSlam", (u, v) => Disc(u, v, Slam(u, v), new Color32(200, 120, 70, 255))),
            SpriteFactory.LikeReference(reference, "ChadPunch", (u, v) => Disc(u, v, Punch(u, v), new Color32(230, 80, 70, 255))),
            SpriteFactory.LikeReference(reference, "ChadAura", (u, v) => Disc(u, v, Sparkle(u, v), new Color32(150, 110, 230, 255))),
        };
        GlowSprite = SpriteFactory.Square(256, 1f, "ChadGlow", PaintGlow);
        RaysSprite = SpriteFactory.Square(256, 1f, "ChadRays", PaintRays);
        StarSprite = SpriteFactory.Square(64, 1f, "ChadStar", PaintStar);

        var go = new GameObject(Name);
        go.transform.SetParent(SkillRegistry.PrefabRoot, worldPositionStays: false);
        go.AddComponent<FixTransform>();
        go.AddComponent<ChadAbility>();
        go.AddComponent<ChadVisuals>();
        return go;
    }

    private static void FindThrow(NamedSpriteList list)
    {
        int index = list.IndexOf("Throw");
        MachoThrow2 macho = index >= 0 ? list.sprites[index].associatedGameObject?.GetComponentInChildren<MachoThrow2>(includeInactive: true) : null;
        if (macho == null)
        {
            // Not under that name: any entry with the component.
            foreach (NamedSprite entry in list.sprites)
            {
                macho = entry.associatedGameObject != null ? entry.associatedGameObject.GetComponentInChildren<MachoThrow2>(includeInactive: true) : null;
                if (macho != null)
                    break;
            }
        }
        ThrowerAnimations = macho != null ? macho.animData : null;
        if (macho != null)
            ThrowerScale = Mathf.Max(0.01f, Mathf.Abs(macho.transform.localScale.y));
        if (ThrowerAnimations == null)
            Plugin.Log.LogWarning("Chad mode: Throw ability animations not found, the chad looks like the slime.");
        else
            Plugin.Log.LogInfo($"Chad mode: Throw ability animations found (form scale {ThrowerScale}).");
    }

    // --- Drawings ((u, v) in -0.5..0.5, v up) ---

    private static readonly Color32 Outline = new(30, 30, 40, 255);
    private static readonly Color32 Skin = new(255, 225, 180, 255);
    private static readonly Color32 White = new(255, 255, 255, 255);

    private static Color32 Disc(float u, float v, Color32? symbol, Color32 background)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        if (d >= 0.47f)
            return default;
        if (d > 0.45f)
            return Outline;
        return symbol ?? background;
    }

    /// <summary>A rounded box with an outline: inside color, outline, or null.</summary>
    private static Color32? Box(float x, float y, float halfW, float halfH, float round, Color32 fill)
    {
        float qx = Mathf.Abs(x) - (halfW - round), qy = Mathf.Abs(y) - (halfH - round);
        float d = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) +
                  Mathf.Min(Mathf.Max(qx, qy), 0f) - round;
        if (d > 0f)
            return null;
        return d > -0.025f ? Outline : fill;
    }

    /// <summary>A fist seen from the side, knuckles towards +x, centered at (0, 0), about 0.4 wide.</summary>
    private static Color32? Fist(float x, float y)
    {
        // Knuckle lines.
        Color32? fist = Box(x, y, 0.2f, 0.15f, 0.07f, Skin);
        if (fist.HasValue && fist.Value.r == Skin.r && x > 0.04f && x < 0.18f)
        {
            foreach (float line in new[] { -0.05f, 0.05f })
            {
                if (Mathf.Abs(y - line) < 0.012f)
                    return Outline;
            }
        }
        // Thumb along the bottom.
        if (!fist.HasValue)
            return Box(x + 0.02f, y + 0.16f, 0.1f, 0.05f, 0.04f, Skin);
        return fist;
    }

    /// <summary>A fist flying right with speed lines.</summary>
    private static Color32? Punch(float u, float v)
    {
        Color32? fist = Fist(u - 0.08f, v);
        if (fist.HasValue)
            return fist;
        foreach (float lane in new[] { -0.1f, 0f, 0.1f })
        {
            if (Mathf.Abs(v - lane) < 0.018f && u < -0.16f && u > -0.38f)
                return White;
        }
        return null;
    }

    /// <summary>A fist coming down onto a cracked ground.</summary>
    private static Color32? Slam(float u, float v)
    {
        // Fist rotated: knuckles down.
        Color32? fist = Fist(-(v - 0.08f), u);
        if (fist.HasValue)
            return fist;
        if (v < -0.2f && v > -0.27f && Mathf.Abs(u) < 0.36f)
            return v > -0.215f || v < -0.255f ? Outline : new Color32(120, 90, 60, 255);
        // Impact lines.
        foreach (float side in new[] { -1f, 1f })
        {
            float x = u * side;
            if (x > 0.2f && x < 0.33f && Mathf.Abs(v + 0.1f - (x - 0.2f) * 0.6f) < 0.016f)
                return White;
        }
        return null;
    }

    /// <summary>A four-pointed sparkle with a smaller one.</summary>
    private static Color32? Sparkle(float u, float v)
    {
        static bool Star(float x, float y, float size) =>
            Mathf.Pow(Mathf.Abs(x) / size, 0.5f) + Mathf.Pow(Mathf.Abs(y) / size, 0.5f) < 1f;

        if (Star(u + 0.04f, v + 0.04f, 0.33f))
            return Star(u + 0.04f, v + 0.04f, 0.26f) ? new Color32(255, 235, 120, 255) : Outline;
        if (Star(u - 0.2f, v - 0.2f, 0.13f))
            return Star(u - 0.2f, v - 0.2f, 0.1f) ? White : Outline;
        return null;
    }

    /// <summary>One four-pointed star filling the square.</summary>
    private static Color32 PaintStar(float u, float v)
    {
        static float Star(float x, float y, float size) =>
            Mathf.Pow(Mathf.Abs(x) / size, 0.5f) + Mathf.Pow(Mathf.Abs(y) / size, 0.5f);

        float d = Star(u, v, 0.48f);
        if (d >= 1f)
            return default;
        return d > 0.8f ? Outline : new Color32(255, 235, 120, 255);
    }

    /// <summary>A flexed arm (bicep) for the skill's icon.</summary>
    private static Color32? Bicep(float u, float v)
    {
        // Upper arm (horizontal) + forearm (up) + bicep bulge + fist.
        Color32? fist = Box(u - 0.17f, v - 0.2f, 0.09f, 0.08f, 0.05f, Skin);
        if (fist.HasValue)
            return fist;
        Color32? forearm = Box(u - 0.17f, v - 0.0f, 0.07f, 0.16f, 0.05f, Skin);
        if (forearm.HasValue)
            return forearm;
        float bx = u + 0.05f, by = v + 0.12f;
        float bulge = bx * bx / (0.2f * 0.2f) + by * by / (0.13f * 0.13f);
        if (bulge < 1f)
            return bulge > 0.8f ? Outline : Skin;
        return Box(u + 0.05f, v + 0.17f, 0.24f, 0.08f, 0.06f, Skin);
    }

    private static Color32 PaintGlow(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v) / 0.5f;
        float a = Mathf.Clamp01(1f - d);
        return new Color32(255, 230, 120, (byte)(255 * a * a));
    }

    private static Color32 PaintRays(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v) / 0.5f;
        if (d > 1f || d < 0.25f)
            return default;
        float angle = Mathf.Atan2(v, u);
        float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 6f)), 12f);
        float fade = (1f - d) * Mathf.Clamp01((d - 0.25f) * 4f);
        return new Color32(255, 245, 190, (byte)(255 * ray * fade));
    }
}
