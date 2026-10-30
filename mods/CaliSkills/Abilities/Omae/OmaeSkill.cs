using BoplFixedMath;
using CaliSkills.Core;
using UnityEngine;

namespace CaliSkills.Abilities.Omae;

/// <summary>
/// "Omae wa mo shindeiru" (Fist of the North Star: "you are already dead"). A copy of the vanilla Grenade ability
/// (ThrowItem2: hold to aim, release to do the throw motion) with nothing in the hand: the throw is the poke.
/// While aiming a red cone shows the hitbox (<see cref="OmaeCone"/>); on release every player in the cone is marked
/// (<see cref="OmaePoke"/>), and dies when the mark runs out (<see cref="OmaeMarks"/>).
/// </summary>
internal sealed class OmaeSkill : Skill
{
    private const int ConeSpritePixels = 256;

    /// <summary>Diameter of the round icon portrait, as a fraction of a vanilla icon's width (1 = edge to edge).</summary>
    private const float IconFill = 0.9f;

    private Ability _ability;
    private float _coneSpriteAngle = -1f;

    /// <summary>Cone of radius 1 world unit, apex at the pivot, pointing right (+x). Rebuilt when the angle changes.</summary>
    internal static Sprite ConeSprite { get; private set; }

    public override string Name => "Omae wa mo shindeiru";

    // The mark kills.
    public override bool IsOffensive => true;

    public override System.Type SettingsType => typeof(OmaeSettings);

    // Everything else is read live. Abilities are instantiated from the prefab at the start of every round.
    public override void OnSettingsChanged() => ApplyBakedSettings();

    private static AudioClip _sound;
    private static bool _soundLoaded;

    /// <summary>"Omae wa mou shindeiru", played when a poke marks someone (Assets/omae_sound.wav).</summary>
    internal static AudioClip Sound
    {
        get
        {
            if (!_soundLoaded)
            {
                _soundLoaded = true;
                try
                {
                    _sound = EmbeddedAssets.LoadWav("omae_sound.wav");
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogError($"Omae sound not loaded: {e.Message}");
                }
            }
            return _sound;
        }
    }

    public override Sprite CreateIcon(NamedSpriteList abilityList)
    {
        Sprite reference = abilityList.sprites[2].sprite;
        try
        {
            return EmbeddedAssets.LoadRoundIconLike("omae_icon.png", reference, IconFill);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogError($"Omae icon not loaded, using the drawn one: {e.Message}");
            return SpriteFactory.LikeReference(reference, Name, PaintIcon);
        }
    }

    public override GameObject CreatePrefab(NamedSpriteList abilityList)
    {
        GameObject grenadePrefab = abilityList.sprites[abilityList.IndexOf("Grenade")].associatedGameObject;
        GameObject omae = Object.Instantiate(grenadePrefab, SkillRegistry.PrefabRoot);
        omae.name = Name;

        _ability = omae.GetComponent<Ability>();

        // Keep the hold-aim-release flow and the throwing motion; no throw force, no recoil, no explosion.
        ThrowItem2 thrower = omae.GetComponent<ThrowItem2>();
        thrower.minThrowForce = Fix.Zero;
        thrower.maxThrowForce = Fix.Zero;
        thrower.knockback = Fix.Zero;
        thrower.isExplosiveGrenade = false;
        thrower.ItemPrefab = CreateHandItemPrefab(thrower.ItemPrefab);

        // Child 0: the aim indicator (replaced by the cone). Child 1: the grenade drawn in the hand during the wind-up.
        HideRenderers(thrower.transform.GetChild(0));
        HideRenderers(thrower.transform.GetChild(1));

        omae.AddComponent<OmaeCone>();

        ApplyBakedSettings();
        return omae;
    }

    /// <summary>Settings stored on the prefab or in generated sprites rather than read at runtime.</summary>
    private void ApplyBakedSettings()
    {
        if (_ability != null)
            _ability.Cooldown = (Fix)OmaeSettings.AbilityCooldown;

        if (!Mathf.Approximately(_coneSpriteAngle, OmaeSettings.HitboxAngle))
        {
            _coneSpriteAngle = OmaeSettings.HitboxAngle;
            float halfAngle = Mathf.Clamp(OmaeSettings.HitboxAngle, 1f, 360f) * 0.5f;
            ConeSprite = SpriteFactory.Square(ConeSpritePixels, 2f, "OmaeCone", (u, v) => PaintCone(u, v, halfAngle));
        }
    }

    private static void HideRenderers(Transform root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(includeInactive: true))
            renderer.enabled = false;
    }

    /// <summary>
    /// ThrowItem2 needs an item to hold and release. This one is invisible and tiny, never explodes, and removes
    /// itself as soon as it's released (see <see cref="OmaeHandItem"/>).
    /// </summary>
    private static BoplBody CreateHandItemPrefab(BoplBody grenadePrefab)
    {
        BoplBody item = Object.Instantiate(grenadePrefab, SkillRegistry.PrefabRoot);
        item.name = "OmaeHandItem";

        foreach (MonoBehaviour behaviour in item.GetComponents<MonoBehaviour>())
        {
            if (behaviour is IGrenadeDetonation)
                Object.DestroyImmediate(behaviour);
        }
        item.GetComponent<Grenade>().timedExplosion = false;

        for (int i = item.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(item.transform.GetChild(i).gameObject);
        SpriteAnimator animator = item.GetComponent<SpriteAnimator>();
        if (animator != null)
            Object.DestroyImmediate(animator);
        SpriteRenderer sprite = item.GetComponent<SpriteRenderer>();
        if (sprite != null)
            sprite.enabled = false;

        DPhysicsCircle circle = item.GetComponent<DPhysicsCircle>();
        if (circle != null)
            circle.radius = (Fix)0.05f;

        item.gameObject.AddComponent<OmaeHandItem>();
        return item;
    }

    /// <summary>Semitransparent red sector, darker along its edge.</summary>
    private static Color32 PaintCone(float u, float v, float halfAngleDegrees)
    {
        float r = Mathf.Sqrt(u * u + v * v) * 2f;
        float angle = Mathf.Abs(Mathf.Atan2(v, u) * Mathf.Rad2Deg);
        if (r > 1f || angle > halfAngleDegrees)
            return default;

        bool edge = r > 0.95f || halfAngleDegrees - angle < 1.5f;
        return edge ? new Color32(220, 20, 20, 170) : new Color32(255, 40, 40, 80);
    }

    /// <summary>A finger poking into a red dot.</summary>
    private static Color32 PaintIcon(float u, float v)
    {
        float dx = u - 0.18f, dy = v - 0.1f;
        if (dx * dx + dy * dy < 0.012f)
            return new Color32(210, 30, 30, 255);

        // Finger: a rounded bar (capsule) from the bottom-left to just before the dot.
        var a = new Vector2(-0.32f, -0.3f);
        var b = new Vector2(0.08f, 0.02f);
        var p = new Vector2(u, v);
        float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude);
        float dist = Vector2.Distance(p, a + (b - a) * t);
        if (dist < 0.075f)
            return dist > 0.055f ? new Color32(40, 30, 30, 255) : new Color32(245, 205, 170, 255);
        return default;
    }
}
