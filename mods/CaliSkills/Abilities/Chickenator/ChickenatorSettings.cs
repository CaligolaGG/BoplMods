using System.ComponentModel;

namespace CaliSkills.Abilities.Chickenator;

/// <summary>
/// Tuning values for the Chickenator skill. These are the defaults: change them here and rebuild, or, with CustomRules,
/// as the lobby host in the rules menu (Abilities → right-click the skill; see Core/CustomRulesStats.cs). Every public
/// static float/int/bool field is a stat there, with its [Description] as hint. Every player uses the host's values.
/// </summary>
internal static class ChickenatorSettings
{
    [Description("Seconds before the potion can be thrown again.")]
    public static float AbilityCooldown = 4f;

    [Description("Seconds before the potion bursts by itself, counted from when you take it out (hold it too long and it bursts in your hand).")]
    public static float FuseTime = 3f;

    [Description("Seconds after the throw before touching a platform or wall makes the potion burst (so throwing it from the floor doesn't burst it at your feet). Players and explosions burst it at once; the fuse keeps running.")]
    public static float ArmTime = 0.2f;

    [Description("Radius of the burst (units, scales with the thrower's size). Everyone in it, you too, becomes a chicken.")]
    public static float BurstRadius = 11f;

    [Description("Seconds a player stays a chicken (another potion starts the time again).")]
    public static float ChickenDuration = 3f;

    [Category("Chicken")]
    [Description("How big the chicken looks, in slime radiuses (visual only; the body you can be hit on stays the slime's).")]
    public static float ChickenSize = 4f;

    [Category("Chicken")]
    [Description("Jump height of a chicken, as a share of a slime's (0.5 = half as high).")]
    public static float JumpHeight = 0.5f;

    [Category("Chicken")]
    [Description("Weight of a chicken, as a multiple of a slime's: falls faster and is pushed less (1.3 = 30% heavier).")]
    public static float Weight = 1.5f;

    [Category("Chicken")]
    [Description("Speed of a chicken, as a share of a slime's (0.9 = 10% slower).")]
    public static float Speed = 0.85f;
}
