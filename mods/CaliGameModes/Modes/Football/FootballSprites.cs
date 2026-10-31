using System.Collections.Generic;
using CaliSkills.Core;
using UnityEngine;

namespace CaliGameModes.Modes.Football;

/// <summary>Runtime sprites: the classic black and white football, and the goal (posts, crossbar, net).</summary>
internal static class FootballSprites
{
    private const int BallPixels = 96;
    private const float GoalPixelsPerUnit = 12f;

    private static readonly Dictionary<float, Sprite> Balls = new();
    private static readonly Dictionary<(int, int, int), Sprite> Goals = new();

    private static readonly Color32 Outline = new(25, 25, 30, 255);
    private static readonly Color32 White = new(250, 250, 250, 255);
    private static readonly Color32 Shade = new(205, 210, 220, 255);
    private static readonly Color32 Patch = new(30, 30, 35, 255);
    private static readonly Color32 Post = new(245, 245, 245, 255);
    private static readonly Color32 PostEdge = new(60, 60, 70, 255);
    private static readonly Color32 Net = new(235, 235, 240, 150);

    /// <summary>A football of <paramref name="radius"/> (world units), for <see cref="CaliSkills.Abilities.Pinball.PinballSkill.SpawnBall"/>.</summary>
    internal static Sprite Ball(float radius)
    {
        if (!Balls.TryGetValue(radius, out Sprite sprite) || sprite == null)
            Balls[radius] = sprite = SpriteFactory.Square(BallPixels, radius * 2f, "Football", PaintBall);
        return sprite;
    }

    /// <summary>
    /// White ball, a black pentagon in the middle and five more around it (cut by the edge), thin seams between,
    /// a dark outline and a soft shade at the bottom right.
    /// </summary>
    private static Color32 PaintBall(float u, float v)
    {
        const float r = 0.5f;
        float d = Mathf.Sqrt(u * u + v * v);
        if (d > r)
            return default;
        if (d > r * 0.92f)
            return Outline;

        // Seen on a sphere: patches near the edge look squashed towards it.
        float squash = 1f + 0.9f * Mathf.Pow(d / r, 3f);

        if (Pentagon(u, v, 0f, 0f, 0.13f, Mathf.PI / 2f))
            return Patch;
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.PI / 2f + i * 2f * Mathf.PI / 5f + Mathf.PI / 5f;
            float cx = Mathf.Cos(angle) * 0.36f, cy = Mathf.Sin(angle) * 0.36f;
            if (Pentagon(u, v, cx, cy, 0.11f * squash * 0.8f, angle + Mathf.PI))
                return Patch;
        }

        // Seams: from each corner of the middle pentagon outwards.
        for (int i = 0; i < 5; i++)
        {
            float angle = Mathf.PI / 2f + i * 2f * Mathf.PI / 5f;
            float ax = Mathf.Cos(angle), ay = Mathf.Sin(angle);
            float along = u * ax + v * ay, across = Mathf.Abs(-u * ay + v * ax);
            if (along > 0.13f && along < 0.3f && across < 0.008f)
                return Outline;
        }

        bool shaded = u - v > 0.45f;
        return shaded ? Shade : White;
    }

    private static bool Pentagon(float u, float v, float cx, float cy, float radius, float rotation)
    {
        float x = u - cx, y = v - cy;
        // Corners at rotation + k × sector, so the sides' normals sit halfway between: a = angle from the nearest one.
        float sector = 2f * Mathf.PI / 5f;
        float a = Mathf.Repeat(Mathf.Atan2(y, x) - rotation, sector) - sector / 2f;
        float apothem = radius * Mathf.Cos(Mathf.PI / 5f);
        return Mathf.Sqrt(x * x + y * y) * Mathf.Cos(a) <= apothem;
    }

    /// <summary>
    /// A goal <paramref name="width"/> × <paramref name="height"/> units, open towards the middle of the map: crossbar
    /// on top, a post on both sides (the back one thicker), the net hatched in between. <paramref name="rightGoal"/>
    /// mirrors it. Pivot at the bottom left.
    /// </summary>
    internal static Sprite Goal(float width, float height, bool rightGoal)
    {
        int w = Mathf.Clamp(Mathf.RoundToInt(width * GoalPixelsPerUnit), 16, 1024);
        int h = Mathf.Clamp(Mathf.RoundToInt(height * GoalPixelsPerUnit), 16, 1024);
        (int, int, int) key = (w, h, rightGoal ? 1 : 0);
        if (Goals.TryGetValue(key, out Sprite sprite) && sprite != null)
            return sprite;

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                // Units from the back (outer side) and from the bottom.
                float back = (rightGoal ? w - 1 - x : x) / GoalPixelsPerUnit;
                pixels[y * w + x] = PaintGoal(back, y / GoalPixelsPerUnit, width, height);
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        tex.hideFlags = HideFlags.DontUnloadUnusedAsset;

        sprite = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, GoalPixelsPerUnit);
        sprite.name = "FootballGoal";
        sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        Goals[key] = sprite;
        return sprite;
    }

    private static Color32 PaintGoal(float back, float up, float width, float height)
    {
        const float backPost = 0.9f, frontPost = 0.55f, bar = 0.8f, edge = 0.18f;

        // Crossbar along the top, posts down both sides (outlined).
        if (up > height - bar)
            return up > height - edge || up < height - bar + edge || back < edge || back > width - edge ? PostEdge : Post;
        if (back < backPost)
            return back < edge || back > backPost - edge ? PostEdge : Post;
        if (back > width - frontPost)
            return back > width - edge || back < width - frontPost + edge ? PostEdge : Post;

        // Net: a diagonal grid, fading towards the open front.
        const float cell = 1.1f, line = 0.12f;
        float a = Mathf.Repeat(back + up, cell), b = Mathf.Repeat(back - up, cell);
        if (a < line || b < line)
        {
            Color32 net = Net;
            net.a = (byte)(Net.a * Mathf.Lerp(1f, 0.55f, back / width));
            return net;
        }
        return default;
    }
}
