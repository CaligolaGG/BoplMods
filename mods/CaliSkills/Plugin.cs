using BepInEx;
using BepInEx.Logging;
using CaliSkills.Abilities.Pinball;
using CaliSkills.Core;
using HarmonyLib;

namespace CaliSkills;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("BoplBattle.exe")]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;

        // Every skill of the pack is declared here, in the order it's appended to the ability grid.
        SkillRegistry.Add(new PinballSkill());

        // Never unpatch in OnDestroy: Bopl Battle destroys the plugin GameObject before the main menu.
        new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(typeof(Plugin).Assembly);

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
    }
}
