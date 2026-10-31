using System.ComponentModel;

namespace CaliSkills.Abilities.NothingPersonal;

/// <summary>
/// Tuning values for the "Nothing Personal Kid" skill. These are the defaults: change them here and rebuild, or, with CustomRules,
/// as the lobby host in the rules menu (Abilities → right-click the skill; see Core/CustomRulesStats.cs). Every public
/// static float/int/bool field is a stat there, with its [Description] as hint. Every player uses the host's values.
/// </summary>
internal static class NothingPersonalSettings
{
    [Description("Seconds before the skill can be used again.")]
    public static float AbilityCooldown = 10f;

    [Description("Seconds the teleport takes: you vanish at once and appear at the marker after this (0 = instant).")]
    public static float TravelTime = 0.1f;

    [Description("Seconds you're immune and untouchable after appearing, like after a respawn.")]
    public static float ImmunityDuration = 0.3f;

    [Description("Seconds you can't use other abilities after appearing.")]
    public static float AbilityLockDuration = 0.3f;

    [Description("Gap between you and the target when appearing behind them (units, scales with size).")]
    public static float BehindGap = 1f;

    [Description("How far above the target's center you appear, so you're a bit off-center (units, scales with size).")]
    public static float BehindLift = 0.8f;

    [Description("Seconds the smoke cloud left behind stays (visual only).")]
    public static float SmokeDuration = 2f;

    [Description("Size of the smoke cloud (visual only).")]
    public static float SmokeSize = 0.6f;

    [Description("Size of the teleport marker, relative to the Teleport ability's (visual only).")]
    public static float MarkerSize = 0.8f;
}
