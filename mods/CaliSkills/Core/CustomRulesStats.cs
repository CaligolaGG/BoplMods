using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using CustomRules.Core;

namespace CaliSkills.Core;

/// <summary>
/// With CustomRules installed, every skill's settings (<see cref="Skill.SettingsType"/>) become ability stats the host
/// edits in the rules menu, sent to every player and applied at match start (then <see cref="Skill.OnSettingsChanged"/>).
/// CustomRules is a soft dependency: without it the skills keep the defaults from code.
/// </summary>
internal static class CustomRulesStats
{
    internal const string CustomRulesGuid = "com.caligola.customrules";

    internal static void RegisterIfInstalled()
    {
        if (Chainloader.PluginInfos.ContainsKey(CustomRulesGuid))
            Register();
        else
            Plugin.Log.LogInfo("CustomRules not installed: skill settings stay at their defaults.");
    }

    // Separate and never inlined: the CustomRules types are only loaded when this runs, i.e. when the mod is there.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Register()
    {
        foreach (Skill skill in SkillRegistry.All)
        {
            if (skill.SettingsType != null)
                AbilityStats.Register(skill.Name, skill.SettingsType, skill.OnSettingsChanged);
        }
    }
}
