using System.Collections.Generic;
using BoplFixedMath;
using UnityEngine;

namespace CaliSkills.Abilities.Flamethrower;

/// <summary>
/// Platforms set on fire by the Flamethrower (scene object, created on the first fire: reset every level). A fire is
/// a stretch of a platform's outline: it starts where the flames hit, spreads both ways along the outline at
/// <see cref="FlamethrowerSettings.FireSpreadSpeed"/> up to <see cref="FlamethrowerSettings.FireMaxSpread"/>, and dies
/// <see cref="FlamethrowerSettings.FireDuration"/> after the flames last hit it (hits inside a fire keep it going;
/// hits elsewhere on the platform start another one). Positions on the outline are the game's "local player position"
/// (0..1 around the platform, <see cref="StickyRoundedRectangle.LocalPlayerPosFromPos"/>), so fires move and turn
/// with their platform. Players touching a burning stretch die (<see cref="FlamethrowerSettings.FireKills"/>).
/// Sim code: platforms and players in a fixed order, Fix math; flames drawn in LateUpdate.
/// </summary>
internal sealed class PlatformFire : MonoUpdatable
{
    private const int MaxFiresPerPlatform = 12;

    /// <summary>How far above the surface the fire burns players (slime radiuses of 1 = scale 1).</summary>
    private static readonly Fix Height = (Fix)0.35f;

    internal sealed class Fire
    {
        public Fix Center;   // 0..1 around the outline
        public Fix Spread;   // units each way
        public Fix Left;     // seconds
        public Fix Age;
        public int Owner;
        public int Seed;
    }

    internal sealed class Burning
    {
        public StickyRoundedRectangle Platform;
        public readonly List<Fire> Fires = new();
        public readonly List<SpriteRenderer> Flames = new();
    }

    private static PlatformFire _instance;
    private readonly List<Burning> _burning = new();
    private int _seeds;

    internal static void Ignite(StickyRoundedRectangle platform, Vec2 point, int owner)
    {
        if (platform == null || platform.IsDestroyed)
            return;
        if (_instance == null)
            _instance = new GameObject("CaliSkills.PlatformFire").AddComponent<PlatformFire>();
        _instance.Add(platform, point, owner);
    }

    private void Awake()
    {
        Updater.RegisterUpdatable(this);
    }

    public override void Init()
    {
    }

    private void Add(StickyRoundedRectangle platform, Vec2 point, int owner)
    {
        Burning burning = _burning.Find(b => b.Platform == platform);
        if (burning == null)
        {
            burning = new Burning { Platform = platform };
            _burning.Add(burning);
        }
        Fix duration = Fix.Max((Fix)FlamethrowerSettings.FireDuration, Fix.Zero);
        Fix at = platform.LocalPlayerPosFromPos(point, Fix.Zero);
        Fix circumference = Fix.Max(platform.Circumference(), (Fix)0.01f);

        // Inside a fire (or right at its edge): keep it going.
        Fire closest = null;
        Fix closestGap = Fix.MaxValue;
        foreach (Fire fire in burning.Fires)
        {
            Fix gap = Gap(fire.Center, at) * circumference - fire.Spread;
            if (gap < closestGap)
            {
                closestGap = gap;
                closest = fire;
            }
        }
        if (closest != null && (closestGap <= (Fix)0.5f || burning.Fires.Count >= MaxFiresPerPlatform))
        {
            closest.Left = duration;
            closest.Owner = owner;
            return;
        }
        burning.Fires.Add(new Fire { Center = at, Left = duration, Owner = owner, Seed = _seeds++ });
    }

    /// <summary>Distance between two outline positions (0..0.5 of the way around).</summary>
    private static Fix Gap(Fix a, Fix b)
    {
        Fix d = Fix.Abs(a - b);
        d -= Fix.Floor(d);
        return Fix.Min(d, Fix.One - d);
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        Fix dt = simDeltaTime * GameTime.PlayerTimeScale;
        Fix spreadSpeed = Fix.Max((Fix)FlamethrowerSettings.FireSpreadSpeed, Fix.Zero);
        Fix maxSpread = Fix.Max((Fix)FlamethrowerSettings.FireMaxSpread, Fix.Zero);
        for (int i = _burning.Count - 1; i >= 0; i--)
        {
            Burning burning = _burning[i];
            if (burning.Platform == null || burning.Platform.IsDestroyed || !burning.Platform.gameObject.activeInHierarchy)
            {
                Remove(i);
                continue;
            }
            for (int f = burning.Fires.Count - 1; f >= 0; f--)
            {
                Fire fire = burning.Fires[f];
                fire.Left -= dt;
                fire.Age += dt;
                if (fire.Left <= Fix.Zero)
                    burning.Fires.RemoveAt(f);
                else
                    fire.Spread = Fix.Min(maxSpread, fire.Spread + spreadSpeed * dt);
            }
            if (burning.Fires.Count == 0)
                Remove(i);
        }
        if (FlamethrowerSettings.FireKills)
            BurnPlayers();
    }

    /// <summary>Every player whose body touches a burning stretch dies (the owner only with FireHurtsOwner).</summary>
    private void BurnPlayers()
    {
        if (_burning.Count == 0)
            return;
        foreach (PlayerCollision target in FlamethrowerAbility.Players(-1))
        {
            Fix radius = target.physics != null ? target.physics.radius : Fix.Zero;
            Vec2 position = target.body.position;
            foreach (Burning burning in _burning)
            {
                if (!Touches(burning, target.GetPlayerId(), position, radius, out int owner))
                    continue;
                target.killPlayer(owner, spawnEffect: true, ignoreInvulnerability: false, CauseOfDeath.Other);
                break;
            }
        }
    }

    private static bool Touches(Burning burning, int playerId, Vec2 position, Fix radius, out int owner)
    {
        owner = -1;
        StickyRoundedRectangle platform = burning.Platform;
        Vec2 closest = platform.GetClosestPoint(position);
        Fix reach = radius + Height * Fix.Max(radius, (Fix)0.5f);
        if (Vec2.SqrMagnitude(position - closest) > reach * reach)
            return false;
        Fix at = platform.LocalPlayerPosFromPos(position, Fix.Zero);
        Fix circumference = Fix.Max(platform.Circumference(), (Fix)0.01f);
        foreach (Fire fire in burning.Fires)
        {
            if (fire.Owner == playerId && !FlamethrowerSettings.FireHurtsOwner)
                continue;
            // The body's own width counts: touching the end of the stretch is enough.
            if (Gap(fire.Center, at) * circumference <= fire.Spread + radius * (Fix)0.7f)
            {
                owner = fire.Owner;
                return true;
            }
        }
        return false;
    }

    private void Remove(int index)
    {
        foreach (SpriteRenderer flame in _burning[index].Flames)
        {
            if (flame != null)
                Destroy(flame.gameObject);
        }
        _burning.RemoveAt(index);
    }

    // --- Visual: flames standing on the burning stretches ---

    private void LateUpdate()
    {
        foreach (Burning burning in _burning)
            DrawFlames(burning);
    }

    private static void DrawFlames(Burning burning)
    {
        StickyRoundedRectangle platform = burning.Platform;
        if (platform == null || platform.IsDestroyed)
            return;
        SpriteRenderer platformRen = platform.GetSpriteRenderer();
        float circumference = Mathf.Max(0.01f, (float)platform.Circumference());
        float spacing = 0.55f;
        int used = 0;
        foreach (Fire fire in burning.Fires)
        {
            float spread = (float)fire.Spread;
            float left = (float)fire.Left;
            float age = (float)fire.Age;
            // Fading in at the start and out in the last half second.
            float strength = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01(left / 0.5f);
            int count = Mathf.Max(1, Mathf.CeilToInt(spread * 2f / spacing) + 1);
            for (int k = 0; k < count; k++)
            {
                float offset = count == 1 ? 0f : -spread + spread * 2f * k / (count - 1);
                float at = (float)fire.Center + offset / circumference;
                at -= Mathf.Floor(at);
                Fix local = (Fix)at;
                Vector2 surface = (Vector2)platform.PositionFromLocalPlayerPos(local, Fix.Zero);
                Vector2 outward = ((Vector2)platform.PositionFromLocalPlayerPos(local, (Fix)0.5f) - surface).normalized;
                if (outward == Vector2.zero)
                    outward = Vector2.up;
                // Flames lean up a bit (fire rises), smaller at the edges of the stretch.
                Vector2 up = (outward + Vector2.up * 0.6f).normalized;
                float edge = spread <= 0.01f ? 1f : 1f - 0.5f * Mathf.Abs(offset) / spread;
                float seed = fire.Seed * 7.31f + k * 1.37f;
                float flicker = 0.75f + 0.35f * Mathf.PerlinNoise(seed, Time.time * 6f);
                float size = 1.1f * edge * flicker * strength;

                if (used >= burning.Flames.Count)
                {
                    var renderer = new GameObject("CaliSkills.PlatformFlame").AddComponent<SpriteRenderer>();
                    renderer.sprite = FlamethrowerSkill.FlameSprite;
                    burning.Flames.Add(renderer);
                }
                SpriteRenderer flame = burning.Flames[used++];
                flame.enabled = size > 0.02f;
                float sway = (Mathf.PerlinNoise(seed + 5f, Time.time * 3f) - 0.5f) * 20f;
                flame.transform.SetPositionAndRotation(surface - outward * 0.1f,
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f + sway));
                flame.transform.localScale = new Vector3(size * 0.8f, size * (0.9f + 0.4f * flicker), 1f);
                flame.color = new Color(1f, 1f, 1f, Mathf.Clamp01(strength * 1.2f));
                if (platformRen != null)
                {
                    flame.sortingLayerID = platformRen.sortingLayerID;
                    flame.sortingOrder = platformRen.sortingOrder + 5;
                }
            }
        }
        for (int i = used; i < burning.Flames.Count; i++)
            burning.Flames[i].enabled = false;
    }

    private void OnDestroy()
    {
        for (int i = _burning.Count - 1; i >= 0; i--)
            Remove(i);
        if (_instance == this)
            _instance = null;
    }
}
