using System.Collections.Generic;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;

namespace CaliSkills.Abilities.Chad;

/// <summary>
/// Slimes stunned by Chad mode's charge (scene object, created on the first stun: reset every level). A stunned slime
/// can't move, jump or use abilities for <see cref="ChadSettings.StunDuration"/> (another stun restarts it): its input
/// is suppressed only while the game reads it (<see cref="SlimeController.OldUpdate"/>), so its momentum carries on
/// as usual (friction on a platform, drag in the air) and other effects that toggle <c>suppressInput</c> aren't
/// disturbed. Shining (Aura farm) slimes can't be stunned. Sim code: players in id order; stars in LateUpdate.
/// </summary>
internal sealed class ChadStun : MonoUpdatable
{
    private const int StarCount = 3;

    private sealed class Stun
    {
        public SlimeController Slime;
        public Fix Left;
        public SpriteRenderer[] Stars;
        public float Spin;
    }

    private static ChadStun _instance;
    private readonly SortedDictionary<int, Stun> _stuns = new();

    /// <summary>Stuns <paramref name="slime"/>; false if it can't be (shining).</summary>
    internal static bool Apply(SlimeController slime, Fix duration)
    {
        if (slime == null || duration <= Fix.Zero || ChadAbility.Shining.Contains(slime))
            return false;
        if (_instance == null)
            _instance = new GameObject("CaliSkills.ChadStun").AddComponent<ChadStun>();
        int id = slime.GetPlayerId();
        if (_instance._stuns.TryGetValue(id, out Stun stun) && stun.Slime == slime)
        {
            stun.Left = Fix.Max(stun.Left, duration);
            return true;
        }
        if (stun != null)
            _instance.Remove(id);
        _instance._stuns[id] = new Stun { Slime = slime, Left = duration };
        return true;
    }

    internal static bool IsStunned(SlimeController slime) =>
        _instance != null && slime != null && _instance._stuns.TryGetValue(slime.GetPlayerId(), out Stun s) && s.Slime == slime;

    private void Awake()
    {
        Updater.RegisterUpdatable(this);
    }

    public override void Init()
    {
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        var ids = new List<int>(_stuns.Keys);
        foreach (int id in ids)
        {
            Stun stun = _stuns[id];
            Player player = PlayerHandler.Get().GetPlayer(id);
            if (stun.Slime == null || stun.Slime.IsDestroyed || player == null || !player.IsAlive ||
                ChadAbility.Shining.Contains(stun.Slime))
            {
                Remove(id);
                continue;
            }
            stun.Left -= GameTime.FixedDeltaTime(id, simDeltaTime);
            if (stun.Left <= Fix.Zero)
                Remove(id);
        }
    }

    private void Remove(int id)
    {
        if (!_stuns.TryGetValue(id, out Stun stun))
            return;
        _stuns.Remove(id);
        if (stun.Stars == null)
            return;
        foreach (SpriteRenderer star in stun.Stars)
        {
            if (star != null)
                Destroy(star.gameObject);
        }
    }

    // --- Visual: stars circling over the head ---

    private void LateUpdate()
    {
        foreach (Stun stun in _stuns.Values)
        {
            SlimeController slime = stun.Slime;
            if (slime == null || slime.IsDestroyed)
                continue;
            if (stun.Stars == null)
            {
                stun.Stars = new SpriteRenderer[StarCount];
                for (int i = 0; i < StarCount; i++)
                {
                    stun.Stars[i] = new GameObject("CaliSkills.StunStar").AddComponent<SpriteRenderer>();
                    stun.Stars[i].sprite = ChadSkill.StarSprite;
                }
            }
            SpriteRenderer slimeRen = slime.spriteRen;
            bool visible = slime.isActiveAndEnabled && slimeRen != null;
            float radius = (float)slime.playerPhysics.radius;
            stun.Spin += Time.deltaTime * 6f;
            Vector2 up = ((Vector2)slime.body.up).normalized;
            Vector2 right = new(up.y, -up.x);
            Vector2 crown = (Vector2)slime.body.position + up * (radius * 2.4f);
            for (int i = 0; i < StarCount; i++)
            {
                SpriteRenderer star = stun.Stars[i];
                star.enabled = visible;
                if (!visible)
                    continue;
                float angle = stun.Spin + i * Mathf.PI * 2f / StarCount;
                // An ellipse seen from the side: the stars behind are smaller.
                float depth = Mathf.Sin(angle);
                star.transform.position = crown + right * (Mathf.Cos(angle) * radius * 2f) + up * (depth * radius * 0.5f);
                star.transform.localScale = Vector3.one * (radius * (1.45f + 0.4f * depth));
                star.transform.rotation = Quaternion.Euler(0f, 0f, stun.Spin * 40f);
                star.sortingLayerID = slimeRen.sortingLayerID;
                star.sortingOrder = slimeRen.sortingOrder + (depth > 0f ? 1 : -1);
                star.color = new Color(1f, 1f, 1f, slimeRen.color.a);
            }
        }
    }

    private void OnDestroy()
    {
        foreach (Stun stun in _stuns.Values)
        {
            if (stun.Stars == null)
                continue;
            foreach (SpriteRenderer star in stun.Stars)
            {
                if (star != null)
                    Destroy(star.gameObject);
            }
        }
        if (_instance == this)
            _instance = null;
    }

    // --- Patches ---

    /// <summary>A stunned slime's input is ignored while the game reads it (no move, jump or ability).</summary>
    [HarmonyPatch(typeof(SlimeController), "OldUpdate")]
    private static class NoInput
    {
        private static void Prefix(SlimeController __instance, out bool __state)
        {
            __state = __instance.suppressInput;
            if (IsStunned(__instance))
                __instance.suppressInput = true;
        }

        private static void Postfix(SlimeController __instance, bool __state)
        {
            if (IsStunned(__instance))
                __instance.suppressInput = __state;
        }
    }

    /// <summary>Stunned slimes can't use abilities (their icons dim).</summary>
    [HarmonyPatch(typeof(SlimeController), nameof(SlimeController.isAbilityCastable))]
    private static class NoAbilities
    {
        private static void Postfix(SlimeController __instance, ref bool __result)
        {
            if (__result && IsStunned(__instance))
                __result = false;
        }
    }
}
