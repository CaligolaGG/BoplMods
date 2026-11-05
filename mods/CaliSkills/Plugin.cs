using BepInEx;
using BepInEx.Logging;
using CaliSkills.Abilities.Bat;
using CaliSkills.Abilities.Chad;
using CaliSkills.Abilities.Charge;
using CaliSkills.Abilities.Chickenator;
using CaliSkills.Abilities.Fan;
using CaliSkills.Abilities.NothingPersonal;
using CaliSkills.Abilities.Omae;
using CaliSkills.Abilities.Pinball;
using CaliSkills.Abilities.Rps;
using CaliSkills.Abilities.Run;
using CaliSkills.Core;
using HarmonyLib;

namespace CaliSkills;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("BoplBattle.exe")]
[BepInDependency(CustomRulesStats.CustomRulesGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    private void Awake()
    {
        Log = Logger;

        // Every skill of the pack is declared here, in the order it's appended to the ability grid.
        SkillRegistry.Add(new PinballSkill());
        SkillRegistry.Add(new OmaeSkill());
        SkillRegistry.Add(new RunSkill());
        SkillRegistry.Add(new NothingPersonalSkill());
        SkillRegistry.Add(new BatSkill());
        SkillRegistry.Add(new RpsSkill());
        SkillRegistry.Add(new FanSkill());
        SkillRegistry.Add(new ChickenatorSkill());
        SkillRegistry.Add(new ChargeSkill());
        SkillRegistry.Add(new ChadSkill());
        SkillRegistry.Add(new Abilities.Flamethrower.FlamethrowerSkill());
        CustomRulesStats.RegisterIfInstalled();

        // Never unpatch in OnDestroy: Bopl Battle destroys the plugin GameObject before the main menu.
        new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(typeof(Plugin).Assembly);

        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
    }
}
