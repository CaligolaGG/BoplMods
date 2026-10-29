using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace CaliSkills.Core;

/// <summary>
/// Appends the pack's skills to the game's ability list (the "abilityIcons" NamedSpriteList asset,
/// see docs/ABILITIES.md). The ability grid, the match spawner and the Random/Revival abilities all read
/// that list, so a new entry is all the game needs to offer and use a skill.
/// </summary>
internal static class SkillRegistry
{
    private static readonly List<Skill> Skills = new();
    private static readonly Dictionary<Skill, NamedSprite> Entries = new();
    private static GameObject _prefabRoot;

    /// <summary>Inactive, persistent parent: children never run Awake, but clones made from them do.</summary>
    internal static Transform PrefabRoot
    {
        get
        {
            if (_prefabRoot == null)
            {
                _prefabRoot = new GameObject("CaliSkills.Prefabs");
                _prefabRoot.SetActive(false);
                Object.DontDestroyOnLoad(_prefabRoot);
            }
            return _prefabRoot.transform;
        }
    }

    internal static void Add(Skill skill) => Skills.Add(skill);

    internal static Skill Find(string name) => Skills.Find(s => s.Name == name);

    internal static void RegisterEverywhere()
    {
        foreach (NamedSpriteList list in Resources.FindObjectsOfTypeAll<NamedSpriteList>())
        {
            if (IsFullAbilityList(list))
                Register(list);
        }
    }

    internal static void Register(NamedSpriteList list)
    {
        if (list == null || !IsFullAbilityList(list))
            return;

        foreach (Skill skill in Skills)
        {
            if (list.IndexOf(skill.Name) >= 0)
                continue;

            if (!Entries.TryGetValue(skill, out NamedSprite entry))
            {
                entry = new NamedSprite(skill.Name, skill.CreateIcon(list), skill.CreatePrefab(list), skill.IsOffensive);
                Entries[skill] = entry;
            }

            // Appended last: indices are ability ids, so vanilla abilities keep theirs.
            list.sprites.Add(entry);
            Plugin.Log.LogInfo($"Registered skill '{skill.Name}' as #{list.sprites.Count - 1} in '{list.name}'.");
        }
    }

    private static bool IsFullAbilityList(NamedSpriteList list) =>
        list.sprites != null && list.sprites.Count > 2 && list.sprites[0].name == "NullAbility" && list.IndexOf("Magnet gun") >= 0;
}

[HarmonyPatch(typeof(AbilityGrid), nameof(AbilityGrid.Awake))]
internal static class RegisterBeforeAbilityGridPatch
{
    // The grid sizes itself from the list in Awake, so the entries must exist before.
    private static void Prefix(AbilityGrid __instance)
    {
        SkillRegistry.Register(__instance.abilityIcons);
        SkillRegistry.RegisterEverywhere();
    }
}

[HarmonyPatch(typeof(CharacterSelectHandler), nameof(CharacterSelectHandler.TryStartGame_inner))]
internal static class RegisterBeforeMatchStartPatch
{
    // Match start reads SteamManager.instance.abilityIcons.
    private static void Prefix()
    {
        if (SteamManager.instance != null)
            SkillRegistry.Register(SteamManager.instance.abilityIcons);
    }
}
