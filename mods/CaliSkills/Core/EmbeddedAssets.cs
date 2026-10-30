using System;
using System.IO;
using UnityEngine;

namespace CaliSkills.Core;

/// <summary>
/// Files from the mod's Assets folder, embedded in the DLL (see CaliSkills.csproj), loaded by file name.
/// Sounds are played through <see cref="SoundPlayer"/>.
/// </summary>
internal static class EmbeddedAssets
{
    private static byte[] Read(string fileName)
    {
        using Stream stream = typeof(EmbeddedAssets).Assembly.GetManifestResourceStream("CaliSkills.Assets." + fileName);
        if (stream == null)
            throw new FileNotFoundException($"Embedded asset '{fileName}' not found (is it in mods/CaliSkills/Assets?).");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>
    /// A PNG turned into an ability icon that sizes exactly like <paramref name="reference"/> everywhere: same pixel
    /// size (the character select UI draws icons at their pixel size), same pixels-per-unit and pivot (the in-match
    /// indicators use world size). The picture is drawn as a round portrait with a dark outline, centered,
    /// <paramref name="fill"/> × the icon's width across, like the round vanilla icons.
    /// </summary>
    internal static Sprite LoadRoundIconLike(string fileName, Sprite reference, float fill)
    {
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
        source.LoadImage(Read(fileName));
        source.wrapMode = TextureWrapMode.Clamp;
        source.filterMode = FilterMode.Bilinear;

        int w = Mathf.Max(16, Mathf.RoundToInt(reference.rect.width));
        int h = Mathf.Max(16, Mathf.RoundToInt(reference.rect.height));
        float radius = Mathf.Min(w, h) * Mathf.Clamp01(fill) / 2f;
        float outline = Mathf.Max(1.5f, radius * 0.07f);
        var outlineColor = new Color(0.1f, 0.18f, 0.28f, 1f);

        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - w / 2f, dy = y + 0.5f - h / 2f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float coverage = Mathf.Clamp01(radius - distance + 0.5f); // anti-aliased disc edge
                if (coverage <= 0f)
                    continue;

                Color color = distance > radius - outline
                    ? outlineColor
                    : source.GetPixelBilinear(0.5f + dx / (2f * radius), 0.5f + dy / (2f * radius));
                color.a *= coverage;
                pixels[y * w + x] = color;
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Bilinear };
        tex.SetPixels(pixels);
        tex.Apply();
        UnityEngine.Object.Destroy(source);

        Vector2 pivot = new Vector2(reference.pivot.x / reference.rect.width, reference.pivot.y / reference.rect.height);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, reference.pixelsPerUnit);
        sprite.name = Path.GetFileNameWithoutExtension(fileName);
        return sprite;
    }

    /// <summary>A 16-bit PCM WAV (mono or stereo) as an AudioClip.</summary>
    internal static AudioClip LoadWav(string fileName)
    {
        byte[] bytes = Read(fileName);
        int channels = 0, sampleRate = 0, bits = 0, dataOffset = -1, dataSize = 0;
        for (int i = 12; i + 8 <= bytes.Length;)
        {
            string id = System.Text.Encoding.ASCII.GetString(bytes, i, 4);
            int size = BitConverter.ToInt32(bytes, i + 4);
            if (id == "fmt ")
            {
                channels = BitConverter.ToInt16(bytes, i + 10);
                sampleRate = BitConverter.ToInt32(bytes, i + 12);
                bits = BitConverter.ToInt16(bytes, i + 22);
            }
            else if (id == "data")
            {
                dataOffset = i + 8;
                dataSize = Math.Min(size, bytes.Length - dataOffset);
                break;
            }
            i += 8 + size + (size & 1);
        }
        if (bits != 16 || channels < 1 || dataOffset < 0)
            throw new InvalidDataException($"'{fileName}' must be a 16-bit PCM WAV (got {bits}-bit, {channels} channels).");

        int samples = dataSize / 2;
        var data = new float[samples];
        for (int s = 0; s < samples; s++)
            data[s] = BitConverter.ToInt16(bytes, dataOffset + s * 2) / 32768f;

        AudioClip clip = AudioClip.Create(Path.GetFileNameWithoutExtension(fileName), samples / channels, channels, sampleRate, stream: false);
        clip.SetData(data, 0);
        return clip;
    }
}

/// <summary>Plays the mod's own sounds at the game's sound effects volume. Visual/audio only, never in sim logic.</summary>
internal sealed class SoundPlayer : MonoBehaviour
{
    private static SoundPlayer _instance;
    private AudioSource _source;

    internal static void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
            return;
        if (_instance == null)
        {
            // Own GameObject: the plugin's one is destroyed by the game before the main menu.
            var go = new GameObject("CaliSkills.SoundPlayer");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SoundPlayer>();
            _instance._source = go.AddComponent<AudioSource>();
            _instance._source.playOnAwake = false;
        }

        AudioManager audio = AudioManager.Get();
        float gameVolume = audio != null ? audio.currentMasterVolume * audio.sfxVolume : 1f;
        _instance._source.PlayOneShot(clip, volume * gameVolume);
    }
}
