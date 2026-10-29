using CustomRules.Core;

namespace CustomRules.Rules;

/// <summary>The rules of this mod. Their gameplay patches are in the other files of this folder.</summary>
internal static class BasicRules
{
    internal static Rule CooldownMultiplier;
    internal static Rule PlayerSize;
    internal static Rule Gravity;
    internal static Rule PlayerSpeed;
    internal static Rule PermanentInvisibility;

    internal static void Register()
    {
        CooldownMultiplier = RuleRegistry.AddFloat("CooldownMultiplier", "Cooldowns ×", 1f, 0f, 10f,
            "Multiplies every ability's cooldown (2 = double, 0.5 = half, 0 = no cooldown).");
        PlayerSize = RuleRegistry.AddInt("PlayerSize", "Player size", SizeRule.NormalSize, 1, 5,
            "Starting size of every player: 3 = normal, each step up/down = one Growth ray / Shrink ray hit at the start of the round.");
        Gravity = RuleRegistry.AddFloat("Gravity", "Gravity ×", 1f, 0f, 5f,
            "Multiplies gravity for players and objects (arrows, grenades...).");
        PlayerSpeed = RuleRegistry.AddFloat("PlayerSpeed", "Player speed ×", 1f, 0.1f, 5f,
            "Multiplies how fast players move (walking and in the air).");
        PermanentInvisibility = RuleRegistry.AddBool("PermanentInvisibility", "Permanent invisibility", false,
            "Every player is always invisible, as if the Invisibility ability never ended.");
    }
}
