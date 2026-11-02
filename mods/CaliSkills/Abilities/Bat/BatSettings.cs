using System.ComponentModel;

namespace CaliSkills.Abilities.Bat;

/// <summary>
/// Tuning values for the Wooden Bat skill. These are the defaults: change them here and rebuild, or, with CustomRules,
/// as the lobby host in the rules menu (Abilities → right-click the skill; see Core/CustomRulesStats.cs). Every public
/// static float/int/bool field is a stat there, with its [Description] as hint. Every player uses the host's values.
/// </summary>
internal static class BatSettings
{
    [Description("Seconds before the bat can be used again, counted from the end of the swing.")]
    public static float AbilityCooldown = 3f;

    [Description("Seconds a swing takes, from start to end of the arc.")]
    public static float SwingTime = 0.2f;

    [Description("Angle the bat sweeps, in degrees, centered on the aimed direction.")]
    public static float SwingArc = 200f;

    [Description("Bat length (units). Scales with the slime's size.")]
    public static float BatLength = 7f;

    [Description("Bat thickness (radius, units). Scales with the slime's size.")]
    public static float BatThickness = 0.7f;

    [Description("Share of the bat's speed (where it hits) given to a hit player as a knock.")]
    public static float HitKnock = 0.35f;

    [Description("Smallest knock speed given to a hit player.")]
    public static float MinKnock = 15f;

    [Description("Speed you get when the bat hits a platform (pogo), away from where it hit.")]
    public static float PogoSpeed = 100f;

    [Description("On: the bat hits flying projectiles (arrows, grenades, smoke grenades, pinballs, missiles, mines...) back, and they become yours.")]
    public static bool DeflectProjectiles = true;

    [Description("Share of the bat's speed (where it hits) a deflected projectile leaves with, at least (it also bounces off with its own speed).")]
    public static float DeflectPower = 0.5f;

    [Description("On: the bat never squishes. A player caught between it and a platform is only knocked, and the bat goes through them.")]
    public static bool NoSquashing = false;
}
