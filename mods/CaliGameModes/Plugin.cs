using BepInEx;
using BepInEx.Logging;
using CaliGameModes.Modes;
using CustomRules.Core;
using HarmonyLib;
using UnityEngine;

namespace CaliGameModes;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("BoplBattle.exe")]
[BepInDependency(CustomRules.Plugin.Guid)]
[BepInDependency("com.caligola.caliskills")]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    /// <summary>Every game mode's rules go in this group of the rules menu, set apart from the basic rules.</summary>
    internal static RuleSection Section;

    private void Awake()
    {
        Log = Logger;

        Section = RuleRegistry.AddSection("CaliGameModes", new Color(0.55f, 0.8f, 1f));
        PinballMode.Register();

        // Never unpatch in OnDestroy: Bopl Battle destroys the plugin GameObject before the main menu.
        new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(typeof(Plugin).Assembly);

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
    }
}
