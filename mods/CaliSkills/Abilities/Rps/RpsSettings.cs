using System.ComponentModel;

namespace CaliSkills.Abilities.Rps;

/// <summary>
/// Tuning values for the Rock Paper Scissors skill. These are the defaults: change them here and rebuild, or, with
/// CustomRules, as the lobby host in the rules menu (Abilities → right-click the skill; see Core/CustomRulesStats.cs).
/// Every public static float/int/bool field is a stat there, with its [Description] as hint. Every player uses the host's values.
/// </summary>
internal static class RpsSettings
{
    [Description("Seconds before the skill can be used again, counted from the release (the duel itself doesn't count).")]
    public static float AbilityCooldown = 3f;

    [Description("Radius of the yellow circle (units, scales with your size): on release you challenge the closest enemy slime in it.")]
    public static float CircleRadius = 10f;

    [Description("Seconds the VS screen is shown before the choice.")]
    public static float VsDuration = 2f;

    [Description("Seconds both players have to pick rock, paper or scissors (no pick = random).")]
    public static float ChoiceTime = 3f;

    [Description("Seconds the picks are shown before the loser is squashed and time starts again.")]
    public static float RevealTime = 2f;
}
