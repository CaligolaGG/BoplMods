using BoplFixedMath;
using CaliSkills.Core;
using UnityEngine;

namespace CaliSkills.Abilities.Chickenator;

/// <summary>
/// Chickenator: a copy of the vanilla Grenade ability (ThrowItem2: hold the item, aim, throw) whose grenade is a potion.
/// It keeps the grenade's fuse (it bursts in your hand if held too long) and bursts on players, platforms, explosions
/// (<see cref="PotionBurst"/>); everyone in the burst becomes a chicken for a while (<see cref="ChickenCurse"/>).
/// </summary>
internal sealed class ChickenatorSkill : Skill
{
    private Ability _ability;
    private BoplBody _potionPrefab;

    /// <summary>A chicken facing right, 1 unit across, feet near the bottom; its neck band is <see cref="BandSprite"/> (tinted).</summary>
    internal static Sprite ChickenSprite { get; private set; }
    internal static Sprite BandSprite { get; private set; }

    /// <summary>The burst's ring, 1 unit across.</summary>
    internal static Sprite RingSprite { get; private set; }

    public override string Name => "Chickenator";

    // Turns players into chickens; never kills by itself.
    public override bool IsOffensive => false;

    public override System.Type SettingsType => typeof(ChickenatorSettings);

    public override void OnSettingsChanged() => ApplyBakedSettings();

    public override Sprite CreateIcon(NamedSpriteList abilityList) =>
        SpriteFactory.LikeReference(abilityList.sprites[2].sprite, Name, PaintIcon);

    public override GameObject CreatePrefab(NamedSpriteList abilityList)
    {
        ChickenSprite = SpriteFactory.Square(256, 1f, "Chicken", (u, v) => Chicken(u, v) ?? default);
        BandSprite = SpriteFactory.Square(256, 1f, "ChickenBand", (u, v) => Band(u, v) ? new Color32(255, 255, 255, 255) : default);
        RingSprite = SpriteFactory.Square(256, 1f, "ChickenatorRing", PaintRing);

        GameObject grenadePrefab = abilityList.sprites[abilityList.IndexOf("Grenade")].associatedGameObject;
        GameObject skill = Object.Instantiate(grenadePrefab, SkillRegistry.PrefabRoot);
        skill.name = Name;
        _ability = skill.GetComponent<Ability>();

        ThrowItem2 thrower = skill.GetComponent<ThrowItem2>();
        thrower.isExplosiveGrenade = false; // not a grenade for the "hold a grenade until it explodes" achievement
        Grenade vanilla = thrower.ItemPrefab.GetComponent<Grenade>();
        Plugin.Log.LogInfo($"Vanilla Grenade: fuse {vanilla?.detonationTime}s, item radius {thrower.ItemPrefab.GetComponent<DPhysicsCircle>()?.radius}.");

        _potionPrefab = CreatePotionPrefab(thrower.ItemPrefab);
        thrower.ItemPrefab = _potionPrefab;

        // Child 1 is the grenade drawn in the slime's hand during the wind-up animation (see ThrowItem2.Awake).
        SpriteRenderer handDummy = thrower.transform.GetChild(1).GetComponentInChildren<SpriteRenderer>(includeInactive: true);
        // Drawn at the thrown potion's size: the grenade's own sprites have transparent margins, so their bounds can't
        // be used. The dummy's transform may be scaled relative to the ability (which is scaled like the item).
        if (handDummy != null)
        {
            float dummyScale = Mathf.Abs(handDummy.transform.lossyScale.x / Mathf.Max(0.0001f, skill.transform.lossyScale.x));
            handDummy.sprite = PotionOfSize(PotionSize(_potionPrefab) / Mathf.Max(0.0001f, dummyScale));
            Plugin.Log.LogInfo($"Chickenator: hand potion {PotionSize(_potionPrefab):0.##} units, dummy scale {dummyScale:0.###}.");
        }

        ApplyBakedSettings();
        return skill;
    }

    private void ApplyBakedSettings()
    {
        if (_ability == null || _potionPrefab == null)
            return;
        _ability.Cooldown = (Fix)ChickenatorSettings.AbilityCooldown;
        _potionPrefab.GetComponent<Grenade>().detonationTime = Fix.Max((Fix)ChickenatorSettings.FuseTime, (Fix)0.1f);
    }

    /// <summary>
    /// A copy of the grenade that bursts into a chicken spell: keeps Item, Grenade (fuse, owner immunity right after
    /// the throw) and its "item" layer; its explosion is replaced by <see cref="PotionBurst"/>; potion look.
    /// </summary>
    private static BoplBody CreatePotionPrefab(BoplBody grenadePrefab)
    {
        BoplBody potion = Object.Instantiate(grenadePrefab, SkillRegistry.PrefabRoot);
        potion.name = "ChickenatorPotion";

        foreach (MonoBehaviour behaviour in potion.GetComponents<MonoBehaviour>())
        {
            if (behaviour is IGrenadeDetonation)
                Object.DestroyImmediate(behaviour);
        }
        Grenade grenade = potion.GetComponent<Grenade>();
        grenade.timedExplosion = true;

        // The grenade's own visuals: fuse sparks etc. (children) and its sprite animation.
        for (int i = potion.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(potion.transform.GetChild(i).gameObject);
        SpriteAnimator animator = potion.GetComponent<SpriteAnimator>();
        if (animator != null)
            Object.DestroyImmediate(animator);
        SpriteRenderer renderer = potion.GetComponent<SpriteRenderer>() ?? potion.gameObject.AddComponent<SpriteRenderer>();
        potion.transform.localScale = Vector3.one;
        renderer.sprite = PotionOfSize(PotionSize(potion));

        potion.gameObject.AddComponent<PotionBurst>();
        return potion;
    }

    /// <summary>How tall the potion is drawn: a bit taller than the item's circle (the flask's neck sticks out).</summary>
    private static float PotionSize(BoplBody potion) =>
        (float)(potion.GetComponent<DPhysicsCircle>()?.radius ?? Fix.One) * 2.6f;

    /// <summary>The potion sprite, <paramref name="size"/> units tall.</summary>
    private static Sprite PotionOfSize(float size) =>
        SpriteFactory.Square(128, size, "ChickenatorPotion", (u, v) => Potion(u, v) ?? default);

    private static readonly Color32 Outline = new(30, 30, 40, 255);
    private static readonly Color32 Background = new(120, 190, 245, 255);
    private static readonly Color32 Liquid = new(225, 70, 200, 255);
    private static readonly Color32 LiquidDark = new(170, 40, 160, 255);
    private static readonly Color32 Glass = new(210, 235, 250, 255);
    private static readonly Color32 Feather = new(250, 250, 248, 255);
    private static readonly Color32 FeatherShade = new(215, 215, 220, 255);
    private static readonly Color32 Beak = new(250, 170, 40, 255);
    private static readonly Color32 Comb = new(225, 45, 45, 255);

    /// <summary>A round flask with pink liquid and a cork; null outside it.</summary>
    internal static Color32? Potion(float u, float v)
    {
        // Cork.
        if (Mathf.Abs(u) < 0.075f && v > 0.3f && v < 0.44f)
            return Mathf.Abs(u) > 0.055f || v > 0.42f ? Outline : new Color32(170, 115, 60, 255);
        // Neck.
        if (Mathf.Abs(u) < 0.09f && v > 0.1f && v <= 0.3f)
            return Mathf.Abs(u) > 0.065f ? Outline : Glass;
        // Bulb.
        float d = Mathf.Sqrt(u * u + (v + 0.12f) * (v + 0.12f));
        if (d > 0.32f)
            return null;
        if (d > 0.28f)
            return Outline;
        float hx = u + 0.12f, hy = v + 0.02f;
        if (hx * hx + hy * hy < 0.003f)
            return new Color32(255, 255, 255, 255);
        if (v > -0.05f)
            return Glass;
        if (v > -0.08f)
            return LiquidDark;
        return d > 0.2f && u > 0f ? LiquidDark : Liquid;
    }

    /// <summary>A white chicken facing right, feet at v ≈ -0.43; null outside it.</summary>
    internal static Color32? Chicken(float u, float v)
    {
        // Eye, beak, wattle, comb (in front of the head).
        if (Circle(u, v, 0.235f, 0.225f, 0.022f))
            return Outline;
        if (Triangle(u, v, new Vector2(0.31f, 0.235f), new Vector2(0.43f, 0.19f), new Vector2(0.31f, 0.15f)))
            return EdgeOf(u, v, 0.31f, 0.235f, 0.43f, 0.19f, 0.31f, 0.15f) ? Outline : Beak;
        if (Ellipse(u, v, 0.3f, 0.11f, 0.03f, 0.045f) is float w)
            return w > 0.6f ? Outline : Comb;
        foreach ((float cx, float cy) in new[] { (0.14f, 0.33f), (0.2f, 0.36f), (0.26f, 0.33f) })
        {
            if (Ellipse(u, v, cx, cy, 0.045f, 0.045f) is float c)
                return c > 0.65f ? Outline : Comb;
        }
        // Head.
        if (Ellipse(u, v, 0.2f, 0.2f, 0.14f, 0.14f) is float head)
            return head > 0.8f ? Outline : Feather;
        // Wing on the body.
        if (Ellipse(u, v, -0.02f, -0.06f, 0.16f, 0.1f) is float wing)
            return wing > 0.75f ? Outline : FeatherShade;
        // Body.
        if (Ellipse(u, v, -0.03f, -0.05f, 0.3f, 0.23f) is float body)
            return body > 0.88f ? Outline : Feather;
        // Tail feathers, up and to the left.
        foreach ((float cx, float cy, float rx, float ry) in new[] { (-0.33f, 0.12f, 0.06f, 0.13f), (-0.27f, 0.17f, 0.05f, 0.11f) })
        {
            if (Ellipse(u, v, cx, cy, rx, ry) is float tail)
                return tail > 0.75f ? Outline : Feather;
        }
        // Legs and feet.
        foreach ((float x0, float x1) in new[] { (-0.07f, -0.09f), (0.07f, 0.09f) })
        {
            if (DistanceToSegment(u, v, x0, -0.25f, x1, -0.41f) < 0.018f)
                return Beak;
            if (DistanceToSegment(u, v, x1 - 0.04f, -0.42f, x1 + 0.06f, -0.42f) < 0.016f)
                return Beak;
        }
        return null;
    }

    /// <summary>The neck band (drawn over the chicken in the player's color).</summary>
    private static bool Band(float u, float v) =>
        DistanceToSegment(u, v, 0.07f, 0.13f, 0.2f, 0.06f) < 0.035f && Chicken(u, v) is Color32 c && c.a > 0;

    private static Color32 PaintRing(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        if (d > 0.5f)
            return default;
        float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.44f) / 0.06f);
        float fill = d < 0.44f ? 0.25f * d / 0.44f : 0f;
        float alpha = Mathf.Max(ring, fill);
        return new Color32(255, 140, 230, (byte)(255 * alpha));
    }

    /// <summary>The potion, and a chicken coming out of it, on a light blue disc.</summary>
    private static Color32 PaintIcon(float u, float v)
    {
        Color32? chicken = Chicken((u - 0.12f) / 0.55f, (v - 0.12f) / 0.55f);
        if (chicken.HasValue)
            return chicken.Value;
        Color32? potion = Potion((u + 0.17f) / 0.6f, (v + 0.12f) / 0.6f);
        if (potion.HasValue)
            return potion.Value;
        float d = Mathf.Sqrt(u * u + v * v);
        if (d < 0.47f)
            return d > 0.45f ? Outline : Background;
        return default;
    }

    private static bool Circle(float u, float v, float cx, float cy, float r) => (u - cx) * (u - cx) + (v - cy) * (v - cy) < r * r;

    /// <summary>0..1 inside the ellipse (1 at its edge), null outside.</summary>
    private static float? Ellipse(float u, float v, float cx, float cy, float rx, float ry)
    {
        float x = (u - cx) / rx, y = (v - cy) / ry;
        float e = Mathf.Sqrt(x * x + y * y);
        return e <= 1f ? e : null;
    }

    private static bool Triangle(float u, float v, Vector2 a, Vector2 b, Vector2 c)
    {
        var p = new Vector2(u, v);
        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    private static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

    private static bool EdgeOf(float u, float v, float ax, float ay, float bx, float by, float cx, float cy) =>
        DistanceToSegment(u, v, ax, ay, bx, by) < 0.014f || DistanceToSegment(u, v, bx, by, cx, cy) < 0.014f ||
        DistanceToSegment(u, v, cx, cy, ax, ay) < 0.014f;

    private static float DistanceToSegment(float u, float v, float ax, float ay, float bx, float by)
    {
        var a = new Vector2(ax, ay);
        var b = new Vector2(bx, by);
        var p = new Vector2(u, v);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
        return Vector2.Distance(p, a + (b - a) * t);
    }
}
