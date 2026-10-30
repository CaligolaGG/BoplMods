using CaliSkills.Core;
using UnityEngine;

namespace CaliSkills.Abilities.Run;

/// <summary>Run: hold the button to be faster (see <see cref="RunAbility"/>).</summary>
internal sealed class RunSkill : Skill
{
    public override string Name => "Run";

    public override bool IsOffensive => false;

    public override System.Type SettingsType => typeof(RunSettings);

    // Everything is read live (speed when a run starts, cooldown every tick).

    public override Sprite CreateIcon(NamedSpriteList abilityList) =>
        SpriteFactory.LikeReference(abilityList.sprites[2].sprite, Name, PaintIcon);

    /// <summary>
    /// The "prefab" the game instantiates for every player that picked Run: under the inactive prefab root, so its own
    /// Awake never runs; FixTransform.InstantiateFixed activates each clone, which registers it.
    /// </summary>
    public override GameObject CreatePrefab(NamedSpriteList abilityList)
    {
        var go = new GameObject(Name);
        go.transform.SetParent(SkillRegistry.PrefabRoot, worldPositionStays: false);
        go.AddComponent<FixTransform>();
        go.AddComponent<RunAbility>();
        return go;
    }

    /// <summary>A double chevron with speed lines, in yellow with a dark outline.</summary>
    private static Color32 PaintIcon(float u, float v)
    {
        var fill = new Color32(255, 214, 64, 255);
        var outline = new Color32(40, 40, 60, 255);

        // Speed lines on the left.
        for (int i = -1; i <= 1; i++)
        {
            float y = i * 0.13f;
            float len = i == 0 ? 0.2f : 0.14f;
            float d = DistanceToSegment(u, v, -0.4f, y, -0.4f + len, y);
            if (d < 0.025f)
                return fill;
            if (d < 0.04f)
                return outline;
        }

        // Two chevrons pointing right.
        float best = float.MaxValue;
        foreach (float x0 in new[] { -0.12f, 0.12f })
        {
            best = Mathf.Min(best, DistanceToSegment(u, v, x0, 0.2f, x0 + 0.18f, 0f));
            best = Mathf.Min(best, DistanceToSegment(u, v, x0 + 0.18f, 0f, x0, -0.2f));
        }
        if (best < 0.045f)
            return fill;
        if (best < 0.065f)
            return outline;
        return default;
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
