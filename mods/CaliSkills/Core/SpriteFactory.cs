using System;
using UnityEngine;

namespace CaliSkills.Core;

/// <summary>Builds sprites at runtime from a per-pixel color function, so skills need no asset files.</summary>
internal static class SpriteFactory
{
    /// <summary>
    /// Creates a sprite with the same size, pivot and pixels-per-unit as <paramref name="reference"/>
    /// (e.g. a vanilla ability icon), so it lines up with the game's own sprites.
    /// </summary>
    /// <param name="paint">(u, v) in [-0.5, 0.5] with v up, returns the pixel color.</param>
    internal static Sprite LikeReference(Sprite reference, string name, Func<float, float, Color32> paint)
    {
        int w = Mathf.Max(16, Mathf.RoundToInt(reference.rect.width));
        int h = Mathf.Max(16, Mathf.RoundToInt(reference.rect.height));
        Vector2 pivot = new Vector2(reference.pivot.x / reference.rect.width, reference.pivot.y / reference.rect.height);
        float pixelsPerUnit = reference.pixelsPerUnit * (w / reference.rect.width);
        return Create(w, h, pivot, pixelsPerUnit, name, paint);
    }

    /// <summary>Creates a square sprite of <paramref name="sizePixels"/> that is <paramref name="worldSize"/> units wide.</summary>
    internal static Sprite Square(int sizePixels, float worldSize, string name, Func<float, float, Color32> paint) =>
        Create(sizePixels, sizePixels, new Vector2(0.5f, 0.5f), sizePixels / worldSize, name, paint);

    private static Sprite Create(int w, int h, Vector2 pivot, float pixelsPerUnit, string name, Func<float, float, Color32> paint)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Bilinear };
        var pixels = new Color32[w * h];
        float s = Mathf.Min(w, h);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = paint((x + 0.5f - w / 2f) / s, (y + 0.5f - h / 2f) / s);
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, pixelsPerUnit);
        sprite.name = name;
        return sprite;
    }

    /// <summary>A shaded ball: dark outline, light body, highlight. Used for the Pinball icon and projectile.</summary>
    internal static Color32 Ball(float u, float v, float radius)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        if (d > radius)
            return default;
        if (d > radius * 0.84f)
            return new Color32(30, 30, 40, 255);

        // Light from the top-left.
        float hx = u + radius * 0.35f, hy = v - radius * 0.35f;
        if (hx * hx + hy * hy < radius * radius * 0.06f)
            return new Color32(255, 255, 255, 255);

        float shade = Mathf.Clamp01(1f - (d / radius) * 0.45f - (v < 0 ? -v / radius * 0.25f : 0f));
        byte c = (byte)(140 + 100 * shade);
        return new Color32(c, c, (byte)Mathf.Min(255, c + 12), 255);
    }
}
