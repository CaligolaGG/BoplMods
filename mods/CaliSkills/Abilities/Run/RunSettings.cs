using System.ComponentModel;

namespace CaliSkills.Abilities.Run;

/// <summary>
/// Tuning values for the Run skill. These are the defaults: change them here and rebuild, or live in game by
/// right-clicking the skill in the ability grid (see Core/SkillSettingsEditor.cs).
/// All players in a match must use the same values (the game simulation is deterministic lockstep).
/// </summary>
internal static class RunSettings
{
    [Description("Speed while running, as a multiplier (1.2 = 20% faster).")]
    public static float SpeedMultiplier = 1.5f;

    [Description("Seconds of cooldown, starting when the button is released (or another ability stops the run).")]
    public static float AbilityCooldown = 3f;
}
