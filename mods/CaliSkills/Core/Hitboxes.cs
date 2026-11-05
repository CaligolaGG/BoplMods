using System.Collections.Generic;
using UnityEngine;

namespace CaliSkills.Core;

/// <summary>
/// "Show hitboxes" (CustomRules' checkbox in the abilities window, <see cref="CustomRulesStats.ShowHitboxes"/>): skills
/// call <see cref="Circle"/> / <see cref="Capsule"/> / <see cref="Box"/> / <see cref="Sector"/> from their LateUpdate while a hitbox is live, and it's drawn in
/// red (see-through fill, solid outline) for that frame. Visual only. Renderers are pooled on a DontDestroyOnLoad
/// object and hidden again at the start of every frame (Update runs before any LateUpdate).
/// </summary>
internal sealed class Hitboxes : MonoBehaviour
{
    private static Hitboxes _instance;
    private static Sprite _disc;
    private static Sprite _bar;
    private static Sprite _box;

    private enum Kind
    {
        Disc,
        Bar,
        Box,
        Sector,
    }

    private readonly List<SpriteRenderer> _discs = new();
    private readonly List<SpriteRenderer> _bars = new();
    private readonly List<SpriteRenderer> _boxes = new();
    private readonly List<SpriteRenderer> _sectors = new();
    private static readonly Dictionary<int, Sprite> SectorSprites = new();
    private int _discsUsed;
    private int _barsUsed;
    private int _boxesUsed;
    private int _sectorsUsed;

    /// <summary>Whether hitboxes are drawn (the host's "Show hitboxes" setting; off without CustomRules).</summary>
    internal static bool Enabled => CustomRulesStats.ShowHitboxes;

    /// <summary>A filled circle (world units), drawn over <paramref name="reference"/>'s sorting layer if given.</summary>
    internal static void Circle(Vector2 center, float radius, SpriteRenderer reference = null)
    {
        if (!Enabled || radius <= 0f)
            return;
        SpriteRenderer renderer = Get().Next(_disc, Kind.Disc);
        renderer.transform.SetPositionAndRotation(center, Quaternion.identity);
        renderer.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
        Sort(renderer, reference);
    }

    /// <summary>A capsule: every point within <paramref name="radius"/> of the segment a–b.</summary>
    internal static void Capsule(Vector2 a, Vector2 b, float radius, SpriteRenderer reference = null)
    {
        if (!Enabled || radius <= 0f)
            return;
        Circle(a, radius, reference);
        Circle(b, radius, reference);
        float length = Vector2.Distance(a, b);
        if (length <= 0.001f)
            return;
        SpriteRenderer renderer = Get().Next(_bar, Kind.Bar);
        renderer.transform.SetPositionAndRotation((a + b) / 2f, Quaternion.Euler(0f, 0f, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg));
        renderer.transform.localScale = new Vector3(length, radius * 2f, 1f);
        Sort(renderer, reference);
    }

    /// <summary>A rectangle centered on <paramref name="center"/>, its height along <paramref name="up"/>.</summary>
    internal static void Box(Vector2 center, Vector2 up, float width, float height, SpriteRenderer reference = null)
    {
        if (!Enabled || width <= 0f || height <= 0f)
            return;
        SpriteRenderer renderer = Get().Next(_box, Kind.Box);
        float angle = Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f;
        renderer.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));
        renderer.transform.localScale = new Vector3(width, height, 1f);
        Sort(renderer, reference);
    }

    /// <summary>A cone (circle sector) from <paramref name="apex"/> along <paramref name="direction"/>, <paramref name="angle"/> degrees wide.</summary>
    internal static void Sector(Vector2 apex, Vector2 direction, float length, float angle, SpriteRenderer reference = null)
    {
        if (!Enabled || length <= 0f || angle <= 0f)
            return;
        int key = Mathf.Clamp(Mathf.RoundToInt(angle), 1, 360);
        if (!SectorSprites.TryGetValue(key, out Sprite sprite) || sprite == null)
        {
            float half = key * 0.5f * Mathf.Deg2Rad;
            sprite = SpriteFactory.Square(128, 1f, "HitboxSector" + key, (u, v) =>
            {
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Abs(Mathf.Atan2(v, u));
                if (d > 0.5f || a > half)
                    return default;
                // Outline: the arc and the two straight sides.
                bool edge = d > 0.485f || (half - a) * d < 0.012f;
                return edge ? new Color32(255, 0, 0, 255) : new Color32(255, 0, 0, 90);
            });
            SectorSprites[key] = sprite;
        }
        SpriteRenderer renderer = Get().Next(sprite, Kind.Sector);
        renderer.sprite = sprite;
        renderer.transform.SetPositionAndRotation(apex, Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        renderer.transform.localScale = new Vector3(length * 2f, length * 2f, 1f);
        Sort(renderer, reference);
    }

    private static void Sort(SpriteRenderer renderer, SpriteRenderer reference)
    {
        if (reference != null)
        {
            renderer.sortingLayerID = reference.sortingLayerID;
            renderer.sortingOrder = reference.sortingOrder + 50;
        }
        else
        {
            renderer.sortingOrder = 1000;
        }
    }

    private static Hitboxes Get()
    {
        if (_instance != null)
            return _instance;
        var go = new GameObject("CaliSkills.Hitboxes");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<Hitboxes>();
        _disc ??= SpriteFactory.Square(128, 1f, "HitboxDisc", (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v);
            return d > 0.5f ? default : d > 0.47f ? new Color32(255, 0, 0, 255) : new Color32(255, 0, 0, 90);
        });
        _bar ??= SpriteFactory.Square(64, 1f, "HitboxBar", (u, v) =>
            Mathf.Abs(v) > 0.47f ? new Color32(255, 0, 0, 255) : new Color32(255, 0, 0, 90));
        _box ??= SpriteFactory.Square(64, 1f, "HitboxBox", (u, v) =>
            Mathf.Abs(u) > 0.47f || Mathf.Abs(v) > 0.47f ? new Color32(255, 0, 0, 255) : new Color32(255, 0, 0, 90));
        return _instance;
    }

    private SpriteRenderer Next(Sprite sprite, Kind kind)
    {
        List<SpriteRenderer> pool = kind switch { Kind.Disc => _discs, Kind.Bar => _bars, Kind.Box => _boxes, _ => _sectors };
        int used = kind switch { Kind.Disc => _discsUsed++, Kind.Bar => _barsUsed++, Kind.Box => _boxesUsed++, _ => _sectorsUsed++ };
        if (used >= pool.Count)
        {
            var renderer = new GameObject("Hitbox").AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, worldPositionStays: false);
            renderer.sprite = sprite;
            pool.Add(renderer);
        }
        SpriteRenderer next = pool[used];
        next.enabled = true;
        return next;
    }

    private void Update()
    {
        for (int i = 0; i < _discsUsed && i < _discs.Count; i++)
            _discs[i].enabled = false;
        for (int i = 0; i < _barsUsed && i < _bars.Count; i++)
            _bars[i].enabled = false;
        for (int i = 0; i < _boxesUsed && i < _boxes.Count; i++)
            _boxes[i].enabled = false;
        for (int i = 0; i < _sectorsUsed && i < _sectors.Count; i++)
            _sectors[i].enabled = false;
        _discsUsed = 0;
        _sectorsUsed = 0;
        _barsUsed = 0;
        _boxesUsed = 0;
    }
}
