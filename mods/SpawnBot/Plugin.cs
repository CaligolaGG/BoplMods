using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SpawnBot;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("BoplBattle.exe")]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static ConfigEntry<float> Cooldown;
    internal static ConfigEntry<int> MaxBots;
    internal static ConfigEntry<float> SpawnDistance;

    private void Awake()
    {
        Log = Logger;

        Cooldown = Config.Bind("General", "Cooldown", 1f,
            new ConfigDescription("Seconds between two SpawnBot casts.", new AcceptableValueRange<float>(0.1f, 30f)));
        MaxBots = Config.Bind("General", "MaxBots", 3,
            new ConfigDescription("Maximum number of bots alive at the same time.", new AcceptableValueRange<int>(1, 8)));
        SpawnDistance = Config.Bind("General", "SpawnDistance", 6f,
            new ConfigDescription("How far in front of you the bot appears.", new AcceptableValueRange<float>(1f, 30f)));

        // Never unpatch in OnDestroy: Bopl Battle destroys the plugin GameObject before the main menu.
        new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(typeof(Plugin).Assembly);

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
    }
}
