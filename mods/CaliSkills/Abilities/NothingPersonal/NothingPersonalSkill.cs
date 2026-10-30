using CaliSkills.Core;
using UnityEngine;

namespace CaliSkills.Abilities.NothingPersonal;

/// <summary>"Nothing Personal Kid": teleport behind the furthest player (see <see cref="NothingPersonalAbility"/>).</summary>
internal sealed class NothingPersonalSkill : Skill
{
    public override string Name => "Nothing Personal Kid";

    // Moves you, hurts nobody.
    public override bool IsOffensive => false;

    public override System.Type SettingsType => typeof(NothingPersonalSettings);

    // Everything is read live.

    public override Sprite CreateIcon(NamedSpriteList abilityList) =>
        SpriteFactory.LikeReference(abilityList.sprites[2].sprite, Name, PaintIcon);

    /// <summary>
    /// The "prefab" the game instantiates for every player that picked the skill: under the inactive prefab root, so its
    /// own Awake never runs; FixTransform.InstantiateFixed activates each clone, which registers it.
    /// Also builds the visuals borrowed from the Teleport and Smoke abilities.
    /// </summary>
    public override GameObject CreatePrefab(NamedSpriteList abilityList)
    {
        NothingPersonalVisuals.Build(abilityList);

        var go = new GameObject(Name);
        go.transform.SetParent(SkillRegistry.PrefabRoot, worldPositionStays: false);
        go.AddComponent<FixTransform>();
        go.AddComponent<NothingPersonalAbility>();
        return go;
    }

    private static readonly Color32 Outline = new(30, 30, 40, 255);
    private static readonly Color32 Background = new(120, 190, 245, 255);

    /// <summary>A red slime holding a katana, right behind an unsuspecting yellow slime.</summary>
    private static Color32 PaintIcon(float u, float v)
    {
        // Front: the yellow slime (bottom left), drawn over everything.
        Color32? front = Slime(u, v, -0.1f, -0.13f, 0.21f, new Color32(255, 222, 40, 255), eyesLeft: true);
        if (front.HasValue)
            return front.Value;

        // Behind it: the red slime (up right), looking at the yellow one's back.
        Color32? back = Slime(u, v, 0.05f, 0.06f, 0.19f, new Color32(232, 40, 40, 255), eyesLeft: true);
        if (back.HasValue)
            return back.Value;

        // The katana, held up on the red slime's right: dark grip, then a long thin blade.
        float grip = DistanceToSegment(u, v, 0.2f, 0.02f, 0.25f, 0.14f);
        if (grip < 0.025f)
            return Outline;
        float blade = DistanceToSegment(u, v, 0.25f, 0.14f, 0.36f, 0.44f);
        if (blade < 0.022f)
            return new Color32(225, 230, 240, 255);
        if (blade < 0.036f)
            return Outline;

        // Light blue round background, like the other skills' icons.
        if (u * u + v * v < 0.47f * 0.47f)
            return Background;
        return default;
    }

    /// <summary>A round slime with an outline and two eyes looking left or right; null outside it.</summary>
    private static Color32? Slime(float u, float v, float cx, float cy, float r, Color32 body, bool eyesLeft)
    {
        float dx = u - cx, dy = v - cy;
        float d = Mathf.Sqrt(dx * dx + dy * dy);
        if (d > r)
            return null;
        if (d > r * 0.86f)
            return Outline;

        float look = eyesLeft ? -1f : 1f;
        foreach (float ex in new[] { -0.32f, 0.08f })
        {
            float eyeX = cx + (ex + 0.1f * look) * r, eyeY = cy + 0.22f * r;
            float e = Mathf.Sqrt((u - eyeX) * (u - eyeX) + (v - eyeY) * (v - eyeY));
            if (e < r * 0.14f)
                return Outline;
            if (e < r * 0.24f)
                return new Color32(255, 255, 255, 255);
        }

        // Simple shading: lighter on the top left.
        float shade = Mathf.Clamp01(1f - (dx * 0.6f - dy * 0.6f) / r * 0.25f);
        return new Color32((byte)(body.r * shade), (byte)(body.g * shade), (byte)(body.b * shade), 255);
    }

    private static float DistanceToSegment(float u, float v, float ax, float ay, float bx, float by)
    {
        var a = new Vector2(ax, ay);
        var b = new Vector2(bx, by);
        var p = new Vector2(u, v);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
        return Vector2.Distance(p, a + (b - a) * t);
    }
}
