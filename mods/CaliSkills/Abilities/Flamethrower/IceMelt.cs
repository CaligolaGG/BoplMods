using System.Collections.Generic;
using BoplFixedMath;
using UnityEngine;

namespace CaliSkills.Abilities.Flamethrower;

/// <summary>
/// Ice platforms melted by the Flamethrower on ice maps (scene object, created on the first hit: reset every level).
/// Every tick a platform is in the flames it loses <see cref="FlamethrowerSettings.MeltSpeed"/> of its size per second
/// (its collider's Scale, like the Shrink ray), down to 1 − <see cref="FlamethrowerSettings.MaxMelt"/>;
/// <see cref="FlamethrowerSettings.RegrowDelay"/> after the last hit it grows back at
/// <see cref="FlamethrowerSettings.RegrowSpeed"/> until all it lost is back. Only what melting took is given back, so
/// other size changes (Growth / Shrink rays) stay. Sim code: platforms in the order they were first hit, Fix math;
/// the melting look (water drops falling, steam) is drawn in LateUpdate.
/// </summary>
internal sealed class IceMelt : MonoUpdatable
{
    internal sealed class Melting
    {
        public StickyRoundedRectangle Platform;
        public DPhysicsRoundedRect Collider;
        public Fix Removed;     // scale taken away by melting, still to give back
        public Fix SinceHit;
        public bool HitThisTick;
        public Fix At;          // last hit, 0..1 around the outline
        public float DripTimer;
        public float SteamTimer;
    }

    private static IceMelt _instance;
    private readonly List<Melting> _melting = new();

    internal static void Hit(StickyRoundedRectangle platform, Vec2 point)
    {
        if (platform == null || platform.IsDestroyed)
            return;
        DPhysicsRoundedRect collider = platform.GetComponent<DPhysicsRoundedRect>();
        if (collider == null)
            return;
        if (_instance == null)
            _instance = new GameObject("CaliSkills.IceMelt").AddComponent<IceMelt>();
        Melting melting = _instance._melting.Find(m => m.Platform == platform);
        if (melting == null)
        {
            melting = new Melting { Platform = platform, Collider = collider };
            _instance._melting.Add(melting);
        }
        melting.HitThisTick = true;
        melting.SinceHit = Fix.Zero;
        melting.At = platform.LocalPlayerPosFromPos(point, Fix.Zero);
    }

    private void Awake()
    {
        Updater.RegisterUpdatable(this);
    }

    public override void Init()
    {
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        Fix dt = simDeltaTime * GameTime.PlayerTimeScale;
        for (int i = _melting.Count - 1; i >= 0; i--)
        {
            Melting m = _melting[i];
            if (m.Platform == null || m.Platform.IsDestroyed || m.Collider == null || m.Collider.IsDestroyed)
            {
                _melting.RemoveAt(i);
                continue;
            }
            Fix scale = m.Collider.Scale;
            Fix original = scale + m.Removed;
            if (m.HitThisTick)
            {
                m.HitThisTick = false;
                Fix maxRemoved = original * Fix.Clamp((Fix)FlamethrowerSettings.MaxMelt, Fix.Zero, (Fix)0.95f);
                Fix step = Fix.Min(original * Fix.Max((Fix)FlamethrowerSettings.MeltSpeed, Fix.Zero) * dt, maxRemoved - m.Removed);
                if (step > Fix.Zero)
                    m.Removed += SetScale(m.Collider, scale - step);
                continue;
            }
            m.SinceHit += dt;
            if (m.SinceHit < (Fix)FlamethrowerSettings.RegrowDelay)
                continue;
            Fix grow = Fix.Min(original * Fix.Max((Fix)FlamethrowerSettings.RegrowSpeed, (Fix)0.01f) * dt, m.Removed);
            if (grow > Fix.Zero)
                m.Removed -= -SetScale(m.Collider, scale + grow);
            if (m.Removed <= Fix.Zero)
                _melting.RemoveAt(i);
        }
    }

    /// <summary>Sets the collider's scale (it clamps it); returns how much it went down.</summary>
    private static Fix SetScale(DPhysicsRoundedRect collider, Fix scale)
    {
        Fix before = collider.Scale;
        collider.Scale = scale;
        return before - collider.Scale;
    }

    // --- Visual: drops of water falling from where it melts, and steam ---

    private void LateUpdate()
    {
        foreach (Melting m in _melting)
        {
            StickyRoundedRectangle platform = m.Platform;
            if (platform == null || platform.IsDestroyed || (float)m.SinceHit > 0.1f)
                continue;
            SpriteRenderer platformRen = platform.GetSpriteRenderer();
            float circumference = Mathf.Max(0.01f, (float)platform.Circumference());
            // Everything scales with the platform: its half size (largest side's half, with the rounded edge).
            Vector2 extents = (Vector2)platform.rr.CalcExtents();
            float size = Mathf.Max(1f, Mathf.Max(extents.x, extents.y) + (float)platform.rr.radius);
            float melt = Mathf.Clamp01((float)(m.Removed / Fix.Max(m.Collider.Scale + m.Removed, (Fix)0.01f)) / 0.3f);
            // Wider and denser as it melts more.
            float spread = size * (0.5f + 0.4f * melt);

            m.DripTimer -= Time.deltaTime;
            while (m.DripTimer <= 0f)
            {
                m.DripTimer += 0.12f / size;
                Surface(platform, (float)m.At + Random.Range(-spread, spread) / circumference, out Vector2 point, out Vector2 outward);
                Vector2 velocity = outward * Random.Range(0.2f, 0.6f) * size + Vector2.down * Random.Range(0f, 0.3f) * size;
                FlameParticles.Emit(FlameParticles.Kind.Drip, point, velocity, Random.Range(0.6f, 1f),
                    Random.Range(0.12f, 0.22f) * size, platformRen);
            }
            m.SteamTimer -= Time.deltaTime;
            while (m.SteamTimer <= 0f)
            {
                m.SteamTimer += 0.25f / size;
                Surface(platform, (float)m.At + Random.Range(-spread, spread) * 0.6f / circumference, out Vector2 point, out Vector2 outward);
                Vector2 velocity = (outward * Random.Range(0.3f, 0.7f) + Vector2.up * Random.Range(0.5f, 1f)) * size;
                FlameParticles.Emit(FlameParticles.Kind.Steam, point, velocity, Random.Range(0.7f, 1.1f),
                    Random.Range(0.45f, 0.75f) * size, platformRen);
            }
        }
    }

    /// <summary>The point <paramref name="at"/> (0..1, wraps) on the platform's outline, and the way out of it there.</summary>
    internal static void Surface(StickyRoundedRectangle platform, float at, out Vector2 point, out Vector2 outward)
    {
        at -= Mathf.Floor(at);
        Fix local = (Fix)at;
        point = (Vector2)platform.PositionFromLocalPlayerPos(local, Fix.Zero);
        outward = ((Vector2)platform.PositionFromLocalPlayerPos(local, (Fix)0.5f) - point).normalized;
        if (outward == Vector2.zero)
            outward = Vector2.up;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
