using System.Collections.Generic;
using BoplFixedMath;
using HarmonyLib;
using UnityEngine;

namespace CaliSkills.Abilities.Charge;

/// <summary>
/// Slimes knocked by a Charge, for a short while (scene object, created on the first hit: reset every level). If one
/// lands on a platform (<see cref="PlayerPhysics.AttachToGround"/>, which a knocked slime flying into a floor, wall or
/// ceiling goes through) at <see cref="ChargeSettings.SquashSpeed"/> or more into it, it's squashed by the charger on
/// the next tick. Sim code: players in id order.
/// </summary>
internal sealed class ChargeKnocks : MonoUpdatable
{
    private sealed class Knock
    {
        public SlimeController Slime;
        public int Attacker;
        public Fix Left;
        public bool Squash;
    }

    private static ChargeKnocks _instance;
    private readonly SortedDictionary<int, Knock> _knocks = new();

    /// <summary>Watches <paramref name="slime"/> for <paramref name="duration"/> (restarted by another hit).</summary>
    internal static void Track(SlimeController slime, int attacker, Fix duration)
    {
        if (_instance == null)
            _instance = new GameObject("CaliSkills.ChargeKnocks").AddComponent<ChargeKnocks>();
        _instance._knocks[slime.GetPlayerId()] = new Knock { Slime = slime, Attacker = attacker, Left = duration };
    }

    private void Awake()
    {
        Updater.RegisterUpdatable(this);
    }

    public override void Init()
    {
    }

    private readonly List<int> _ids = new();


    public override void UpdateSim(Fix simDeltaTime)
    {
        List<int> ids = _ids; // reused: runs every tick
        ids.Clear();
        ids.AddRange(_knocks.Keys);
        foreach (int id in ids)
        {
            Knock k = _knocks[id];
            SlimeController slime = k.Slime;
            Player player = PlayerHandler.Get().GetPlayer(id);
            if (slime == null || slime.IsDestroyed || player == null || !player.IsAlive)
            {
                _knocks.Remove(id);
                continue;
            }
            if (k.Squash)
            {
                _knocks.Remove(id);
                PlayerCollision collision = slime.GetPlayerCollision();
                if (collision != null && slime.isActiveAndEnabled)
                    collision.killPlayer(k.Attacker, spawnEffect: true, ignoreInvulnerability: false, CauseOfDeath.Squished);
                continue;
            }
            k.Left -= GameTime.FixedDeltaTime(id, simDeltaTime);
            if (k.Left <= Fix.Zero)
                _knocks.Remove(id);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>A knocked slime hitting a platform: fast enough into it = squashed (velocity read before it's zeroed).</summary>
    [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.AttachToGround))]
    private static class SquashOnImpact
    {
        private static void Prefix(PlayerPhysics __instance, StickyRoundedRectangle wall)
        {
            if (_instance == null || wall == null || wall.IsDestroyed)
                return;
            SlimeController slime = __instance.GetComponent<SlimeController>();
            if (slime == null || !_instance._knocks.TryGetValue(slime.GetPlayerId(), out Knock k) || k.Slime != slime)
                return;
            PlayerBody body = slime.body;
            Vec2 normal = Vec2.NormalizedSafe(wall.currentNormal(body));
            Fix into = -Vec2.Dot(body.Velocity, normal);
            if (into >= (Fix)ChargeSettings.SquashSpeed)
                k.Squash = true;
        }
    }
}
