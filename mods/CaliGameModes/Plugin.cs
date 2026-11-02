using BepInEx;
using BepInEx.Logging;
using CaliGameModes.Maps;
using CaliGameModes.Modes;
using CaliGameModes.Modes.Football;
using CustomRules.Core;
using HarmonyLib;
using UnityEngine;

namespace CaliGameModes;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("BoplBattle.exe")]
[BepInDependency(CustomRules.Plugin.Guid)]
[BepInDependency("com.caligola.caliskills")]
[BepInDependency(MapLoaderBridge.Guid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    /// <summary>Every game mode's rules go in this group of the rules menu, set apart from the basic rules.</summary>
    internal static RuleSection Section;

    private void Awake()
    {
        Log = Logger;

        Section = RuleRegistry.AddSection("CaliGameModes", new Color(0.55f, 0.8f, 1f));
        CustomMapsRule.Register();
        PinballMode.Register();
        FootballMode.Register();

        // Never unpatch in OnDestroy: Bopl Battle destroys the plugin GameObject before the main menu.
        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll(typeof(Plugin).Assembly);
        MapLoaderBridge.Init(harmony);

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
    }
}
