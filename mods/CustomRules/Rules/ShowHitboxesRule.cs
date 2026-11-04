using CustomRules.Core;

namespace CustomRules.Rules;

/// <summary>
/// "Show hitboxes": a checkbox in the abilities window (Edit abilities). Saved, synced and in profiles like every rule,
/// but with no row in the Rules tab. Ability mods read <see cref="Active"/> and draw their hidden hitboxes while it's on
/// (CaliSkills does); nothing in the game itself changes.
/// </summary>
public static class ShowHitboxesRule
{
    internal static Rule Rule { get; private set; }

    /// <summary>Whether hitboxes should be drawn in the match in progress (the host's value).</summary>
    public static bool Active => Rule != null && Rule.ActiveBool;

    internal static void Register()
    {
        Rule = RuleRegistry.AddHidden("ShowHitboxes", RuleType.Bool, false,
            "Draw the abilities' hidden hitboxes in red during matches (for testing; visual only).", "Rules");
    }
}
