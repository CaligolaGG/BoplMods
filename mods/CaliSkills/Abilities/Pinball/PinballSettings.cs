using System.ComponentModel;

namespace CaliSkills.Abilities.Pinball;

/// <summary>
/// Tuning values for the Pinball skill. These are the defaults: change them here and rebuild, or, with CustomRules,
/// as the lobby host in the rules menu (Abilities → right-click the skill; see Core/CustomRulesStats.cs). Every public
/// static float/int/bool field is a stat there, with its [Description] as hint. Every player uses the host's values.
///
/// The slime throws the ball like a grenade. Speed, size and knockback scale with how long the ability was held
/// (the ball grows in the slime's hand meanwhile): the values below are for a FULL charge, and the "...AtMinCharge"
/// fractions give the value for a quick tap. Example: BallSpeed 40 with SpeedAtMinCharge 0.4 → 16 on a tap,
/// 40 fully charged, linear in between. Full charge takes as long as the grenade's (its aim indicator stops growing).
/// </summary>
internal static class PinballSettings
{
    [Description("Launch speed at full charge (units/s). With BouncePower 1 the ball keeps it while bouncing.")]
    public static float BallSpeed = 40f;

    [Description("Ball radius at full charge (units), hitbox and sprite.")]
    public static float BallSize = 1.8f;

    [Description("Speed given to a hit player, at full charge.")]
    public static float BallKnockback = 80f;

    [Description("Seconds before the skill can be used again (not charge-scaled).")]
    public static float AbilityCooldown = 5f;

    [Description("Seconds the ball keeps bouncing after the throw (not charge-scaled).")]
    public static float AbilityDuration = 10f;

    // --- Charge scaling: fraction of each stat when released with no charge (1 = no scaling) ---

    [Description("Speed fraction on a quick tap (1 = no charge scaling).")]
    public static float SpeedAtMinCharge = 0.4f;

    [Description("Size fraction on a quick tap.")]
    public static float SizeAtMinCharge = 0.6f;

    [Description("Knockback fraction on a quick tap.")]
    public static float KnockbackAtMinCharge = 0.4f;

    // --- Extra tuning ---

    [Description("How much the ball bounces off things: 1 = keeps its speed, <1 slows down each bounce, >1 speeds up. " +
                 "On surfaces the bouncier of the two counts.")]
    public static float BouncePower = 1f;

    [Description("Gravity on the ball (0 = flies straight, 1 = normal gravity).")]
    public static float BallGravity = 0.8f;

    [Description("Seconds a hit player stays knocked (ragdolled, no control).")]
    public static float KnockDuration = 0.4f;

    [Description("Min share of the knock pushing away from the surface a player is stuck to (0..1).")]
    public static float MinDetachFraction = 0.5f;

    [Description("Seconds before the same ball can hit the same player again.")]
    public static float HitCooldownPerPlayer = 0.4f;
}
