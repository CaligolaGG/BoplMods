using System.Linq;
using CaliSkills.Abilities.NothingPersonal;
using UnityEngine;

namespace CaliSkills.Abilities.Fan;

/// <summary>
/// What the Fan looks like (visual only, drawn from <see cref="FanAbility"/>'s sim state; floats and Unity time are fine):
/// - the transformation: a puff of smoke, the slime squashes down and the fan pops up out of it (and back at the end);
/// - the fan: <see cref="FanSkill.BodySprite"/> with its yellow parts (<see cref="FanSkill.TintSprite"/>) in the
///   slime's color, turned towards where it blows (mirrored when blowing right, so it's never upside down), humming
///   while it blows;
/// - the wind: the trapezoid area, faint, and streaks rushing out of the fan, faster and brighter near it, and the
///   wind sound looping while it blows.
/// The real slime sprite is hidden meanwhile.
/// </summary>
internal sealed class FanVisuals : MonoBehaviour
{
    private const int StreakCount = 16;

    private FanAbility _ability;
    private GameObject _root;
    private SpriteRenderer _slimeCopy, _body, _tint, _area;
    private AudioSource _sound;
    private readonly SpriteRenderer[] _streaks = new SpriteRenderer[StreakCount];
    private readonly float[] _streakAlong = new float[StreakCount];
    private readonly float[] _streakSide = new float[StreakCount];
    private float _hum;
    private bool _hidingSlime;

    private void Awake() => _ability = GetComponent<FanAbility>();

    private void Build()
    {
        _root = new GameObject("CaliSkills.Fan");
        _slimeCopy = Renderer("SlimeCopy", null);
        _area = Renderer("WindArea", null);
        _body = Renderer("Body", FanSkill.BodySprite);
        _tint = Renderer("Tint", FanSkill.TintSprite);
        _sound = _root.AddComponent<AudioSource>();
        _sound.clip = FanSkill.WindSound;
        _sound.loop = true;
        _sound.playOnAwake = false;
        for (int i = 0; i < StreakCount; i++)
        {
            _streaks[i] = Renderer("Streak" + i, FanSkill.StreakSprite);
            Respawn(i, Random.value);
        }
    }

    private SpriteRenderer Renderer(string name, Sprite sprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root.transform, worldPositionStays: false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.enabled = false;
        return renderer;
    }

    private void Respawn(int i, float along)
    {
        _streakAlong[i] = along;
        _streakSide[i] = Random.Range(-0.9f, 0.9f);
    }

    /// <summary>A puff of smoke where the slime turns into a fan (or back).</summary>
    internal void OnTransform(bool toFan)
    {
        SlimeController slime = _ability != null ? _ability.Caster : null;
        if (slime != null && !slime.IsDestroyed)
            NothingPersonalVisuals.SpawnSmokeCloud(slime.body.position, 0.5f * (float)slime.body.fixtrans.Scale, 0.5f);
    }

    private void LateUpdate()
    {
        SlimeController slime = _ability.Caster;
        bool active = _ability.CurrentState != FanAbility.State.Idle && slime != null && !slime.IsDestroyed &&
                      slime.isActiveAndEnabled;
        if (!active)
        {
            Hide();
            return;
        }
        if (_root == null)
            Build();

        SpriteRenderer slimeRen = slime.spriteRen;
        if (slimeRen != null)
        {
            slimeRen.enabled = false;
            _hidingSlime = true;
        }

        float scale = (float)_ability.Scale;
        float size = Mathf.Max(0.1f, FanSettings.FanSize) * (float)slime.playerPhysics.radius;
        Vector2 center = (Vector2)slime.body.position;
        Vector2 dir = (Vector2)_ability.Direction;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        int layer = slimeRen != null ? slimeRen.sortingLayerID : 0;
        int order = slimeRen != null ? slimeRen.sortingOrder : 0;

        // Transformation: 0 = slime, 1 = fan.
        float transform01 = TransformProgress();

        // The slime squashing down into the floor (first half), from its current look.
        bool showSlime = transform01 < 0.5f && slimeRen != null && slimeRen.sprite != null;
        _slimeCopy.enabled = showSlime;
        if (showSlime)
        {
            float squash = transform01 / 0.5f;
            _slimeCopy.sprite = slimeRen.sprite;
            _slimeCopy.sharedMaterial = slimeRen.sharedMaterial;
            _slimeCopy.flipX = slimeRen.flipX;
            _slimeCopy.sortingLayerID = layer;
            _slimeCopy.sortingOrder = order;
            Transform t = _slimeCopy.transform;
            t.position = slimeRen.transform.position;
            t.rotation = slimeRen.transform.rotation;
            Vector3 lossy = slimeRen.transform.lossyScale;
            t.localScale = new Vector3(lossy.x * (1f + 0.4f * squash), lossy.y * (1f - squash), 1f);
        }

        // The fan popping up (second half, with a little overshoot).
        float pop = Mathf.Clamp01((transform01 - 0.4f) / 0.6f);
        float fanScale = size * (Mathf.SmoothStep(0f, 1f, pop) + 0.15f * Mathf.Sin(pop * Mathf.PI));
        bool showFan = pop > 0f;
        _body.enabled = _tint.enabled = showFan;
        if (showFan)
        {
            // The fan grows up out of the slime to its cage position (its foot on the floor).
            center = Vector2.Lerp(center, (Vector2)_ability.HeadPosition, Mathf.SmoothStep(0f, 1f, pop));
            // The sprite blows left; mirrored to blow right, so the stand stays below.
            bool right = dir.x > 0f;
            float fanAngle = right ? angle : angle - 180f;
            if (_ability.CurrentState == FanAbility.State.Blowing)
            {
                _hum += Time.deltaTime;
                fanAngle += 1.5f * Mathf.Sin(_hum * 45f);
            }
            Place(_body, center, fanAngle, fanScale, layer, order);
            Place(_tint, center, fanAngle, fanScale, layer, order + 1);
            _body.flipX = _tint.flipX = right;
            _tint.color = SlimeColor(slime);
        }

        DrawWind(dir, angle, layer, order, scale);
    }

    private float TransformProgress()
    {
        float time = Mathf.Max(0.001f, FanSettings.TransformTime);
        float t = (float)_ability.StateTime;
        return _ability.CurrentState switch
        {
            FanAbility.State.Transforming => Mathf.Clamp01(t / time),
            FanAbility.State.Returning => 1f - Mathf.Clamp01(t / time),
            _ => 1f,
        };
    }

    private static void Place(SpriteRenderer renderer, Vector2 position, float angle, float size, int layer, int order, bool flipY = false)
    {
        Transform t = renderer.transform;
        t.position = position;
        t.rotation = Quaternion.Euler(0f, 0f, angle);
        t.localScale = new Vector3(size, size, 1f);
        renderer.flipY = flipY;
        renderer.sortingLayerID = layer;
        renderer.sortingOrder = order;
    }

    /// <summary>The wind's area, streaks and sound, while it blows.</summary>
    private void DrawWind(Vector2 dir, float angle, int layer, int order, float scale)
    {
        bool blowing = _ability.CurrentState == FanAbility.State.Blowing;
        UpdateSound(blowing);
        _area.enabled = blowing;
        foreach (SpriteRenderer streak in _streaks)
            streak.enabled = blowing;
        if (!blowing)
            return;

        Vector2 origin = (Vector2)_ability.WindOrigin;
        Vector2 side = new(-dir.y, dir.x);
        float length = (float)_ability.WindLength;
        float near = Mathf.Max(0.01f, FanSettings.WindWidthNear * scale), far = Mathf.Max(0.01f, FanSettings.WindWidthFar * scale);
        float width = Mathf.Max(near, far);

        _area.sprite = FanSkill.AreaSprite(near / width);
        // The area sprite's near side is narrower than its far side; if the wind widens the other way, flip it.
        _area.flipX = near > far;
        Place(_area, origin + dir * (length / 2f), angle, 1f, layer, order - 1);
        _area.transform.localScale = new Vector3(length, width, 1f);

        float strengthNear = Mathf.Max(1f, FanSettings.StrengthNear), strengthEdge = Mathf.Max(0f, FanSettings.StrengthEdge);
        for (int i = 0; i < StreakCount; i++)
        {
            float along = _streakAlong[i];
            // Faster near the fan, slower at the edge, like the push.
            float speed = Mathf.Lerp(strengthNear, Mathf.Max(strengthEdge, strengthNear * 0.25f), along) * 0.012f;
            along += speed * Time.deltaTime;
            if (along > 1f)
            {
                Respawn(i, 0f);
                along = 0f;
            }
            _streakAlong[i] = along;

            float half = Mathf.Lerp(near, far, along) / 2f;
            Vector2 position = origin + dir * (along * length) + side * (_streakSide[i] * half);
            SpriteRenderer streak = _streaks[i];
            Place(streak, position, angle, 1f, layer, order + 4);
            float streakLength = Mathf.Lerp(1.6f, 1f, along) * scale;
            streak.transform.localScale = new Vector3(streakLength, streakLength * 2f, 1f);
            float fadeIn = Mathf.Clamp01(along / 0.08f);
            streak.color = new Color(1f, 1f, 1f, 0.75f * fadeIn * (1f - along));
        }
    }

    /// <summary>
    /// The slime's color: the player material's _ShadowColor, which is what the game itself uses as "the player's
    /// color" (Beam, Platform tint, Mine, cursor); else its team color.
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> LoggedColors = new();

    private static Color SlimeColor(SlimeController slime)
    {
        const string property = "_ShadowColor";
        Player player = PlayerHandler.Get().GetPlayer(slime.GetPlayerId());
        Material material = player?.Color;
        if (material != null && material.HasProperty(property))
        {
            if (LoggedColors.Add(material.name))
                Plugin.Log.LogInfo($"Fan: player material '{material.name}' colors: " + string.Join(", ",
                    new[] { "_MainColor", "_ShadowColor", "_ReflectionColour" }.Where(material.HasProperty)
                        .Select(n => $"{n} {material.GetColor(n)}")));
            Color c = material.GetColor(property);
            c.a = 1f;
            return c;
        }
        TeamColors colors = slime.playerCollision?.reviveEffectPrefab?.teamColors;
        if (player != null && colors != null && player.Team >= 0 && player.Team < colors.Length)
        {
            Color c = colors[player.Team].fill;
            c.a = 1f;
            return c;
        }
        return Color.white;
    }

    /// <summary>The wind sound, at the game's sound effects volume, looping while the fan blows.</summary>
    private void UpdateSound(bool blowing)
    {
        if (_sound == null || _sound.clip == null)
            return;
        AudioManager audio = AudioManager.Get();
        _sound.volume = audio != null ? audio.currentMasterVolume * audio.sfxVolume : 1f;
        if (blowing && !_sound.isPlaying)
            _sound.Play();
        else if (!blowing && _sound.isPlaying)
            _sound.Stop();
    }

    private void Hide()
    {
        UpdateSound(blowing: false);
        if (_hidingSlime)
        {
            _hidingSlime = false;
            SlimeController slime = _ability.Caster;
            if (slime != null && !slime.IsDestroyed && slime.spriteRen != null)
                slime.spriteRen.enabled = true;
        }
        if (_root != null && _root.activeSelf)
        {
            foreach (SpriteRenderer renderer in _root.GetComponentsInChildren<SpriteRenderer>())
                renderer.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (_root != null)
            Destroy(_root);
    }
}
