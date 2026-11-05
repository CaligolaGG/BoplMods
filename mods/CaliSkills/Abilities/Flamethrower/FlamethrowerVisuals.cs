using UnityEngine;

namespace CaliSkills.Abilities.Flamethrower;

/// <summary>
/// The Flamethrower's look (visual only, drawn from <see cref="FlamethrowerAbility"/>'s state): while it fires, the
/// user's flamethrower sprite (Assets/flamethrower.png) is held in front of the slime, pointed where the flames go
/// (flipped when pointing left, so it's never upside down), and flames pour out of its muzzle: particles spread over
/// the cone that stop where a platform stopped the cone's rays (<see cref="FlamethrowerAbility.RayReach"/>). The
/// user's flamethrower sound (Assets/flamethrower_sound.wav) loops meanwhile. Also draws its Show hitboxes cone.
/// </summary>
public class FlamethrowerVisuals : MonoBehaviour
{
    /// <summary>Gun length, in slime radiuses.</summary>
    private const float GunSize = 2.6f;

    /// <summary>Where the grip is held: ahead of the slime's center, in slime radiuses.</summary>
    private const float GripAhead = 0.35f;

    /// <summary>Flames per second.</summary>
    private const float FlameRate = 110f;

    private FlamethrowerAbility _ability;
    private GameObject _root;
    private SpriteRenderer _gun;
    private AudioSource _sound;
    private float _emit;

    private void Awake() => _ability = GetComponent<FlamethrowerAbility>();

    private void Build()
    {
        _root = new GameObject("CaliSkills.Flamethrower");
        _gun = new GameObject("Gun").AddComponent<SpriteRenderer>();
        _gun.transform.SetParent(_root.transform, worldPositionStays: false);
        _gun.sprite = FlamethrowerSkill.GunSprite;
        _sound = _root.AddComponent<AudioSource>();
        _sound.clip = FlamethrowerSkill.Sound;
        _sound.loop = true;
        _sound.playOnAwake = false;
    }

    internal void OnStart()
    {
        _emit = 0f;
    }

    private void LateUpdate()
    {
        SlimeController slime = _ability.Caster;
        bool firing = _ability.Firing && slime != null && !slime.IsDestroyed && slime.isActiveAndEnabled && slime.spriteRen != null;
        if (!firing)
        {
            Hide();
            return;
        }
        if (_root == null)
            Build();

        SpriteRenderer slimeRen = slime.spriteRen;
        float radius = (float)slime.playerPhysics.radius;
        Vector2 center = (Vector2)slime.body.position;
        Vector2 dir = ((Vector2)_ability.Direction).normalized;
        if (dir == Vector2.zero)
            dir = Vector2.right;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        bool left = dir.x < 0f;

        // The gun: its grip ahead of the slime, the barrel along the flames.
        float size = GunSize * radius;
        Vector2 grip = center + dir * (GripAhead * radius);
        Transform t = _gun.transform;
        t.SetPositionAndRotation(grip, Quaternion.Euler(0f, 0f, angle));
        t.localScale = new Vector3(size, left ? -size : size, 1f);
        _gun.sortingLayerID = slimeRen.sortingLayerID;
        _gun.sortingOrder = slimeRen.sortingOrder + 1;
        _gun.color = new Color(1f, 1f, 1f, slimeRen.color.a);
        _gun.enabled = true;
        Vector2 muzzleLocal = FlamethrowerSkill.MuzzleFromGrip * size;
        if (left)
            muzzleLocal.y = -muzzleLocal.y;
        Vector2 muzzle = grip + (Vector2)(Quaternion.Euler(0f, 0f, angle) * muzzleLocal);

        EmitFlames(center, muzzle, dir, slimeRen);
        DrawHitbox(slimeRen);
        UpdateSound(!GameSessionHandler.GameIsPaused);
    }

    private void EmitFlames(Vector2 center, Vector2 muzzle, Vector2 dir, SpriteRenderer slimeRen)
    {
        if (GameSessionHandler.GameIsPaused)
            return;
        float length = (float)_ability.Length;
        float half = (float)_ability.HalfAngle * Mathf.Rad2Deg;
        float muzzleAlong = Vector2.Dot(muzzle - center, dir);
        const float life = 0.42f;
        _emit += Time.deltaTime * FlameRate;
        while (_emit >= 1f)
        {
            _emit -= 1f;
            // Mostly in the middle of the cone.
            float offset = (Random.value + Random.value - 1f) * half;
            Vector2 way = Quaternion.Euler(0f, 0f, offset) * dir;
            float reach = Reach(offset / Mathf.Max(half, 0.01f)) - Mathf.Max(0f, muzzleAlong);
            float travel = Mathf.Max(0f, length - Mathf.Max(0f, muzzleAlong));
            float speed = travel / (life * 0.8f) * Random.Range(0.85f, 1.1f);
            Vector2 start = muzzle + way * Random.Range(0f, 0.3f);
            FlameParticles.Emit(FlameParticles.Kind.Flame, start, way * speed, life * Random.Range(0.85f, 1.1f),
                length * Random.Range(0.13f, 0.19f), slimeRen, Mathf.Max(0f, reach));
        }
    }

    /// <summary>How far the cone reaches at <paramref name="side"/> -1..1 across it (between the rays).</summary>
    private float Reach(float side)
    {
        int last = FlamethrowerAbility.RayCount - 1;
        float at = (Mathf.Clamp(side, -1f, 1f) + 1f) / 2f * last;
        int i = Mathf.Min(last - 1, Mathf.FloorToInt(at));
        float a = (float)_ability.RayReach[i], b = (float)_ability.RayReach[i + 1];
        // A platform hit by either ray stops the flames between them at the nearer one.
        return Mathf.Min(Mathf.Lerp(a, b, at - i), Mathf.Min(a, b) + 0.5f);
    }

    private void DrawHitbox(SpriteRenderer slimeRen)
    {
        if (!Core.Hitboxes.Enabled)
            return;
        Core.Hitboxes.Sector((Vector2)_ability.Origin, (Vector2)_ability.Direction, (float)_ability.Length,
            (float)_ability.HalfAngle * 2f * Mathf.Rad2Deg, slimeRen);
    }

    /// <summary>The flamethrower sound, at the game's sound effects volume, looping while it fires.</summary>
    private void UpdateSound(bool on)
    {
        if (_sound == null || _sound.clip == null)
            return;
        AudioManager audio = AudioManager.Get();
        _sound.volume = audio != null ? audio.currentMasterVolume * audio.sfxVolume : 1f;
        if (on && !_sound.isPlaying)
            _sound.Play();
        else if (!on && _sound.isPlaying)
            _sound.Stop();
    }

    private void Hide()
    {
        UpdateSound(on: false);
        if (_root != null)
            _gun.enabled = false;
    }

    private void OnDestroy()
    {
        if (_root != null)
            Destroy(_root);
    }
}
