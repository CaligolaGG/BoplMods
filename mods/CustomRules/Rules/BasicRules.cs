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
    internal static Rule DisableAbilitySpawning;
    internal static Rule InfiniteRound;
    internal static Rule PlayerBouncing;
    internal static Rule BounceOnEverything;
    internal static Rule BouncePower;
    internal static Rule ExtraJump;
    internal static Rule AirJumps;

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
        DisableAbilitySpawning = RuleRegistry.AddBool("DisableAbilitySpawning", "Disable ability spawning", false,
            "No ability pickups: none appear during the round, and players don't drop their ability when they die.");
        InfiniteRound = RuleRegistry.AddBool("InfiniteRound", "Infinite round duration", false,
            "No sudden death: the platforms never start falling, however long the round lasts.");
        PlayerBouncing = RuleRegistry.AddBool("PlayerBouncing", "Player bouncing mode", false,
            "Slimes are solid: they bounce off each other instead of passing through.");
        BounceOnEverything = RuleRegistry.AddBool("PlayerBouncing.Everything", "Bounce on everything", false,
            "Slimes don't stick to platforms anymore: they bounce off them too. Touching a platform gives back your jump (press jump in the air).",
            parent: PlayerBouncing);
        BouncePower = RuleRegistry.AddFloat("PlayerBouncing.BouncePower", "Bounce power ×", 1f, 0f, 10f,
            "Multiplies how bouncy the slimes are (1 = keeps its speed off platforms).", parent: PlayerBouncing);
        ExtraJump = RuleRegistry.AddBool("ExtraJump", "Extra Jump", false,
            "Players can jump again in mid-air.");
        AirJumps = RuleRegistry.AddInt("ExtraJump.AirJumps", "Air jumps", 2, 1, 10,
            "Extra jumps in mid-air (2 = Triple Jump). They come back on landing.", parent: ExtraJump);
    }
}
