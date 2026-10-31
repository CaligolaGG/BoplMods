using System.ComponentModel;
using CaliSkills.Abilities.Pinball;

namespace CaliGameModes.Modes.Football;

/// <summary>
/// The football's stats: defaults here, set by the host in the rules menu (Football mode → Edit football), separate
/// from the Pinball ability's. The ball lasts until a goal (no duration).
/// </summary>
internal static class FootballSettings
{
    [Description("Ball radius (units).")]
    public static float BallSize = 1.5f;

    [Description("Speed given to a player the ball hits.")]
    public static float BallKnockback = 20f;

    [Description("Speed the ball leaves a player with (× player bounce power).")]
    public static float BallSpeed = 30f;

    [Category("Bounce power")]
    [Description("Bounce off everything but players (platforms, items, abilities): 1 = keeps its speed, <1 slows down " +
                 "each bounce, >1 speeds up. On surfaces the bouncier of the two counts.")]
    public static float EnvironmentBouncePower = 1f;

    [Category("Bounce power")]
    [Description("Bounce off players it hits: 1 = keeps its speed, <1 slows down, >1 speeds up.")]
    public static float PlayerBouncePower = 1.3f;

    [Description("Gravity on the ball (0 = floats, 1 = normal gravity).")]
    public static float BallGravity = 1f;

    [Description("Seconds a hit player stays knocked (ragdolled, no control).")]
    public static float KnockDuration = 0.3f;

    [Description("Seconds before the same ball can hit the same player again.")]
    public static float HitCooldownPerPlayer = 0.2f;
}

/// <summary>The football plays with <see cref="FootballSettings"/>, at full charge, and never expires.</summary>
internal sealed class FootballTuning : PinballTuning
{
    internal static readonly FootballTuning Instance = new();

    public override float BallSpeed => FootballSettings.BallSpeed;

    public override float BallSize => FootballSettings.BallSize;

    public override float BallKnockback => FootballSettings.BallKnockback;

    public override float AbilityDuration => 0f;

    public override float SpeedAtMinCharge => 1f;

    public override float SizeAtMinCharge => 1f;

    public override float KnockbackAtMinCharge => 1f;

    public override float EnvironmentBouncePower => FootballSettings.EnvironmentBouncePower;

    public override float PlayerBouncePower => FootballSettings.PlayerBouncePower;

    public override float BallGravity => FootballSettings.BallGravity;

    public override float KnockDuration => FootballSettings.KnockDuration;

    public override float HitCooldownPerPlayer => FootballSettings.HitCooldownPerPlayer;
}
