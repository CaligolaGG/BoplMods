using System.Collections.Generic;
using CaliSkills.Abilities.NothingPersonal;
using UnityEngine;

namespace CaliSkills.Abilities.Chad;

/// <summary>
/// Chad mode's look (visual only, drawn from <see cref="ChadAbility"/>'s state): the slime sprite is hidden and the
/// Throw ability's muscly slime (MachoThrow2) is drawn in the player's material: idle, or walking while it moves on
/// a platform; its opening (enter: the jump up, until the impact; then grab: hands into the ground) for the slam; its throw (beginThrow + exit) for the charge, facing
/// the charge's side. Aura farm adds a pulsing golden glow and turning light rays. Smoke puffs when it transforms and where
/// the slam lands.
/// </summary>
public class ChadVisuals : MonoBehaviour
{
    private ChadAbility _ability;
    private GameObject _root;
    private SpriteRenderer _body;
    private SpriteRenderer _glow;
    private SpriteRenderer _rays;
    private bool _hidingSlime;
    private float _loopTime;
    private float _shineTime;
    private bool _loggedDraw;
    private readonly Dictionary<string, SpriteFrame[]> _frames = new();

    private void Awake()
    {
        _ability = GetComponent<ChadAbility>();
    }

    private void Build()
    {
        _root = new GameObject("CaliSkills.Chad");
        _body = NewRenderer("Chad", null);
        _glow = NewRenderer("Glow", ChadSkill.GlowSprite);
        _rays = NewRenderer("Rays", ChadSkill.RaysSprite);
    }

    private SpriteRenderer NewRenderer(string name, Sprite sprite)
    {
        SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(_root.transform, worldPositionStays: false);
        renderer.sprite = sprite;
        renderer.enabled = false;
        return renderer;
    }

    internal void OnTransform()
    {
        SlimeController slime = _ability != null ? _ability.Caster : null;
        if (slime != null && !slime.IsDestroyed)
            NothingPersonalVisuals.SpawnSmokeCloud(slime.body.position, 0.55f * (float)slime.body.fixtrans.Scale, 0.45f);
    }

    internal void OnSlamImpact()
    {
        SlimeController slime = _ability != null ? _ability.Caster : null;
        if (slime == null || slime.IsDestroyed)
            return;
        Vec2 feet = slime.body.position - Vec2.NormalizedSafe(slime.body.up) * slime.playerPhysics.radius;
        NothingPersonalVisuals.SpawnSmokeCloud(feet, 0.7f * (float)slime.body.fixtrans.Scale, 0.4f);
    }

    private void LateUpdate()
    {
        SlimeController slime = _ability.Caster;
        bool alive = slime != null && !slime.IsDestroyed && slime.isActiveAndEnabled && slime.spriteRen != null;
        bool chad = alive && _ability.InMode && ChadSkill.ThrowerAnimations != null;
        bool shining = alive && _ability.IsShining;
        if (!chad && !shining)
        {
            Hide();
            return;
        }
        if (_root == null)
            Build();

        SpriteRenderer slimeRen = slime.spriteRen;
        float alpha = slimeRen.color.a;
        float scale = (float)_ability.Scale;
        Vector2 center = (Vector2)slime.body.position;

        if (chad)
        {
            slimeRen.enabled = false;
            _hidingSlime = true;
            DrawChad(slime, slimeRen, scale, alpha);
        }
        else
        {
            RestoreSlime();
            _body.enabled = false;
        }

        DrawHitboxes(slimeRen);

        if (shining)
        {
            _shineTime += Time.deltaTime;
            // Around what's drawn: the big chad in Chad mode (its sprite's bounds), else the slime.
            float size = (float)slime.playerPhysics.radius * 2f;
            Vector2 middle = center;
            if (chad && _body.enabled && _body.sprite != null)
            {
                Bounds bounds = _body.bounds;
                size = Mathf.Max(bounds.size.x, bounds.size.y);
                middle = bounds.center;
            }
            float pulse = 1f + 0.12f * Mathf.Sin(_shineTime * 18f);
            _glow.transform.position = middle;
            _glow.transform.localScale = Vector3.one * (size * 2.5f * pulse);
            _glow.color = new Color(1f, 1f, 1f, 0.85f * alpha);
            _rays.transform.position = middle;
            _rays.transform.rotation = Quaternion.Euler(0f, 0f, _shineTime * 120f);
            _rays.transform.localScale = Vector3.one * (size * 3.5f * pulse);
            _rays.color = new Color(1f, 1f, 1f, 0.9f * alpha);
            _glow.sortingLayerID = _rays.sortingLayerID = slimeRen.sortingLayerID;
            _glow.sortingOrder = slimeRen.sortingOrder - 2;
            _rays.sortingOrder = slimeRen.sortingOrder - 1;
            _glow.enabled = _rays.enabled = true;
        }
        else
        {
            _shineTime = 0f;
            _glow.enabled = _rays.enabled = false;
        }
    }

    private void DrawChad(SlimeController slime, SpriteRenderer slimeRen, float scale, float alpha)
    {
        bool grounded = slime.playerPhysics.IsGrounded();
        bool facingRight = slime.isFacingRight();
        Sprite sprite;
        switch (_ability.CurrentMove)
        {
            case ChadAbility.Move.Slam:
                // The Throw ability's opening: the jump up (enter) until the impact, then the hands plunging into the
                // ground (grab).
                sprite = _ability.SlamLanded
                    ? Frame(new[] { "grab" }, _ability.SlamPhase01)
                    : Frame(new[] { "enter" }, _ability.SlamPhase01);
                break;
            case ChadAbility.Move.Punch:
                // The throw: wind-up then release.
                sprite = Frame(new[] { "beginThrow", "exit" }, _ability.MoveProgress01);
                facingRight = _ability.PunchRight;
                break;
            default:
                _loopTime += Time.deltaTime;
                bool walking = grounded && Mathf.Abs((float)slime.playerPhysics.groundedSpeed) > 1f;
                sprite = LoopFrame(walking ? "walk" : "idle", _loopTime);
                break;
        }
        if (!_loggedDraw)
        {
            _loggedDraw = true;
            Plugin.Log.LogInfo($"Chad mode: drawing {(sprite != null ? sprite.name : "nothing (no frame)")} at scale {scale * ChadSkill.ThrowerScale}.");
        }
        _body.sprite = sprite != null ? sprite : slimeRen.sprite;
        _body.sharedMaterial = slimeRen.sharedMaterial;
        _body.color = new Color(1f, 1f, 1f, alpha);
        _body.sortingLayerID = slimeRen.sortingLayerID;
        _body.sortingOrder = slimeRen.sortingOrder;
        Transform t = _body.transform;
        t.position = (Vector2)slime.body.position;
        t.rotation = grounded ? slime.transform.rotation : Quaternion.identity;
        // The muscly slime's sprites face right (MachoThrow2 mirrors its form when facing left).
        float s = scale * ChadSkill.ThrowerScale;
        t.localScale = new Vector3(facingRight ? s : -s, s, 1f);
        _body.enabled = true;
    }

    private SpriteFrame[] Frames(string name)
    {
        if (_frames.TryGetValue(name, out SpriteFrame[] frames))
            return frames;
        try
        {
            frames = ChadSkill.ThrowerAnimations.GetAnimation(name);
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"Chad mode: animation {name} not found ({e.Message}).");
            frames = null;
        }
        _frames[name] = frames;
        return frames;
    }

    /// <summary>The frame at <paramref name="progress01"/> through these animations played one after the other (by frame durations).</summary>
    private Sprite Frame(string[] names, float progress01)
    {
        var all = new List<SpriteFrame>();
        foreach (string name in names)
        {
            SpriteFrame[] frames = Frames(name);
            if (frames != null)
                all.AddRange(frames);
        }
        if (all.Count == 0)
            return null;
        int total = 0;
        foreach (SpriteFrame frame in all)
            total += Mathf.Max(1, Mathf.Abs(frame.frameDuration));
        int tick = Mathf.Min(total - 1, Mathf.FloorToInt(Mathf.Clamp01(progress01) * total));
        foreach (SpriteFrame frame in all)
        {
            tick -= Mathf.Max(1, Mathf.Abs(frame.frameDuration));
            if (tick < 0)
                return frame.sprite;
        }
        return all[all.Count - 1].sprite;
    }

    /// <summary>A looping animation's frame at <paramref name="seconds"/> (frame durations are 60-a-second ticks).</summary>
    private Sprite LoopFrame(string name, float seconds)
    {
        SpriteFrame[] frames = Frames(name);
        if (frames == null || frames.Length == 0)
            return null;
        int total = 0;
        foreach (SpriteFrame frame in frames)
            total += Mathf.Max(1, Mathf.Abs(frame.frameDuration));
        return Frame(new[] { name }, (Mathf.FloorToInt(seconds * 60f) % total + 0.5f) / total);
    }

    private void RestoreSlime()
    {
        if (!_hidingSlime)
            return;
        _hidingSlime = false;
        SlimeController slime = _ability.Caster;
        if (slime != null && !slime.IsDestroyed && slime.spriteRen != null)
            slime.spriteRen.enabled = true;
    }

    /// <summary>Show hitboxes: the slam's squash rectangle, the charge's stun rectangle, the aura's shining area.</summary>
    private void DrawHitboxes(SpriteRenderer slimeRen)
    {
        if (!Core.Hitboxes.Enabled)
            return;
        if (_ability.SlamHitbox(out Vec2 slamCenter, out Vec2 slamUp, out BoplFixedMath.Fix halfWidth, out BoplFixedMath.Fix halfHeight))
            Core.Hitboxes.Box((Vector2)slamCenter, (Vector2)slamUp, (float)halfWidth * 2f, (float)halfHeight * 2f, slimeRen);
        if (_ability.ChargeHitbox(out Vec2 chargeCenter, out Vec2 chargeUp, out BoplFixedMath.Fix halfLength, out BoplFixedMath.Fix chargeHalfHeight))
            Core.Hitboxes.Box((Vector2)chargeCenter, (Vector2)chargeUp, (float)halfLength * 2f, (float)chargeHalfHeight * 2f, slimeRen);
        if (_ability.IsShining)
            Core.Hitboxes.Circle((Vector2)_ability.AuraCenter, (float)_ability.AuraReach, slimeRen);
    }

    private void Hide()
    {
        _loopTime = 0f;
        _shineTime = 0f;
        RestoreSlime();
        if (_root != null)
            _body.enabled = _glow.enabled = _rays.enabled = false;
    }

    private void OnDestroy()
    {
        if (_root != null)
            Destroy(_root);
    }
}
