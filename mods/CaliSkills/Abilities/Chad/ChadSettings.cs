using System.ComponentModel;

namespace CaliSkills.Abilities.Chad;

/// <summary>
/// Tuning values for the Chad mode skill. These are the defaults: change them here and rebuild, or, with CustomRules,
/// as the lobby host in the rules menu (Abilities → right-click the skill; see Core/CustomRulesStats.cs). Every public
/// static float/int/bool field is a stat there, with its [Description] as hint. Every player uses the host's values.
/// </summary>
internal static class ChadSettings
{
    [Description("Seconds before Chad mode can be used again (starts when the mode ends).")]
    public static float AbilityCooldown = 3f;

    [Category("Slam")]
    [Description("Seconds of the slam's wind-up on a platform before it hits.")]
    public static float SlamWindup = 0.25f;

    [Category("Slam")]
    [Description("Speed of the dive when slamming in the air (units per second, scales with size).")]
    public static float SlamFallSpeed = 60f;

    [Category("Slam")]
    [Description("Seconds after the impact before you can move again.")]
    public static float SlamRecover = 0.3f;

    [Category("Slam")]
    [Description("Width of the rectangle under you that squashes players, in slime radiuses (centered on you, along the platform).")]
    public static float SlamWidth = 7f;

    [Category("Slam")]
    [Description("How far the squash rectangle reaches below your bottom, in slime radiuses (it starts at your middle).")]
    public static float SlamDepth = 2f;

    [Category("Slam")]
    [Description("How far the squash rectangle is shifted towards where you face, in slime radiuses (0 = centered on you; negative = backwards).")]
    public static float SlamForwardOffset = 1f;

    [Category("Slam")]
    [Description("How far the squash rectangle is shifted up, away from the platform (straight up in the air), in slime radiuses (negative = down).")]
    public static float SlamUpOffset = 1f;

    [Category("Slam")]
    [Description("How far the platform under you jitters at the impact (units; 0 = no shake).")]
    public static float SlamShake = 0.15f;

    [Category("Charge")]
    [Description("Distance of the charge (units, scales with size). It goes straight sideways; a platform in the way stops it.")]
    public static float ChargeDistance = 12f;

    [Category("Charge")]
    [Description("Speed of the charge (units per second, scales with size).")]
    public static float ChargeSpeed = 70f;

    [Category("Charge")]
    [Description("Length of the rectangle around you that stuns slimes, along the charge, in slime radiuses (centered on you).")]
    public static float ChargeHitLength = 4f;

    [Category("Charge")]
    [Description("Height of the stun rectangle, across the charge, in slime radiuses (centered on you).")]
    public static float ChargeHitHeight = 7.5f;

    [Category("Charge")]
    [Description("How far the stun rectangle is shifted towards where you charge, in slime radiuses (0 = centered on you; negative = backwards).")]
    public static float ChargeForwardOffset = 0.5f;

    [Category("Charge")]
    [Description("How far the stun rectangle is shifted up, away from the platform (straight up in the air), in slime radiuses (negative = down).")]
    public static float ChargeUpOffset = 0.4f;

    [Category("Charge")]
    [Description("Seconds a slime caught by the charge is stunned: it can't move, jump or use abilities, but keeps its momentum.")]
    public static float StunDuration = 1.3f;

    [Category("Aura farm")]
    [Description("Seconds of shining: invincible and immune to knockback, ragdolls, stuns and the chicken curse; reflects projectiles, beams and rays, pushes smoke away, turns black holes it touches into white holes and reveals invisible slimes nearby.")]
    public static float AuraDuration = 2.5f;

    [Category("Aura farm")]
    [Description("Size of the shining area that reflects projectiles and beams and touches black holes, in slime radiuses around you.")]
    public static float AuraSize = 6f;

    [Category("Aura farm")]
    [Description("How hard projectiles are pushed back when they hit the shining area: their bounced-off speed × this (1 = same speed they came at).")]
    public static float ReflectPower = 1.8f;

    [Category("Aura farm")]
    [Description("Distance within which smoke is pushed away (units, scales with size).")]
    public static float SmokeRadius = 12f;

    [Category("Aura farm")]
    [Description("Speed smoke is pushed away at (units per second).")]
    public static float SmokePush = 40f;

    [Category("Aura farm")]
    [Description("Distance within which invisible slimes are revealed (units, scales with size).")]
    public static float RevealRadius = 12f;
}
