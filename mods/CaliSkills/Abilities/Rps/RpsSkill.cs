using CaliSkills.Core;
using UnityEngine;

namespace CaliSkills.Abilities.Rps;

/// <summary>Rock Paper Scissors: hold, release, and duel a slime in your circle (see <see cref="RpsAbility"/>).</summary>
internal sealed class RpsSkill : Skill
{
    /// <summary>The three choices, in ability button order (first / second / third ability).</summary>
    internal static readonly string[] ChoiceNames = { "Rock", "Paper", "Scissors" };

    /// <summary>Rock, paper, scissors icons (ability-indicator sized), by choice.</summary>
    internal static Sprite[] ChoiceSprites { get; private set; }

    /// <summary>The yellow challenge circle, 1 unit across.</summary>
    internal static Sprite RingSprite { get; private set; }

    /// <summary>White square / VS screen halves, 1 unit across (tinted by their renderers).</summary>
    internal static Sprite WhiteSprite { get; private set; }
    internal static Sprite LeftPanelSprite { get; private set; }
    internal static Sprite RightPanelSprite { get; private set; }

    /// <summary>The Time stop ability's spell (its inversion sphere and curves), for the same freeze effect. Null if not found.</summary>
    internal static TimeStop TimeStopTemplate { get; private set; }

    /// <summary>The Time stop cast sound, or null.</summary>
    internal static string TimeStopSound { get; private set; }

    public override string Name => "Rock Paper Scissors";

    // The loser is squashed.
    public override bool IsOffensive => true;

    public override System.Type SettingsType => typeof(RpsSettings);

    // Everything is read live.

    public override Sprite CreateIcon(NamedSpriteList abilityList) =>
        SpriteFactory.LikeReference(abilityList.sprites[2].sprite, Name, PaintIcon);

    /// <summary>
    /// The "prefab" the game instantiates for every player that picked the skill: under the inactive prefab root, so its
    /// own Awake never runs; FixTransform.InstantiateFixed activates each clone, which registers it.
    /// Also builds the duel's sprites and finds the Time stop visuals.
    /// </summary>
    public override GameObject CreatePrefab(NamedSpriteList abilityList)
    {
        Sprite reference = abilityList.sprites[2].sprite;
        ChoiceSprites = new[]
        {
            SpriteFactory.LikeReference(reference, "RpsRock", (u, v) => OnDisc(u, v, Rock(u, v))),
            SpriteFactory.LikeReference(reference, "RpsPaper", (u, v) => OnDisc(u, v, Paper(u, v))),
            SpriteFactory.LikeReference(reference, "RpsScissors", (u, v) => OnDisc(u, v, Scissors(u, v))),
        };
        RingSprite = SpriteFactory.Square(512, 1f, "RpsRing", PaintRing);
        WhiteSprite = SpriteFactory.Square(16, 1f, "RpsWhite", (_, _) => new Color32(255, 255, 255, 255));
        LeftPanelSprite = SpriteFactory.Square(256, 1f, "RpsPanelLeft", (u, v) => PaintPanel(u, v, left: true));
        RightPanelSprite = SpriteFactory.Square(256, 1f, "RpsPanelRight", (u, v) => PaintPanel(u, v, left: false));
        FindTimeStop(abilityList);

        var go = new GameObject(Name);
        go.transform.SetParent(SkillRegistry.PrefabRoot, worldPositionStays: false);
        go.AddComponent<FixTransform>();
        go.AddComponent<RpsAbility>();
        return go;
    }

    private static void FindTimeStop(NamedSpriteList list)
    {
        int index = list.IndexOf("Time stop");
        CastSpell cast = index >= 0 ? list.sprites[index].associatedGameObject?.GetComponentInChildren<CastSpell>(includeInactive: true) : null;
        TimeStopTemplate = cast != null && cast.spell != null ? cast.spell.GetComponent<TimeStop>() : null;
        TimeStopSound = cast != null && !string.IsNullOrEmpty(cast.soundEffectOnCast) ? cast.soundEffectOnCast : null;
        if (TimeStopTemplate == null)
            Plugin.Log.LogWarning("Rock Paper Scissors: Time stop spell not found, the freeze has no sphere.");
    }

    private static readonly Color32 Outline = new(30, 30, 40, 255);
    private static readonly Color32 Background = new(120, 190, 245, 255);

    private static Color32 OnDisc(float u, float v, Color32? symbol)
    {
        if (symbol.HasValue)
            return symbol.Value;
        float d = Mathf.Sqrt(u * u + v * v);
        if (d < 0.47f)
            return d > 0.45f ? Outline : Background;
        return default;
    }

    /// <summary>Rock, paper and scissors together, smaller, on a light blue disc.</summary>
    private static Color32 PaintIcon(float u, float v)
    {
        const float s = 0.58f;
        Color32? symbol = Paper((u - 0f) / s, (v - 0.17f) / s)
                          ?? Rock((u + 0.19f) / s, (v + 0.15f) / s)
                          ?? Scissors((u - 0.19f) / s, (v + 0.15f) / s);
        return OnDisc(u, v, symbol);
    }

    /// <summary>A lumpy grey stone, lit from the top left; null outside it.</summary>
    internal static Color32? Rock(float u, float v)
    {
        float x = u, y = v + 0.02f;
        float angle = Mathf.Atan2(y, x);
        float radius = 0.27f + 0.025f * Mathf.Sin(5f * angle + 0.6f) + 0.018f * Mathf.Sin(3f * angle + 2f);
        float d = Mathf.Sqrt(x * x + y * y);
        if (d > radius)
            return null;
        if (d > radius - 0.03f)
            return Outline;

        // A crack, and a highlight.
        if (Mathf.Abs(y - 0.35f * x - 0.04f) < 0.012f && x > 0.02f && x < 0.17f)
            return new Color32(90, 60, 40, 255);
        float hx = x + 0.1f, hy = y - 0.1f;
        if (hx * hx + hy * hy < 0.004f)
            return new Color32(240, 215, 180, 255);

        // Warm brown stone (a grey one looks like a disabled icon).
        float shade = Mathf.Clamp01(0.75f + (y - x) * 0.9f);
        return new Color32((byte)(140 + 70 * shade), (byte)(95 + 55 * shade), (byte)(60 + 40 * shade), 255);
    }

    /// <summary>A sheet of paper, tilted, with lines and a folded corner; null outside it.</summary>
    internal static Color32? Paper(float u, float v)
    {
        float a = -12f * Mathf.Deg2Rad;
        float x = u * Mathf.Cos(a) - v * Mathf.Sin(a), y = u * Mathf.Sin(a) + v * Mathf.Cos(a);
        const float w = 0.2f, h = 0.27f, fold = 0.1f;
        if (Mathf.Abs(x) > w || Mathf.Abs(y) > h)
            return null;

        // Folded corner (top right): cut off, with the flap drawn inside.
        float corner = (x - (w - fold)) + (y - (h - fold));
        if (corner > fold)
            return null;
        const float t = 0.025f;
        if (Mathf.Abs(x) > w - t || Mathf.Abs(y) > h - t || corner > fold - t * 1.4f)
            return Outline;
        // The flap: the folded-over triangle, edged where it meets the sheet.
        if (x > w - fold && y > h - fold)
            return x < w - fold + t || y < h - fold + t ? Outline : new Color32(205, 210, 220, 255);

        for (float line = -0.17f; line < 0.14f; line += 0.075f)
        {
            if (Mathf.Abs(y - line) < 0.012f && x > -w + 0.06f && x < w - 0.06f)
                return new Color32(150, 180, 225, 255);
        }
        return new Color32(250, 250, 250, 255);
    }

    /// <summary>Open scissors pointing up: two steel blades, red ring handles; null outside them.</summary>
    internal static Color32? Scissors(float u, float v)
    {
        var pivot = new Vector2(0f, 0.0f);
        var p = new Vector2(u, v);

        // Handles: red rings below the pivot.
        foreach (float side in new[] { -1f, 1f })
        {
            var ring = new Vector2(0.12f * side, -0.21f);
            float d = Vector2.Distance(p, ring);
            if (d < 0.095f && d > 0.045f)
                return d > 0.08f || d < 0.058f ? Outline : new Color32(225, 50, 50, 255);
            float arm = DistanceToSegment(p, pivot, ring + new Vector2(-0.03f * side, 0.07f));
            if (arm < 0.035f && d > 0.045f)
                return arm > 0.022f ? Outline : new Color32(225, 50, 50, 255);
        }

        // Pivot screw.
        float screw = Vector2.Distance(p, pivot);
        if (screw < 0.03f)
            return screw > 0.018f ? Outline : new Color32(250, 220, 80, 255);

        // Blades: tapering from the pivot to the tips.
        foreach (float side in new[] { -1f, 1f })
        {
            var tip = new Vector2(0.15f * side, 0.34f);
            Vector2 ab = tip - pivot;
            float t = Mathf.Clamp01(Vector2.Dot(p - pivot, ab) / ab.sqrMagnitude);
            float dist = Vector2.Distance(p, pivot + ab * t);
            float width = Mathf.Lerp(0.05f, 0.012f, t);
            if (dist < width + 0.014f)
                return dist > width ? Outline : new Color32(205, 215, 225, 255);
        }
        return null;
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
        return Vector2.Distance(p, a + (b - a) * t);
    }

    /// <summary>A yellow ring with a faint fill.</summary>
    private static Color32 PaintRing(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        if (d > 0.5f)
            return default;
        if (d > 0.485f)
            return new Color32(255, 225, 40, 235);
        return new Color32(255, 225, 40, 30);
    }

    /// <summary>
    /// One half of the VS band: white (tinted by the renderer) left or right of a slanted cut through the middle, with a
    /// dark edge along the cut.
    /// </summary>
    private static Color32 PaintPanel(float u, float v, bool left)
    {
        float cut = u - 0.08f * v / 0.5f;
        float side = left ? -cut : cut;
        if (side < 0f)
            return default;
        if (side < 0.012f || Mathf.Abs(v) > 0.485f)
            return new Color32(35, 35, 45, 255);
        return new Color32(255, 255, 255, 255);
    }
}
