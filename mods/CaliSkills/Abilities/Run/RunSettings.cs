using System.ComponentModel;

namespace CaliSkills.Abilities.Run;

/// <summary>
/// Tuning values for the Run skill. These are the defaults: change them here and rebuild, or, with CustomRules,
/// as the lobby host in the rules menu (Abilities → right-click the skill; see Core/CustomRulesStats.cs). Every public
/// static float/int/bool field is a stat there, with its [Description] as hint. Every player uses the host's values.
/// </summary>
internal static class RunSettings
{
    [Description("Speed while running, as a multiplier (1.2 = 20% faster).")]
    public static float SpeedMultiplier = 1.5f;

    [Description("Seconds of cooldown, starting when the button is released (or another ability stops the run).")]
    public static float AbilityCooldown = 3f;
}
