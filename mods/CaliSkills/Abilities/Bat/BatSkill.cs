using CaliSkills.Core;
using UnityEngine;

namespace CaliSkills.Abilities.Bat;

/// <summary>Wooden Bat: hold to raise the bat, release to swing it (see <see cref="BatAbility"/>).</summary>
internal sealed class BatSkill : Skill
{
    /// <summary>The bat's barrel radius in <see cref="BatSprite"/>, whose length is 1 (for scaling it to the hitbox).</summary>
    internal const float SpriteHalfThickness = 0.09f;

    private static Sprite _batSprite;

    public override string Name => "Wooden Bat";

    // It squishes players between the bat and platforms...
    public override bool IsOffensive => true;

    /// <summary>...unless squishing is off (NoSquashing): then it only knocks, and doesn't count as offensive.</summary>
    public override bool IsOffensiveWith(System.Func<string, string> lobbyValue) =>
        !(lobbyValue(nameof(BatSettings.NoSquashing)) is string text ? text == bool.TrueString : BatSettings.NoSquashing);

    public override System.Type SettingsType => typeof(BatSettings);

    // Everything is read live.

    /// <summary>The bat lying along +x, 1 unit long, pivot in the middle.</summary>
    internal static Sprite BatSprite =>
        _batSprite != null ? _batSprite : _batSprite = SpriteFactory.Square(256, 1f, "WoodenBat", (u, v) => Bat(u, v) ?? default);

    public override Sprite CreateIcon(NamedSpriteList abilityList) =>
        SpriteFactory.LikeReference(abilityList.sprites[2].sprite, Name, PaintIcon);

    /// <summary>
    /// The "prefab" the game instantiates for every player that picked the skill: under the inactive prefab root, so its
    /// own Awake never runs; FixTransform.InstantiateFixed activates each clone, which registers it.
    /// </summary>
    public override GameObject CreatePrefab(NamedSpriteList abilityList)
    {
        _ = BatSprite;
        var go = new GameObject(Name);
        go.transform.SetParent(SkillRegistry.PrefabRoot, worldPositionStays: false);
        go.AddComponent<FixTransform>();
        go.AddComponent<BatAbility>();
        return go;
    }

    private static readonly Color32 Outline = new(70, 40, 20, 255);
    private static readonly Color32 Wood = new(205, 145, 80, 255);
    private static readonly Color32 WoodDark = new(175, 115, 60, 255);
    private static readonly Color32 Tape = new(60, 45, 40, 255);
    private static readonly Color32 Background = new(120, 190, 245, 255);

    /// <summary>
    /// A baseball bat along x in [-0.48, 0.48] (knob on the left, barrel on the right), or null outside it:
    /// round knob, taped handle, widening to a rounded barrel with a darker wood stripe.
    /// </summary>
    private static Color32? Bat(float x, float y)
    {
        const float left = -0.48f, right = 0.48f;
        if (x < left - 0.06f || x > right)
            return null;

        // Knob.
        float knob = Mathf.Sqrt((x - (left + 0.02f)) * (x - (left + 0.02f)) + y * y);
        if (knob < 0.045f)
            return knob > 0.03f ? Outline : Tape;

        // Radius along the bat: thin handle, taper, barrel, rounded end.
        float radius;
        if (x < -0.12f)
            radius = 0.026f;
        else if (x < 0.25f)
            radius = Mathf.Lerp(0.026f, SpriteHalfThickness, Mathf.SmoothStep(0f, 1f, (x + 0.12f) / 0.37f));
        else
            radius = SpriteHalfThickness;
        float endCenter = right - SpriteHalfThickness;
        if (x > endCenter)
            radius = Mathf.Sqrt(Mathf.Max(0f, SpriteHalfThickness * SpriteHalfThickness - (x - endCenter) * (x - endCenter)));
        if (x < left + 0.02f || Mathf.Abs(y) > radius)
            return null;

        if (Mathf.Abs(y) > radius - 0.014f || x > right - 0.012f)
            return Outline;
        if (x < -0.16f)
            return Tape;
        return y > radius * 0.2f && y < radius * 0.55f ? WoodDark : Wood;
    }

    /// <summary>The bat, diagonal, hitting a baseball, on a light blue disc.</summary>
    private static Color32 PaintIcon(float u, float v)
    {
        // The ball, top left of the barrel.
        Color32 ball = SpriteFactory.Ball(u + 0.24f, v - 0.22f, 0.11f);
        if (ball.a > 0)
            return ball;

        // The bat, rotated 45° (knob bottom left), a bit smaller than the disc.
        const float scale = 0.95f;
        float c = Mathf.Cos(-Mathf.PI / 4f), s = Mathf.Sin(-Mathf.PI / 4f);
        float x = (u * c - v * s) / scale, y = (u * s + v * c) / scale;
        Color32? bat = Bat(x, y * 0.75f);
        if (bat.HasValue)
            return bat.Value;

        float d = Mathf.Sqrt(u * u + v * v);
        if (d < 0.47f)
            return d > 0.45f ? new Color32(30, 30, 40, 255) : Background;
        return default;
    }
}
