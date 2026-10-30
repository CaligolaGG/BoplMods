using UnityEngine;

namespace SpawnBot;

/// <summary>Draws the SpawnBot icon (a small robot head) at runtime, so the mod needs no asset files.</summary>
internal static class IconFactory
{
    internal static Sprite CreateIcon(Sprite reference)
    {
        int w = Mathf.Max(16, Mathf.RoundToInt(reference.rect.width));
        int h = Mathf.Max(16, Mathf.RoundToInt(reference.rect.height));

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontUnloadUnusedAsset, // survive scene loads (Resources.UnloadUnusedAssets)
        };
        var pixels = new Color32[w * h];

        var outline = new Color32(30, 30, 40, 255);
        var face = new Color32(235, 235, 245, 255);
        var eye = new Color32(40, 190, 255, 255);

        float s = Mathf.Min(w, h);
        float cx = w / 2f;
        float cy = h * 0.45f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x - cx) / s;
                float v = (y - cy) / s;
                Color32 c = default;

                // antenna: stick + ball
                if (Mathf.Abs(u) < 0.03f && v > 0.25f && v < 0.38f) c = outline;
                if (Circle(u, v - 0.40f, 0.06f)) c = outline;

                // head: rounded square with outline
                if (RoundedBox(u, v, 0.36f, 0.27f, 0.09f)) c = outline;
                if (RoundedBox(u, v, 0.31f, 0.22f, 0.06f)) c = face;

                // eyes and mouth
                if (Circle(u + 0.13f, v - 0.05f, 0.065f) || Circle(u - 0.13f, v - 0.05f, 0.065f)) c = eye;
                if (Mathf.Abs(u) < 0.16f && Mathf.Abs(v + 0.12f) < 0.025f) c = outline;

                pixels[y * w + x] = c;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        Vector2 pivot = new Vector2(reference.pivot.x / reference.rect.width, reference.pivot.y / reference.rect.height);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, reference.pixelsPerUnit * (w / reference.rect.width));
        sprite.name = AbilityRegistration.AbilityName;
        return sprite;
    }

    private static bool Circle(float u, float v, float r) => u * u + v * v <= r * r;

    private static bool RoundedBox(float u, float v, float halfW, float halfH, float radius)
    {
        float dx = Mathf.Max(Mathf.Abs(u) - (halfW - radius), 0f);
        float dy = Mathf.Max(Mathf.Abs(v) - (halfH - radius), 0f);
        return dx * dx + dy * dy <= radius * radius;
    }
}
