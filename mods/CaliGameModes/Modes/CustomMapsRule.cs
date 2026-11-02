using CaliGameModes.Maps;
using CaliGameModes.Modes.Football;
using CustomRules.Core;

namespace CaliGameModes.Modes;

/// <summary>
/// With the Map Loader mod installed every match is played on its custom maps. This rule (on by default) leaves them
/// out so matches use the game's own levels; football ignores it and always plays on its football maps.
/// </summary>
internal static class CustomMapsRule
{
    internal static Rule DeactivateCustomMaps;

    internal static void Register()
    {
        DeactivateCustomMaps = RuleRegistry.AddBool("DeactivateCustomMaps", "Deactivate custom maps", true,
            "With the Map Loader mod: its custom maps are left out and matches use the game's own levels. " +
            "Football mode always plays on its football maps.", section: Plugin.Section);

        // Lobby values: the host's for online guests, so every machine makes the same choice.
        MapLoaderBridge.UseGameLevels = () => DeactivateCustomMaps.LobbyBool && !FootballMode.Enabled.LobbyBool;
    }
}
