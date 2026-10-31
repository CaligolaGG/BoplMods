using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using CustomRules.Core;
using HarmonyLib;

namespace CaliSkills.Core;

/// <summary>
/// With CustomRules installed, every skill's settings (<see cref="Skill.SettingsType"/>) become ability stats the host
/// edits in the rules menu, sent to every player and applied at match start (then <see cref="Skill.OnSettingsChanged"/>).
/// Skills whose offensive flag depends on a setting (<see cref="Skill.IsOffensiveWith"/>) are kept up to date: in the
/// lobby every frame from the lobby's values (for CustomRules' offensive filters), at match start from the active ones
/// (for Random's draws in the match).
/// CustomRules is a soft dependency: without it the skills keep the defaults from code.
/// </summary>
internal static class CustomRulesStats
{
    internal const string CustomRulesGuid = "com.caligola.customrules";

    private static bool _installed;

    internal static void RegisterIfInstalled()
    {
        _installed = Chainloader.PluginInfos.ContainsKey(CustomRulesGuid);
        if (_installed)
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
            if (skill.SettingsType == null)
                continue;
            Skill s = skill;
            AbilityStats.Register(s.Name, s.SettingsType, () =>
            {
                s.OnSettingsChanged();
                SkillRegistry.SetOffensive(s, s.IsOffensiveWith(_ => null));
            });
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RefreshOffensiveFromLobby()
    {
        foreach (Skill skill in SkillRegistry.All)
        {
            Skill s = skill;
            SkillRegistry.SetOffensive(s, s.IsOffensiveWith(stat => AbilityStats.TryGetLobbyValue(s.Name, stat, out string v) ? v : null));
        }
    }

    /// <summary>Character select (local and online): the offensive flags follow the lobby's settings.</summary>
    [HarmonyPatch]
    private static class RefreshInLobby
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CharacterSelectHandler), "Update");
            yield return AccessTools.Method(typeof(CharacterSelectHandler_online), "Update");
        }

        private static void Postfix()
        {
            if (_installed)
                RefreshOffensiveFromLobby();
        }
    }
}
