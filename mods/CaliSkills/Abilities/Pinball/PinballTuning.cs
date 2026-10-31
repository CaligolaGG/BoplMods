namespace CaliSkills.Abilities.Pinball;

/// <summary>
/// The values one ball plays with. <see cref="Ability"/> reads <see cref="PinballSettings"/> (the Pinball skill's
/// stats); other users of the ball (e.g. CaliGameModes' football) subclass it with their own. Read live, every use.
/// </summary>
internal class PinballTuning
{
    internal static readonly PinballTuning Ability = new();

    public virtual float BallSpeed => PinballSettings.BallSpeed;

    public virtual float BallSize => PinballSettings.BallSize;

    public virtual float BallKnockback => PinballSettings.BallKnockback;

    /// <summary>Seconds the ball lasts after the throw; 0 or less = until something destroys it.</summary>
    public virtual float AbilityDuration => PinballSettings.AbilityDuration;

    public virtual float SpeedAtMinCharge => PinballSettings.SpeedAtMinCharge;

    public virtual float SizeAtMinCharge => PinballSettings.SizeAtMinCharge;

    public virtual float KnockbackAtMinCharge => PinballSettings.KnockbackAtMinCharge;

    public virtual float EnvironmentBouncePower => PinballSettings.EnvironmentBouncePower;

    public virtual float PlayerBouncePower => PinballSettings.PlayerBouncePower;

    public virtual float BallGravity => PinballSettings.BallGravity;

    public virtual float KnockDuration => PinballSettings.KnockDuration;

    public virtual float MinDetachFraction => PinballSettings.MinDetachFraction;

    public virtual float HitCooldownPerPlayer => PinballSettings.HitCooldownPerPlayer;
}
