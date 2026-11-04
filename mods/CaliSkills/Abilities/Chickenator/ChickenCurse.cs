using System.Collections.Generic;
using BoplFixedMath;
using CaliSkills.Abilities.NothingPersonal;
using HarmonyLib;
using UnityEngine;

namespace CaliSkills.Abilities.Chickenator;

/// <summary>
/// Players turned into chickens by a Chickenator potion (scene object, created on the first one: reset every level).
/// A chicken (for <see cref="ChickenatorSettings.ChickenDuration"/>, restarted by another potion):
/// - can't use abilities (<see cref="NoAbilities"/>; their icons dim like any uncastable ability);
/// - jumps <see cref="ChickenatorSettings.JumpHeight"/> as high, is <see cref="ChickenatorSettings.Weight"/> as heavy
///   (gravity × weight, pushes ÷ weight) and <see cref="ChickenatorSettings.Speed"/> as fast: its PlayerPhysics values
///   are multiplied while it's a chicken and divided back after (so other temporary changes, e.g. Run, stay right);
///   the jump takes the extra gravity into account (jump speed × √(height × weight));
/// - looks like a chicken (slime sprite hidden, a chicken with a band in the player's color), with a puff of smoke
///   when it turns and turns back.
/// Sim code: Fix math, players in id order; visuals in LateUpdate.
/// </summary>
internal sealed class ChickenCurse : MonoUpdatable
{
    private sealed class Chicken
    {
        public SlimeController Slime;
        public Fix Left;
        public Fix Jump, Gravity, Push, Speed;
        public SpriteRenderer Body, Band;
        public float Bob;
    }

    private static ChickenCurse _instance;
    private readonly SortedDictionary<int, Chicken> _chickens = new();

    /// <summary>Turns <paramref name="slime"/> into a chicken for <paramref name="duration"/> (or restarts its time).</summary>
    internal static void Apply(SlimeController slime, Fix duration)
    {
        if (duration <= Fix.Zero || Chad.ChadPatches.BlocksCurse(slime))
            return;
        if (_instance == null)
            _instance = new GameObject("CaliSkills.ChickenCurse").AddComponent<ChickenCurse>();
        _instance.Add(slime, duration);
    }

    internal static bool IsChicken(SlimeController slime) =>
        _instance != null && _instance._chickens.TryGetValue(slime.GetPlayerId(), out Chicken c) && c.Slime == slime;

    private void Awake()
    {
        Updater.RegisterUpdatable(this);
    }

    public override void Init()
    {
    }

    private void Add(SlimeController slime, Fix duration)
    {
        int id = slime.GetPlayerId();
        if (_chickens.TryGetValue(id, out Chicken existing) && existing.Slime == slime)
        {
            existing.Left = Fix.Max(existing.Left, duration);
            return;
        }
        if (existing != null)
            Remove(id, restore: true);

        Fix weight = Fix.Max((Fix)ChickenatorSettings.Weight, (Fix)0.1f);
        var chicken = new Chicken
        {
            Slime = slime,
            Left = duration,
            Jump = Fix.Sqrt(Fix.Max((Fix)ChickenatorSettings.JumpHeight, Fix.Zero) * weight),
            Gravity = weight,
            Push = Fix.One / weight,
            Speed = Fix.Max((Fix)ChickenatorSettings.Speed, (Fix)0.01f),
        };
        Scale(slime.playerPhysics, chicken, apply: true);
        _chickens[id] = chicken;
        // A slime hidden behind an ability form has a stale position: the burst's own puff shows there.
        if (slime.isActiveAndEnabled)
            Puff(slime);
        AudioManager.Get()?.Play("transform1");
    }

    /// <summary>Multiplies the slime's movement values by the chicken's factors, or divides them back.</summary>
    private static void Scale(PlayerPhysics physics, Chicken c, bool apply)
    {
        if (physics == null)
            return;
        if (apply)
        {
            physics.jumpStrength *= c.Jump;
            physics.gravity_modifier *= c.Gravity;
            physics.inverseMass01 *= c.Push;
            physics.maxSpeed *= c.Speed;
            physics.accel *= c.Speed;
            physics.airAccel *= c.Speed;
        }
        else
        {
            if (c.Jump > Fix.Zero)
                physics.jumpStrength /= c.Jump;
            physics.gravity_modifier /= c.Gravity;
            physics.inverseMass01 /= c.Push;
            physics.maxSpeed /= c.Speed;
            physics.accel /= c.Speed;
            physics.airAccel /= c.Speed;
        }
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        var ids = new List<int>(_chickens.Keys);
        foreach (int id in ids)
        {
            Chicken c = _chickens[id];
            SlimeController slime = c.Slime;
            Player player = PlayerHandler.Get().GetPlayer(id);
            if (slime == null || slime.IsDestroyed || player == null || !player.IsAlive)
            {
                Remove(id, restore: slime != null && !slime.IsDestroyed);
                continue;
            }
            // Hit while in an ability form (inactive slime): the time starts once it's a slime again.
            if (!slime.isActiveAndEnabled)
                continue;
            c.Left -= GameTime.FixedDeltaTime(id, simDeltaTime);
            if (c.Left <= Fix.Zero)
            {
                Remove(id, restore: true);
                Puff(slime);
                AudioManager.Get()?.Play("transform2");
            }
        }
    }

    private void Remove(int id, bool restore)
    {
        if (!_chickens.TryGetValue(id, out Chicken c))
            return;
        _chickens.Remove(id);
        if (restore && c.Slime != null && !c.Slime.IsDestroyed)
        {
            Scale(c.Slime.playerPhysics, c, apply: false);
            if (c.Slime.spriteRen != null)
                c.Slime.spriteRen.enabled = true;
        }
        if (c.Body != null)
            Destroy(c.Body.gameObject);
    }

    private static void Puff(SlimeController slime) =>
        NothingPersonalVisuals.SpawnSmokeCloud(slime.body.position, 0.5f * (float)slime.body.fixtrans.Scale, 0.5f);

    // --- Visual ---

    private void LateUpdate()
    {
        foreach (Chicken c in _chickens.Values)
        {
            SlimeController slime = c.Slime;
            if (slime == null || slime.IsDestroyed)
                continue;
            SpriteRenderer slimeRen = slime.spriteRen;
            bool visible = slime.isActiveAndEnabled && slimeRen != null;
            if (c.Body == null)
            {
                c.Body = new GameObject("Chicken").AddComponent<SpriteRenderer>();
                c.Body.sprite = ChickenatorSkill.ChickenSprite;
                c.Band = new GameObject("Band").AddComponent<SpriteRenderer>();
                c.Band.sprite = ChickenatorSkill.BandSprite;
                c.Band.transform.SetParent(c.Body.transform, worldPositionStays: false);
                c.Band.color = PlayerColor(slime);
            }
            c.Body.enabled = c.Band.enabled = visible;
            if (!visible)
                continue;

            slimeRen.enabled = false;
            float size = Mathf.Max(0.1f, ChickenatorSettings.ChickenSize) * (float)slime.playerPhysics.radius;
            // A little hop while it runs.
            bool moving = Mathf.Abs((float)slime.playerPhysics.groundedSpeed) > 1f && slime.playerPhysics.IsGrounded();
            c.Bob += Time.deltaTime * (moving ? 14f : 0f);
            float bob = moving ? Mathf.Abs(Mathf.Sin(c.Bob)) * 0.08f : 0f;

            Transform t = c.Body.transform;
            // Upright on the slime's own up (a slime on a wall or ceiling is rotated with it).
            Vector2 up = ((Vector2)slime.body.up).normalized;
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg - 90f);
            // Feet (0.43 of the sprite below its middle) where the slime's bottom is, however big the chicken.
            float lift = 0.43f * size - (float)slime.playerPhysics.radius;
            t.position = (Vector2)slime.body.position + (Vector2)(t.up * (lift + size * bob));
            t.localScale = new Vector3(size, size * (1f - bob * 0.5f), 1f);
            c.Body.flipX = c.Band.flipX = !slime.isFacingRight();
            c.Body.sortingLayerID = c.Band.sortingLayerID = slimeRen.sortingLayerID;
            c.Body.sortingOrder = slimeRen.sortingOrder;
            c.Band.sortingOrder = slimeRen.sortingOrder + 1;
            Color alpha = new Color(1f, 1f, 1f, slimeRen.color.a);
            c.Body.color = alpha;
        }
    }

    /// <summary>The player's color (the player material's _ShadowColor, what the game uses for it), else white.</summary>
    private static Color PlayerColor(SlimeController slime)
    {
        Material material = PlayerHandler.Get().GetPlayer(slime.GetPlayerId())?.Color;
        if (material != null && material.HasProperty("_ShadowColor"))
        {
            Color c = material.GetColor("_ShadowColor");
            c.a = 1f;
            return c;
        }
        return Color.white;
    }

    private void OnDestroy()
    {
        foreach (Chicken c in _chickens.Values)
        {
            if (c.Body != null)
                Destroy(c.Body.gameObject);
        }
        if (_instance == this)
            _instance = null;
    }

    /// <summary>Chickens can't use abilities.</summary>
    [HarmonyPatch(typeof(SlimeController), nameof(SlimeController.isAbilityCastable))]
    private static class NoAbilities
    {
        private static void Postfix(SlimeController __instance, ref bool __result)
        {
            if (__result && IsChicken(__instance))
                __result = false;
        }
    }
}
