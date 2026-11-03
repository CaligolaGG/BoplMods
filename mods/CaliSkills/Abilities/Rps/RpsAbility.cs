using System;
using System.Linq;
using BoplFixedMath;
using UnityEngine;

namespace CaliSkills.Abilities.Rps;

/// <summary>
/// Rock Paper Scissors: an instant ability (the slime stays a slime).
/// - Pressed and held: a yellow circle (<see cref="RpsSettings.CircleRadius"/>) shows around the slime, which moves
///   freely. The cooldown waits (its timer is pinned to 0 until the release).
/// - Released: the closest enemy slime touching the circle is challenged to a duel (<see cref="RpsDuel"/>); nobody in
///   it = nothing happens (the cooldown starts anyway).
/// Sim code: inputs and Fix math only, slimes in a fixed order. The circle is a visual drawn from the sim state.
/// </summary>
public class RpsAbility : AbilityMonoBehaviour
{
    private SlimeController _caster;
    private Player _player;
    private int _slot = -1;
    private bool _holding;

    private SpriteRenderer _circle;

    public override bool IsRope => false;
    public override bool CooldownStoppedDuringTimeStop => false;
    public override bool IgnoreNextCooldown => false;

    private void Awake()
    {
        // Same as the game's InstantAbility: abilities are simulation objects driven by Updater.
        Updater.RegisterUpdatable(this);

        var circle = new GameObject("RpsCircle");
        circle.transform.SetParent(transform, worldPositionStays: false);
        _circle = circle.AddComponent<SpriteRenderer>();
        _circle.sprite = RpsSkill.RingSprite;
        _circle.enabled = false;
    }

    public override void Init()
    {
    }

    public override void EnterAbility(PlayerInfo playerInfo, Action<AbilityExitInfo> onExitAbility)
    {
        // Ability objects are spawned inactive and Updater skips inactive ones: UpdateSim must run from now on.
        gameObject.SetActive(true);
        _caster = playerInfo.slimeController;
        _player = PlayerHandler.Get().GetPlayer(playerInfo.playerId);
        _slot = playerInfo.AbilityButtonUsedIndex012;
        if (_caster == null || _player == null || _holding)
            return;
        _holding = true;
        AudioManager.Get()?.Play("abilityPickup");
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        if (!_holding)
            return;
        if (!CasterUsable() || RpsDuel.Current != null)
        {
            _holding = false;
            return;
        }

        // The cooldown starts on release.
        if (_slot >= 0 && _slot < _caster.abilityCooldownTimers.Length)
            _caster.abilityCooldownTimers[_slot] = Fix.Zero;

        if (_player.AbilityButtonIsDown(_slot))
            return;
        _holding = false;
        SlimeController target = Target();
        if (target != null)
            RpsDuel.Begin(_caster, target);
    }

    private bool CasterUsable() =>
        _caster != null && !_caster.IsDestroyed && _caster.isActiveAndEnabled && !_caster.isInAbility && _player.IsAlive;

    private Fix Radius => (Fix)RpsSettings.CircleRadius * _caster.body.fixtrans.Scale;

    /// <summary>The closest alive enemy slime touching the circle (ties: lower player id), or null.</summary>
    private SlimeController Target()
    {
        Vec2 center = _caster.body.position;
        Fix radius = Radius;
        return FindObjectsOfType<SlimeController>()
            .Where(s => s != _caster && RpsDuel.CanDuel(s) && s.GetPlayerId() != _player.Id &&
                        PlayerHandler.Get().GetPlayer(s.GetPlayerId()) is Player p && p.Team != _player.Team)
            .Select(s => (slime: s, reach: radius + s.playerPhysics.radius, d2: Vec2.SqrMagnitude(s.body.position - center)))
            .Where(t => t.d2 <= t.reach * t.reach)
            .OrderBy(t => t.d2.m_rawValue)
            .ThenBy(t => t.slime.GetPlayerId())
            .Select(t => t.slime)
            .FirstOrDefault();
    }

    private void LateUpdate()
    {
        bool show = _holding && _caster != null && !_caster.IsDestroyed && _caster.isActiveAndEnabled;
        _circle.enabled = show;
        if (!show)
            return;
        float diameter = 2f * (float)Radius;
        _circle.transform.position = (Vector2)_caster.body.position;
        _circle.transform.rotation = Quaternion.identity;
        _circle.transform.localScale = Vector3.one;
        Vector3 parentScale = _circle.transform.lossyScale;
        _circle.transform.localScale = new Vector3(
            diameter / Mathf.Max(0.0001f, parentScale.x), diameter / Mathf.Max(0.0001f, parentScale.y), 1f);
        SpriteRenderer slime = _caster.spriteRen;
        if (slime != null)
        {
            _circle.sortingLayerID = slime.sortingLayerID;
            _circle.sortingOrder = slime.sortingOrder - 1;
        }
    }

    public override void ExitAbility(AbilityExitInfo info)
    {
    }

    public override Fix GetCooldown() => (Fix)RpsSettings.AbilityCooldown;

    public override bool IsInstant() => true;

    public override bool IsCastable(Player player) => !_holding && RpsDuel.Current == null;

    public override void UpdateCooldowns(PlayerBody playerBody)
    {
    }

    public override PlayerCollision GetPlayerCollision() => null;

    public override SlimeController GetSlimeController() => _caster;
}
