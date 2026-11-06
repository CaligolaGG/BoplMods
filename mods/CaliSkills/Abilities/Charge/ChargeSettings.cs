using System.ComponentModel;

namespace CaliSkills.Abilities.Charge;

/// <summary>
/// Tuning values for the Charge skill. These are the defaults: change them here and rebuild, or, with CustomRules,
/// as the lobby host in the rules menu (Abilities → right-click the skill; see Core/CustomRulesStats.cs). Every public
/// static float/int/bool field is a stat there, with its [Description] as hint. Every player uses the host's values.
/// </summary>
internal static class ChargeSettings
{
    [Description("Seconds before the charge can be used again (starts when the dash ends).")]
    public static float AbilityCooldown = 4f;

    [Description("Seconds of holding for a full charge; at this point the slime launches by itself.")]
    public static float MaxChargeTime = 3f;

    [Description("How much bigger the slime gets at a full charge (0.5 = 50% bigger). It also hits players that much further out.")]
    public static float MaxGrowth = 0.75f;

    [Category("Dash")]
    [Description("Distance of the dash released right away (units, scales with the slime's size).")]
    public static float MinDistance = 8f;

    [Category("Dash")]
    [Description("Distance of the dash at a full charge (units, scales with the slime's size).")]
    public static float MaxDistance = 35f;

    [Category("Dash")]
    [Description("Speed of the dash (units per second, scales with the slime's size).")]
    public static float DashSpeed = 90f;

    [Category("Dash")]
    [Description("Share of the dash's speed the slime keeps when it ends in the air (0 = stops dead).")]
    public static float EndMomentum = 0.3f;

    [Category("Hit")]
    [Description("Knockback given to players hit by a dash released right away.")]
    public static float MinKnockback = 70f;

    [Category("Hit")]
    [Description("Knockback given to players hit by a fully charged dash.")]
    public static float MaxKnockback = 100f;

    [Category("Hit")]
    [Description("How much players hit are also pushed aside, out of the dash's path (0 = straight ahead).")]
    public static float SideKnock = 0.4f;

    [Category("Hit")]
    [Description("Push given to a platform the dash slams into, at a full charge (less with less charge; the Meteor's is 30). Platforms that can't move only shake.")]
    public static float PlatformKnockback = 60f;

    [Category("Hit")]
    [Description("Seconds a hit player tumbles helplessly.")]
    public static float KnockDuration = 0.6f;

    [Category("Hit")]
    [Description("Speed into a platform that squashes a hit player (while knocked back, and a little after). The game cuts sideways knockback to two thirds, so the default needs most of a charge.")]
    public static float SquashSpeed = 20f;
}
