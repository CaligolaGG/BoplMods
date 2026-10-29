using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SpawnBot;

/// <summary>
/// Adds "SpawnBot" to the game's ability list (the NamedSpriteList "abilityIcons" asset, see docs/ABILITIES.md).
/// The character select grid, the match spawner and the Revival ability all read that list,
/// so appending an entry is enough to make the ability selectable and usable.
/// </summary>
internal static class AbilityRegistration
{
    internal const string AbilityName = "SpawnBot";

    private static GameObject _template;
    private static Sprite _icon;

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
        if (list == null || list.IndexOf(AbilityName) >= 0)
            return;

        // Copy size/pixels-per-unit from an existing icon so ours matches in the grid and above the slime.
        _icon ??= IconFactory.CreateIcon(list.sprites[2].sprite);
        _template ??= CreateTemplate();

        // Appended last: indices are ability ids, so existing abilities keep theirs.
        list.sprites.Add(new NamedSprite(AbilityName, _icon, _template, isOffensiveAbility: false));
        Plugin.Log.LogInfo($"Registered ability '{AbilityName}' as #{list.sprites.Count - 1} in '{list.name}'.");
    }

    private static bool IsFullAbilityList(NamedSpriteList list) =>
        list.sprites != null && list.sprites.Count > 2 && list.sprites[0].name == "NullAbility" && list.IndexOf("Magnet gun") >= 0;

    /// <summary>
    /// The "prefab" the game instantiates for every player that picked SpawnBot. It stays inactive, so its
    /// own Awake never runs; FixTransform.InstantiateFixed briefly activates each clone to register it.
    /// </summary>
    private static GameObject CreateTemplate()
    {
        var go = new GameObject(AbilityName);
        go.SetActive(false);
        Object.DontDestroyOnLoad(go);
        go.AddComponent<FixTransform>();
        go.AddComponent<SpawnBotAbility>();
        return go;
    }
}

[HarmonyPatch(typeof(AbilityGrid), nameof(AbilityGrid.Awake))]
internal static class RegisterBeforeAbilityGridPatch
{
    // The grid sizes itself from the list in Awake, so the entry must exist before.
    private static void Prefix(AbilityGrid __instance)
    {
        AbilityRegistration.Register(__instance.abilityIcons);
        AbilityRegistration.RegisterEverywhere();
    }
}

[HarmonyPatch(typeof(CharacterSelectHandler), nameof(CharacterSelectHandler.TryStartGame_inner))]
internal static class RegisterBeforeMatchStartPatch
{
    // Match start reads SteamManager.instance.abilityIcons; make sure it has the entry too.
    private static void Prefix()
    {
        if (SteamManager.instance != null)
            AbilityRegistration.Register(SteamManager.instance.abilityIcons);
    }
}

/// <summary>Keep the "Random" ability from rolling SpawnBot.</summary>
[HarmonyPatch]
internal static class ExcludeFromRandomAbilityPatch
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        typeof(RandomAbility).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == nameof(RandomAbility.GetRandomAbilityPrefab));

    private static void Postfix(ref NamedSprite __result, NamedSpriteList abilityIcons)
    {
        if (__result.name != AbilityRegistration.AbilityName)
            return;

        // Same range as the game (index 2..), minus our entry. Other mods may append entries after ours.
        var candidates = new List<NamedSprite>();
        for (int i = 2; i < abilityIcons.sprites.Count; i++)
        {
            if (abilityIcons.sprites[i].name != AbilityRegistration.AbilityName)
                candidates.Add(abilityIcons.sprites[i]);
        }
        __result = candidates[Updater.RandomInt(0, candidates.Count)];
    }
}
