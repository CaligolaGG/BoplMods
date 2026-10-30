using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CustomRules.Core;
using CustomRules.Rules;
using HarmonyLib;

namespace CustomRules;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("BoplBattle.exe")]
public class Plugin : BaseUnityPlugin
{
    /// <summary>For [BepInDependency] in mods that add rules.</summary>
    public const string Guid = MyPluginInfo.PLUGIN_GUID;

    internal static ManualLogSource Log;

    /// <summary>Kept in a static: the plugin object itself is destroyed by the game before the main menu.</summary>
    internal static ConfigFile RulesConfig;

    private void Awake()
    {
        Log = Logger;
        RulesConfig = Config;

        BasicRules.Register();
        RuleProfiles.Init();

        // Never unpatch in OnDestroy: Bopl Battle destroys the plugin GameObject before the main menu.
        new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(typeof(Plugin).Assembly);

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
    }
}
