using CaliSkills.Core;
using UnityEngine;

namespace CaliSkills.Abilities.Flamethrower;

/// <summary>
/// Flamethrower: hold to throw flames in a cone where you aim; they burn players to death, set platforms on fire, melt
/// ice platforms on ice maps and work as a jetpack on space maps (see <see cref="FlamethrowerAbility"/>).
/// Skill vs ability: this class is the definition, used once at start-up (registered by Plugin.Awake): name, offensive
/// flag, settings, icon, art and the prefab the game copies for every player who picks it. What happens in a match is
/// in <see cref="FlamethrowerAbility"/>; tuning values are in <see cref="FlamethrowerSettings"/>.
/// </summary>
internal sealed class FlamethrowerSkill : Skill
{
    /// <summary>The user's flamethrower (Assets/flamethrower.png, 512×186): 1 unit long, pointing +x, pivot at the grip.</summary>
    internal static Sprite GunSprite { get; private set; }

    /// <summary>The grip in the flamethrower sprite (pixels from its top-left), and its muzzle.</summary>
    private static readonly Vector2 GripPixel = new(75f, 110f);
    private static readonly Vector2 MuzzlePixel = new(507f, 46f);
    private static readonly Vector2 GunPixels = new(512f, 186f);

    /// <summary>The muzzle, from the grip, in gun lengths (y up).</summary>
    internal static Vector2 MuzzleFromGrip => new((MuzzlePixel.x - GripPixel.x) / GunPixels.x, (GripPixel.y - MuzzlePixel.y) / GunPixels.x);

    /// <summary>Looped while it fires (Assets/flamethrower_sound.wav, the user's MP3 as 16-bit mono).</summary>
    internal static AudioClip Sound { get; private set; }

    /// <summary>A soft round puff (white, tinted per particle), 1 unit across.</summary>
    internal static Sprite PuffSprite { get; private set; }

    /// <summary>A flame standing on +y, its base at the pivot, 1 unit tall.</summary>
    internal static Sprite FlameSprite { get; private set; }

    /// <summary>A water drop, 1 unit tall.</summary>
    internal static Sprite DropSprite { get; private set; }

    public override string Name => "Flamethrower";

    public override bool IsOffensive => true;

    public override System.Type SettingsType => typeof(FlamethrowerSettings);

    // Everything is read live.

    public override Sprite CreateIcon(NamedSpriteList abilityList)
    {
        Sprite reference = abilityList.sprites[2].sprite;
        try
        {
            return EmbeddedAssets.LoadRoundIconLike("flamethrower_icon.png", reference, 0.95f);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"Flamethrower: icon not loaded ({e.Message}), using a drawn one.");
            return SpriteFactory.LikeReference(reference, Name, PaintIcon);
        }
    }

    /// <summary>
    /// The "prefab" the game instantiates for every player that picked the skill: under the inactive prefab root, so its
    /// own Awake never runs; FixTransform.InstantiateFixed activates each clone, which registers it.
    /// </summary>
    public override GameObject CreatePrefab(NamedSpriteList abilityList)
    {
        try
        {
            GunSprite = EmbeddedAssets.LoadSprite("flamethrower.png", 1f,
                new Vector2(GripPixel.x / GunPixels.x, 1f - GripPixel.y / GunPixels.y));
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"Flamethrower: sprite not loaded: {e.Message}");
        }
        try
        {
            Sound = EmbeddedAssets.LoadWav("flamethrower_sound.wav");
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"Flamethrower: sound not loaded: {e.Message}");
        }
        PuffSprite = SpriteFactory.Square(64, 1f, "FlamePuff", PaintPuff);
        FlameSprite = SpriteFactory.Square(64, 1f, "PlatformFlame", PaintFlame);
        // The flame's pivot is its base: move it from the square's center to its bottom.
        FlameSprite = Sprite.Create(FlameSprite.texture, FlameSprite.rect, new Vector2(0.5f, 0.05f), FlameSprite.pixelsPerUnit);
        FlameSprite.name = "PlatformFlame";
        FlameSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        DropSprite = SpriteFactory.Square(32, 1f, "WaterDrop", PaintDrop);

        var go = new GameObject(Name);
        go.transform.SetParent(SkillRegistry.PrefabRoot, worldPositionStays: false);
        go.AddComponent<FixTransform>();
        go.AddComponent<FlamethrowerAbility>();
        go.AddComponent<FlamethrowerVisuals>();
        return go;
    }

    private static Color32 PaintPuff(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v) * 2f;
        if (d >= 1f)
            return default;
        float a = Mathf.Pow(1f - d, 1.3f);
        return new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(a * 1.4f)));
    }

    /// <summary>A flame: a teardrop pointing up, white-yellow core, orange, red edge.</summary>
    private static Color32 PaintFlame(float u, float v)
    {
        // v from -0.5 (base) to 0.5 (tip); width narrows to the tip.
        float y = v + 0.45f;
        if (y < 0f || y > 0.95f)
            return default;
        float halfWidth = y < 0.25f ? 0.3f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((0.25f - y) / 0.25f, 2f))) : 0.3f * (1f - (y - 0.25f) / 0.7f);
        if (halfWidth <= 0.001f)
            return default;
        float x = Mathf.Abs(u) / halfWidth;
        if (x >= 1f)
            return default;
        float heat = (1f - x) * (1f - y * 0.8f);
        Color c = heat > 0.55f ? Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 1f, 0.85f), (heat - 0.55f) / 0.45f)
            : heat > 0.25f ? Color.Lerp(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.85f, 0.3f), (heat - 0.25f) / 0.3f)
            : Color.Lerp(new Color(0.85f, 0.15f, 0.05f), new Color(1f, 0.45f, 0.1f), heat / 0.25f);
        c.a = Mathf.Clamp01((1f - x) * 4f) * Mathf.Clamp01((0.95f - y) * 6f);
        return c;
    }

    /// <summary>A water drop: round at the bottom, pointed at the top.</summary>
    private static Color32 PaintDrop(float u, float v)
    {
        float r = 0.22f;
        float cy = -0.15f;
        float dx = u, dy = v - cy;
        bool inside = dx * dx + dy * dy < r * r || (v > cy && v < 0.4f && Mathf.Abs(u) < r * (0.4f - v) / (0.4f - cy));
        if (!inside)
            return default;
        bool highlight = (u + 0.08f) * (u + 0.08f) + (v + 0.1f) * (v + 0.1f) < 0.004f;
        return highlight ? new Color32(255, 255, 255, 255) : new Color32(150, 210, 255, 230);
    }

    /// <summary>Fallback icon if the PNG can't be loaded: a cone of fire on a dark red disc.</summary>
    private static Color32 PaintIcon(float u, float v)
    {
        float d = Mathf.Sqrt(u * u + v * v);
        if (d >= 0.47f)
            return default;
        if (d > 0.45f)
            return new Color32(30, 20, 20, 255);
        float x = u + 0.3f;
        if (x > 0f && Mathf.Abs(v) < x * 0.45f)
        {
            float heat = 1f - x / 0.75f;
            return Color.Lerp(new Color(0.9f, 0.2f, 0.05f), new Color(1f, 0.95f, 0.5f), heat);
        }
        return new Color32(90, 25, 15, 255);
    }
}
