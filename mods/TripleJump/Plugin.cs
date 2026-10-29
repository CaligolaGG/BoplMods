using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace TripleJump;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("BoplBattle.exe")]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static ConfigEntry<int> AirJumps;
    internal static ConfigEntry<bool> DebugLogging;

    private void Awake()
    {
        Log = Logger;

        // Written to BepInEx/config/com.boplmods.triplejump.cfg in the mod manager profile.
        // Every player in an online lobby must use the same value, or the lockstep simulation desyncs.
        AirJumps = Config.Bind(
            "General",
            "AirJumps",
            2,
            new ConfigDescription(
                "Extra jumps allowed while airborne. 2 = triple jump (1 ground jump + 2 air jumps).",
                new AcceptableValueRange<int>(0, 10)));

        DebugLogging = Config.Bind(
            "Debug",
            "DebugLogging",
            false,
            "Log every jump press with the state used to decide on an air jump.");

        // Patches must stay applied for the whole session. Don't unpatch in OnDestroy: Bopl Battle destroys
        // the BepInEx manager GameObject (and with it this plugin) before the main menu.
        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll(typeof(Plugin).Assembly);

        if (DebugLogging.Value)
        {
            foreach (var method in harmony.GetPatchedMethods())
                Log.LogInfo($"Patched {method.DeclaringType?.Name}.{method.Name}");
        }

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded ({AirJumps.Value} air jumps).");
    }
}
