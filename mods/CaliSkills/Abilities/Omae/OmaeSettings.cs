using System.ComponentModel;

namespace CaliSkills.Abilities.Omae;

/// <summary>
/// Tuning values for the "Omae wa mo shindeiru" skill. These are the defaults: change them here and rebuild, or live
/// in game by right-clicking the skill in the ability grid (see Core/SkillSettingsEditor.cs).
/// All players in a match must use the same values (the game simulation is deterministic lockstep).
/// </summary>
internal static class OmaeSettings
{
    [Description("Seconds before the skill can be used again.")]
    public static float AbilityCooldown = 5f;

    [Description("Seconds a poked player has left before dying.")]
    public static float MarkDuration = 20f;

    [Description("Seconds removed from the timer when an already marked player is poked again.")]
    public static float MarkDurationReduction = 5f;

    [Description("Reach of the poke (cone length, units). Scales with the slime's size.")]
    public static float HitboxSize = 8f;

    [Description("Width of the poke cone, in degrees.")]
    public static float HitboxAngle = 60f;

    [Description("Volume of the \"Omae wa mou shindeiru\" line played when a poke marks someone (1 = normal).")]
    public static float SoundVolume = 1f;

    [Description("Flashes per second on a freshly marked player (visual only).")]
    public static float FlashesPerSecond = 1f;

    [Description("Flashes per second when the mark is about to run out; the flashing also gets stronger (visual only).")]
    public static float FlashesPerSecondAtEnd = 8f;
}
