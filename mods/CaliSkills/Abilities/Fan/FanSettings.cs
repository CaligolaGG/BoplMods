using System.ComponentModel;

namespace CaliSkills.Abilities.Fan;

/// <summary>
/// Tuning values for the Fan skill. These are the defaults: change them here and rebuild, or, with CustomRules, as the
/// lobby host in the rules menu (Abilities → right-click the skill; see Core/CustomRulesStats.cs). Every public static
/// float/int/bool field is a stat there, with its [Description] as hint. Every player uses the host's values.
/// </summary>
internal static class FanSettings
{
    [Description("Seconds before the fan can be used again, counted from when you're a slime again.")]
    public static float AbilityCooldown = 4f;

    [Description("Longest the fan blows while you hold the button (not counting the transformations); releasing it stops the fan earlier.")]
    public static float Duration = 4f;

    [Description("Seconds the transformation takes, slime to fan and back (no wind meanwhile).")]
    public static float TransformTime = 0.4f;

    [Description("How fast the fan turns towards where you aim, in degrees per second.")]
    public static float RotationSpeed = 150f;

    [Description("Size of the fan, in slime radiuses (the wind starts at its cage).")]
    public static float FanSize = 5f;

    [Category("Wind area")]
    [Description("Length of the wind (units, scales with your size).")]
    public static float WindLength = 50f;

    [Category("Wind area")]
    [Description("Width of the wind at the fan: the short side of the trapezoid (units, scales with your size).")]
    public static float WindWidthNear = 10f;

    [Category("Wind area")]
    [Description("Width of the wind at its far end: the long side of the trapezoid (units, scales with your size).")]
    public static float WindWidthFar = 15f;

    [Category("Wind strength")]
    [Description("Push right at the fan: speed added every second, in units per second.")]
    public static float StrengthNear = 400f;

    [Category("Wind strength")]
    [Description("Push at the far end of the wind (it fades from the fan's strength to this).")]
    public static float StrengthEdge = 250f;

    [Category("Wind strength")]
    [Description("Share of the push given to platforms (they're heavy).")]
    public static float PlatformPush = 0.15f;

    [Category("Wind strength")]
    [Description("Share of the push given to smoke clouds, which drift with the wind.")]
    public static float SmokeDrift = 0.08f;
}
