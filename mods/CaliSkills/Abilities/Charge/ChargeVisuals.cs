using BoplFixedMath;
using CaliSkills.Abilities.NothingPersonal;
using UnityEngine;

namespace CaliSkills.Abilities.Charge;

/// <summary>
/// The Charge's look (visual only, drawn from <see cref="ChargeAbility"/>'s state): while charging and dashing the
/// slime sprite is hidden and the Meteor's hold animation (<see cref="ChargeSkill.MeteorFrames"/>, in the player's
/// material; the slime's own sprite if not found) is drawn instead, pointed where it will go, growing with the charge
/// and shaking more and more like the Meteor's; a line in the player's color shows how far the dash will go. The dash
/// leaves fading afterimages. The Meteor's vibrate sound loops while charging.
/// </summary>
public class ChargeVisuals : MonoBehaviour
{
    private const float GhostInterval = 0.02f;

    private ChargeAbility _ability;
    private GameObject _root;
    private SpriteRenderer _meteor;
    private SpriteRenderer _line;
    private bool _hidingSlime;
    private float _animTime;
    private float _ghostTimer;

    private void Awake()
    {
        _ability = GetComponent<ChargeAbility>();
    }

    private void Build()
    {
        _root = new GameObject("CaliSkills.Charge");
        _meteor = new GameObject("Meteor").AddComponent<SpriteRenderer>();
        _meteor.transform.SetParent(_root.transform, worldPositionStays: false);
        _line = new GameObject("Line").AddComponent<SpriteRenderer>();
        _line.transform.SetParent(_root.transform, worldPositionStays: false);
        _line.sprite = ChargeSkill.LineSprite;
    }

    /// <summary>A puff of smoke where the dash starts.</summary>
    internal void OnLaunch()
    {
        SlimeController slime = _ability != null ? _ability.Caster : null;
        if (slime != null && !slime.IsDestroyed)
            NothingPersonalVisuals.SpawnSmokeCloud(slime.body.position, 0.45f * (float)slime.body.fixtrans.Scale, 0.4f);
        _ghostTimer = 0f;
    }

    private void Update()
    {
        if (_ability != null && _ability.CurrentState == ChargeAbility.State.Charging && !GameSessionHandler.GameIsPaused)
            AudioManager.Get()?.PlayOrContinueLoop("meteor_vibrate");
    }

    private void LateUpdate()
    {
        SlimeController slime = _ability.Caster;
        bool active = _ability.CurrentState != ChargeAbility.State.Idle && slime != null && !slime.IsDestroyed &&
                      slime.isActiveAndEnabled;
        if (!active)
        {
            Hide();
            return;
        }
        if (_root == null)
            Build();

        // Show hitboxes: the circle the dash hits players with (the grown slime).
        if (Core.Hitboxes.Enabled && _ability.CurrentState == ChargeAbility.State.Dashing)
            Core.Hitboxes.Circle((Vector2)slime.body.position, (float)(slime.playerPhysics.radius * _ability.Growth), slime.spriteRen);

        SpriteRenderer slimeRen = slime.spriteRen;
        if (slimeRen == null)
            return;
        slimeRen.enabled = false;
        _hidingSlime = true;

        bool charging = _ability.CurrentState == ChargeAbility.State.Charging;
        float charge = (float)_ability.Charge01;
        float scale = (float)_ability.Scale * (float)_ability.Growth;
        float radius = (float)slime.playerPhysics.radius;
        Vector2 dir = ((Vector2)_ability.Direction).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + ChargeSkill.MeteorSpriteAngle;

        // The meteor: animated, pointed where it goes, shaking harder as the charge grows (like the Meteor's hold).
        _meteor.sprite = CurrentSprite(slimeRen.sprite);
        _meteor.sharedMaterial = slimeRen.sharedMaterial;
        _meteor.color = new Color(1f, 1f, 1f, slimeRen.color.a);
        _meteor.sortingLayerID = slimeRen.sortingLayerID;
        _meteor.sortingOrder = slimeRen.sortingOrder;
        _meteor.enabled = true;
        Vector2 shake = charging ? Random.insideUnitCircle * (0.06f + 0.18f * charge) * radius : Vector2.zero;
        Transform t = _meteor.transform;
        t.position = (Vector2)slime.body.position + shake;
        t.rotation = Quaternion.Euler(0f, 0f, angle);
        t.localScale = new Vector3(scale, scale, 1f);

        // How far the dash will go.
        if (charging)
        {
            float length = (float)_ability.DashDistance;
            Color color = PlayerColor(slime);
            color.a = 0.35f * slimeRen.color.a;
            _line.color = color;
            _line.sortingLayerID = slimeRen.sortingLayerID;
            _line.sortingOrder = slimeRen.sortingOrder - 1;
            _line.transform.position = (Vector2)slime.body.position + dir * (length * 0.5f);
            _line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            _line.transform.localScale = new Vector3(length, radius * 0.5f * (float)_ability.Scale, 1f);
            _line.enabled = true;
        }
        else
        {
            _line.enabled = false;
            _ghostTimer -= Time.deltaTime;
            if (_ghostTimer <= 0f)
            {
                _ghostTimer = GhostInterval;
                Ghost.Spawn(_meteor);
            }
        }
    }

    /// <summary>The Meteor's hold animation frame for now (frame durations are in sim ticks, 60 a second).</summary>
    private Sprite CurrentSprite(Sprite fallback)
    {
        SpriteFrame[] frames = ChargeSkill.MeteorFrames;
        if (frames == null || frames.Length == 0)
            return fallback;
        int total = 0;
        foreach (SpriteFrame frame in frames)
            total += Mathf.Max(1, Mathf.Abs(frame.frameDuration));
        _animTime += Time.deltaTime;
        int tick = Mathf.FloorToInt(_animTime * 60f) % total;
        foreach (SpriteFrame frame in frames)
        {
            tick -= Mathf.Max(1, Mathf.Abs(frame.frameDuration));
            if (tick < 0)
                return frame.sprite != null ? frame.sprite : fallback;
        }
        return frames[frames.Length - 1].sprite ?? fallback;
    }

    /// <summary>The player's color (the player material's _ShadowColor, what the game uses for it), else white.</summary>
    private static Color PlayerColor(SlimeController slime)
    {
        Material material = PlayerHandler.Get().GetPlayer(slime.GetPlayerId())?.Color;
        if (material != null && material.HasProperty("_ShadowColor"))
            return material.GetColor("_ShadowColor");
        return Color.white;
    }

    private void Hide()
    {
        _animTime = 0f;
        if (_hidingSlime)
        {
            _hidingSlime = false;
            SlimeController slime = _ability.Caster;
            if (slime != null && !slime.IsDestroyed && slime.spriteRen != null)
                slime.spriteRen.enabled = true;
        }
        if (_root != null)
        {
            _meteor.enabled = false;
            _line.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (_root != null)
            Destroy(_root);
    }

    /// <summary>A fading copy of the meteor, left behind during the dash.</summary>
    private sealed class Ghost : MonoBehaviour
    {
        private const float Lifetime = 0.18f;
        private SpriteRenderer _renderer;
        private float _alpha;
        private float _time;

        internal static void Spawn(SpriteRenderer source)
        {
            var go = new GameObject("CaliSkills.ChargeGhost");
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale;
            Ghost ghost = go.AddComponent<Ghost>();
            ghost._renderer = go.AddComponent<SpriteRenderer>();
            ghost._renderer.sprite = source.sprite;
            ghost._renderer.sharedMaterial = source.sharedMaterial;
            ghost._renderer.sortingLayerID = source.sortingLayerID;
            ghost._renderer.sortingOrder = source.sortingOrder - 1;
            ghost._alpha = source.color.a * 0.5f;
            ghost._renderer.color = new Color(1f, 1f, 1f, ghost._alpha);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            float t = Mathf.Clamp01(_time / Lifetime);
            _renderer.color = new Color(1f, 1f, 1f, _alpha * (1f - t));
            transform.localScale *= 1f - Time.deltaTime * 1.5f;
            if (_time >= Lifetime)
                Destroy(gameObject);
        }
    }
}
