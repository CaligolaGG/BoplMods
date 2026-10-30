using System.Linq;
using BoplFixedMath;
using CaliSkills.Core;
using UnityEngine;

namespace CaliSkills.Abilities.NothingPersonal;

/// <summary>
/// Visual-only pieces of "Nothing Personal Kid", built from vanilla abilities' assets when the skill's prefab is made:
/// - the marker: a copy of the Teleport ability's portal indicator, stripped of its physics/teleport logic;
/// - the teleport itself: the Teleport ability's own particles (burst where you leave, burst where you arrive, and the
///   light beam between the two positions, set up like Teleport.CastAbility does);
/// - the smoke: the Smoke ability's cloud sprite (its "enter" animation, then fading out).
/// Never used for sim decisions, so floats and Unity time are fine here.
/// </summary>
internal static class NothingPersonalVisuals
{
    private static GameObject _markerTemplate;
    private static Teleport _teleport;
    private static Sprite[] _smokeFrames;
    private static Material _smokeMaterial;

    /// <summary>Called when the skill prefab is created (the ability list is available then).</summary>
    internal static void Build(NamedSpriteList abilityList)
    {
        BuildMarker(abilityList);
        BuildSmoke(abilityList);
    }

    private static GameObject PrefabOf(NamedSpriteList list, string name)
    {
        int index = list.IndexOf(name);
        return index >= 0 ? list.sprites[index].associatedGameObject : null;
    }

    private static void BuildMarker(NamedSpriteList list)
    {
        Teleport teleport = PrefabOf(list, "Teleport")?.GetComponentInChildren<Teleport>(includeInactive: true);
        if (teleport == null || teleport.tpiPrefab == null)
        {
            Plugin.Log.LogWarning("Nothing Personal Kid: Teleport indicator not found, no marker.");
            return;
        }
        _teleport = teleport; // its particle prefabs, used as they are

        // Copy under the inactive prefab root, then remove everything but the visuals (renderers, particles).
        GameObject marker = Object.Instantiate(teleport.tpiPrefab.gameObject, SkillRegistry.PrefabRoot);
        marker.name = "NothingPersonalMarker";
        for (int pass = 0; pass < 3; pass++)
        {
            foreach (Component component in marker.GetComponentsInChildren<Component>(includeInactive: true).Reverse())
            {
                if (component is Transform || component is Renderer || component is ParticleSystem)
                    continue;
                Object.DestroyImmediate(component); // fails silently while another component requires it: next pass
            }
        }
        foreach (ParticleSystem particles in marker.GetComponentsInChildren<ParticleSystem>(includeInactive: true))
        {
            ParticleSystem.MainModule main = particles.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy; // follow the marker's (smaller) scale
        }
        _markerTemplate = marker;
    }

    private static void BuildSmoke(NamedSpriteList list)
    {
        ThrowItem2 thrower = PrefabOf(list, "Smoke")?.GetComponentInChildren<ThrowItem2>(includeInactive: true);
        FlammableSmoke smoke = thrower?.ItemPrefab?.GetComponent<SmokeGrenadeExplode2>()?.Smoke;
        if (smoke == null || smoke.animData == null)
        {
            Plugin.Log.LogWarning("Nothing Personal Kid: Smoke cloud not found, no smoke.");
            return;
        }

        _smokeFrames = smoke.animData.GetAnimation("enter").Concat(smoke.animData.GetAnimation("loop"))
            .Select(f => f.sprite).Where(s => s != null).ToArray();
        _smokeMaterial = smoke.GetComponentInChildren<SpriteRenderer>(includeInactive: true)?.sharedMaterial;
    }

    /// <summary>The Teleport marker, smaller, tinted with the player's color, at <paramref name="position"/>.</summary>
    internal static GameObject SpawnMarker(Vec2 position, Player player)
    {
        if (_markerTemplate == null)
            return null;

        GameObject marker = Object.Instantiate(_markerTemplate, (Vector3)(Vector2)position, Quaternion.identity);
        marker.transform.localScale = Vector3.one * NothingPersonalSettings.MarkerSize * (float)player.Scale;
        Color tint = player.Color != null && player.Color.HasProperty("_ShadowColor")
            ? Color.Lerp(Color.white, player.Color.GetColor("_ShadowColor"), 0.8f)
            : Color.white;
        foreach (ParticleSystem particles in marker.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.MainModule main = particles.main;
            main.startColor = tint;
        }
        marker.SetActive(true);
        return marker;
    }

    /// <summary>
    /// The Teleport ability's cast effects: a burst where you leave, a portal burst where you arrive, and the light beam
    /// between them (two particle streams pulled toward each other, as in Teleport.CastAbility).
    /// </summary>
    internal static void PlayTeleportEffects(Vec2 from, Vec2 to, Fix scale)
    {
        if (_teleport == null)
            return;

        var fromPos = (Vector3)(Vector2)from;
        var toPos = (Vector3)(Vector2)to;
        if (_teleport.onCastParticlePrefabSmall != null)
            Teleport.UpdateParticleScale(Object.Instantiate(_teleport.onCastParticlePrefabSmall, fromPos, Quaternion.identity), scale, Fix.One);
        if (_teleport.onCastParticlePrefab != null)
            Object.Instantiate(_teleport.onCastParticlePrefab, toPos, Quaternion.identity)
                .GetComponent<portalIndicatorParticle>()?.SetScale((float)scale);

        Vector3 offset = toPos - fromPos;
        if (_teleport.betweenPortalsParticlePrefab == null || offset.sqrMagnitude <= _teleport.minSqrDistanceForLineDrawing)
            return;
        const float beamSeconds = 0.25f;
        Beam(fromPos, offset, -offset.magnitude / beamSeconds);
        Beam(toPos, offset, offset.magnitude / beamSeconds);
    }

    private static void Beam(Vector3 position, Vector3 offset, float radial)
    {
        ParticleSystem.VelocityOverLifetimeModule velocity =
            Object.Instantiate(_teleport.betweenPortalsParticlePrefab, position, Quaternion.identity).velocityOverLifetime;
        velocity.orbitalOffsetX = offset.x;
        velocity.orbitalOffsetY = offset.y;
        velocity.radial = radial;
    }

    internal static void MoveMarker(GameObject marker, Vec2 position)
    {
        if (marker != null)
            marker.transform.position = (Vector3)(Vector2)position;
    }

    /// <summary>A small smoke cloud at <paramref name="position"/> that plays and fades out.</summary>
    internal static void SpawnSmoke(Vec2 position, Fix scale)
    {
        if (_smokeFrames == null || _smokeFrames.Length == 0)
            return;

        var go = new GameObject("NothingPersonalSmoke");
        go.transform.position = (Vector3)(Vector2)position;
        go.transform.localScale = Vector3.one * NothingPersonalSettings.SmokeSize * (float)scale;
        var renderer = go.AddComponent<SpriteRenderer>();
        if (_smokeMaterial != null)
            renderer.sharedMaterial = _smokeMaterial;
        renderer.sortingOrder = 10;
        go.AddComponent<SmokePuff>().Init(renderer, _smokeFrames, NothingPersonalSettings.SmokeDuration);
    }
}

/// <summary>Plays the smoke frames, then fades out and removes itself.</summary>
internal sealed class SmokePuff : MonoBehaviour
{
    private const float FramesPerSecond = 12f;
    private const float FadeSeconds = 0.6f;

    private SpriteRenderer _renderer;
    private Sprite[] _frames;
    private float _duration;
    private float _age;

    internal void Init(SpriteRenderer renderer, Sprite[] frames, float duration)
    {
        _renderer = renderer;
        _frames = frames;
        _duration = Mathf.Max(0.1f, duration);
        _renderer.sprite = frames[0];
    }

    private void Update()
    {
        _age += Time.deltaTime;
        if (_age >= _duration)
        {
            Destroy(gameObject);
            return;
        }

        _renderer.sprite = _frames[Mathf.Min(_frames.Length - 1, (int)(_age * FramesPerSecond))];
        Color color = _renderer.color;
        color.a = Mathf.Clamp01((_duration - _age) / FadeSeconds);
        _renderer.color = color;
    }
}
