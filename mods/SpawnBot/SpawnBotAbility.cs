using System;
using BoplFixedMath;

namespace SpawnBot;

/// <summary>
/// The ability component. Instantiated by the game (like any other ability prefab) when a player
/// picked SpawnBot in character select. Instant: casting it doesn't hide or move the caster.
/// </summary>
public class SpawnBotAbility : AbilityMonoBehaviour
{
    private SlimeController _caster;

    public override bool IsRope => false;
    public override bool CooldownStoppedDuringTimeStop => false;
    public override bool IgnoreNextCooldown => false;

    private void Awake()
    {
        // Same as the game's InstantAbility: abilities are simulation objects driven by Updater.
        Updater.RegisterUpdatable(this);
    }

    public override void Init()
    {
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
    }

    public override void EnterAbility(PlayerInfo playerInfo, Action<AbilityExitInfo> onExitAbility)
    {
        _caster = playerInfo.slimeController;
        BotSpawner.TrySpawnBot(playerInfo);
    }

    public override void ExitAbility(AbilityExitInfo info)
    {
    }

    public override Fix GetCooldown() => (Fix)Plugin.Cooldown.Value;

    public override bool IsInstant() => true;

    public override bool IsCastable(Player player) => true;

    public override void UpdateCooldowns(PlayerBody playerBody)
    {
    }

    public override PlayerCollision GetPlayerCollision() => null;

    public override SlimeController GetSlimeController() => _caster;
}
