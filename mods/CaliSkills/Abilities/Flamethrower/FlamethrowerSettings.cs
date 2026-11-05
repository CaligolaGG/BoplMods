using System.ComponentModel;

namespace CaliSkills.Abilities.Flamethrower;

/// <summary>
/// Tuning values for the Flamethrower skill. These are the defaults: change them here and rebuild, or, with CustomRules,
/// as the lobby host in the rules menu (Abilities → right-click the skill; see Core/CustomRulesStats.cs). Every public
/// static float/int/bool field is a stat there, with its [Description] as hint. Every player uses the host's values.
/// </summary>
internal static class FlamethrowerSettings
{
    [Description("Seconds before the flamethrower can be used again, counted from when it stops (not on space maps).")]
    public static float AbilityCooldown = 3f;

    [Description("Cooldown on space maps, in seconds.")]
    public static float SpaceCooldown = 1.5f;

    [Description("Longest the flamethrower fires while you hold the button; releasing it stops it earlier.")]
    public static float MaxDuration = 2.5f;

    [Category("Flames")]
    [Description("How far the flames reach, from the slime's center (units, scales with your size).")]
    public static float FlameLength = 12f;

    [Category("Flames")]
    [Description("How wide the cone of flames opens, in degrees.")]
    public static float FlameAngle = 30f;

    [Category("Flames")]
    [Description("Whether the flames set smoke clouds off (like the Rocket's fire).")]
    public static bool IgniteSmoke = true;

    [Category("Flames")]
    [Description("Whether the flames blow up mines they reach.")]
    public static bool DetonateMines = true;

    [Category("Burning platforms (normal maps)")]
    [Description("Whether platforms hit by the flames catch fire (normal maps; never on space maps).")]
    public static bool PlatformsCatchFire = true;

    [Category("Burning platforms (normal maps)")]
    [Description("Seconds a burning spot keeps burning after the flames last hit it.")]
    public static float FireDuration = 2.5f;

    [Category("Burning platforms (normal maps)")]
    [Description("How fast the fire spreads along the platform, each way, in units per second.")]
    public static float FireSpreadSpeed = 4f;

    [Category("Burning platforms (normal maps)")]
    [Description("How far a fire spreads each way from where it started, in units.")]
    public static float FireMaxSpread = 12f;

    [Category("Burning platforms (normal maps)")]
    [Description("Whether players touching a burning part of a platform die.")]
    public static bool FireKills = true;

    [Category("Burning platforms (normal maps)")]
    [Description("Whether the flamethrower's owner can die in their own fire on a platform.")]
    public static bool FireHurtsOwner = false;

    [Category("Melting ice (ice maps)")]
    [Description("How fast a platform melts while the flames hit it: share of its size lost per second.")]
    public static float MeltSpeed = 0.35f;

    [Category("Melting ice (ice maps)")]
    [Description("The most a platform can melt: share of its size (0.6 = down to 40%).")]
    public static float MaxMelt = 0.6f;

    [Category("Melting ice (ice maps)")]
    [Description("Seconds after the flames last hit a platform before it starts growing back.")]
    public static float RegrowDelay = 2.5f;

    [Category("Melting ice (ice maps)")]
    [Description("How fast a melted platform grows back: share of its size per second.")]
    public static float RegrowSpeed = 0.1f;

    [Category("Jetpack (space maps)")]
    [Description("Share by which the cone of flames is smaller on space maps (0.3 = 30% shorter and narrower).")]
    public static float SpaceConeShrink = 0.3f;

    [Category("Jetpack (space maps)")]
    [Description("Push on you, away from where you aim, on space maps: speed added every second, in units per second.")]
    public static float JetpackPush = 140f;
}
