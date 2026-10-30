using System.Collections.Generic;
using System.Linq;
using CustomRules.Core;
using HarmonyLib;
using UnityEngine;

namespace CustomRules.Rules;

/// <summary>
/// Rules about which abilities players get:
/// - "No offensive abilities" / "Only offensive abilities" (exclusive): abilities not allowed can't be picked in
///   character select (<see cref="AbilityFilterLobby"/>), and Random (also ability pickups) only draws allowed ones.
/// - "Preselect abilities" section: a chosen ability per slot is forced onto every player (wins over the filters).
/// Abilities are looked up by name in the game's ability list (the "abilityIcons" NamedSpriteList), whose entries say
/// whether they're offensive (CaliSkills: Omae wa mo shindeiru is, Pinball isn't). Entry 0 is the empty slot, entry 1
/// Random.
/// </summary>
internal static class AbilityRules
{
    private const int FirstRealAbility = 2;
    private static readonly string[] NeverRandom = { "SpawnBot" };

    internal static Rule NoOffensive;
    internal static Rule OnlyOffensive;
    internal static readonly Rule[] Preselect = new Rule[3];

    internal static void Register()
    {
        NoOffensive = RuleRegistry.AddBool("NoOffensiveAbilities", "No offensive abilities", false,
            "Offensive abilities can't be picked (greyed out), and Random only gives non-offensive ones.");
        OnlyOffensive = RuleRegistry.AddBool("OnlyOffensiveAbilities", "Only offensive abilities", false,
            "Non-offensive abilities can't be picked (greyed out), and Random only gives offensive ones.");
        RuleRegistry.MakeExclusive(NoOffensive, OnlyOffensive);

        RuleSection section = RuleRegistry.AddSection("Preselect abilities", new Color(1f, 0.75f, 0.4f));
        for (int i = 0; i < Preselect.Length; i++)
        {
            Preselect[i] = RuleRegistry.AddChoice($"PreselectAbility{i + 1}", $"Ability {i + 1}", AbilityNames,
                "Player's choice",
                $"Forces this ability in slot {i + 1} for every player (if the match has that many abilities).",
                section: section, icon: AbilityIcon);
        }
    }

    private static NamedSpriteList AbilityList => SteamManager.instance != null ? SteamManager.instance.abilityIcons : null;

    /// <summary>Random + every real ability, for the preselect pickers.</summary>
    private static IReadOnlyList<string> AbilityNames()
    {
        NamedSpriteList list = AbilityList;
        if (list == null)
            return new string[0];
        return list.sprites.Skip(1).Select(s => s.name).Where(n => !NeverRandom.Contains(n)).ToList();
    }

    private static Sprite AbilityIcon(string name)
    {
        NamedSpriteList list = AbilityList;
        if (list == null || string.IsNullOrEmpty(name))
            return null;
        int index = list.sprites.FindIndex(s => s.name == name);
        return index >= 0 ? list.sprites[index].sprite : null;
    }

    private static bool IsRandom(NamedSprite entry) =>
        entry.associatedGameObject != null && entry.associatedGameObject.GetComponent<RandomAbility>() != null;

    /// <summary>
    /// Whether the offensive filter allows this ability. Random is always allowed (its draws are filtered); so is the
    /// empty slot.
    /// </summary>
    internal static bool Allowed(NamedSprite entry, bool noOffensive, bool onlyOffensive)
    {
        if (entry.associatedGameObject == null || IsRandom(entry))
            return true;
        if (noOffensive)
            return !entry.isOffensiveAbility;
        if (onlyOffensive)
            return entry.isOffensiveAbility;
        return true;
    }

    /// <summary>In the match: the active (host's) values.</summary>
    private static bool Allowed(NamedSprite entry) => Allowed(entry, NoOffensive.ActiveBool, OnlyOffensive.ActiveBool);

    private static bool FilterActive => NoOffensive.ActiveBool || OnlyOffensive.ActiveBool;

    // In character select: the rules as set in the lobby (the host's for online guests), not yet active.
    internal static bool LobbyNoOffensive => RulesSync.LobbyBool(NoOffensive);
    internal static bool LobbyOnlyOffensive => RulesSync.LobbyBool(OnlyOffensive);
    internal static bool LobbyFilterActive => LobbyNoOffensive || LobbyOnlyOffensive;

    /// <summary>
    /// Every round, before the slimes and their abilities are created (on every machine, same result): preselected
    /// abilities replace the players' own. (The offensive filters act in character select instead: disallowed abilities
    /// can't be picked, see <see cref="AbilityFilterLobby"/>.)
    /// </summary>
    [HarmonyPatch(typeof(GameSessionHandler), nameof(GameSessionHandler.SpawnPlayers))]
    private static class ApplyOnRoundStart
    {
        private static void Prefix()
        {
            NamedSpriteList list = AbilityList;
            if (list == null)
                return;

            foreach (Player player in PlayerHandler.Get().PlayerList())
            {
                if (player.Abilities == null || player.AbilityIcons == null)
                    continue;

                for (int slot = 0; slot < player.Abilities.Count && slot < player.AbilityIcons.Count; slot++)
                {
                    string forced = slot < Preselect.Length ? Preselect[slot].ActiveText : "";
                    if (string.IsNullOrEmpty(forced))
                        continue;
                    int index = list.sprites.FindIndex(s => s.name == forced);
                    if (index < 1)
                        continue;
                    player.Abilities[slot] = list.sprites[index].associatedGameObject;
                    player.AbilityIcons[slot] = list.sprites[index].sprite;
                }
            }
        }
    }

    /// <summary>
    /// Random's draws (at round start and for ability pickups): if the drawn ability isn't allowed, draw again among the
    /// allowed ones with the game's seeded RNG, so every machine gets the same.
    /// </summary>
    [HarmonyPatch]
    private static class FilterRandomDraws
    {
        private static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
            AccessTools.GetDeclaredMethods(typeof(RandomAbility))
                .Where(m => m.Name == nameof(RandomAbility.GetRandomAbilityPrefab));

        private static void Postfix(ref NamedSprite __result, NamedSpriteList abilityIcons, NamedSpriteList abilityIcons_demo)
        {
            if (!FilterActive || (Allowed(__result) && !IsRandom(__result)))
                return;

            NamedSpriteList list = SteamManager.instance != null && SteamManager.instance.dlc.HasDLC() ? abilityIcons : abilityIcons_demo;
            if (list == null)
                return;
            List<NamedSprite> candidates = list.sprites
                .Skip(FirstRealAbility)
                .Where(s => s.associatedGameObject != null && !IsRandom(s) && !NeverRandom.Contains(s.name) && Allowed(s))
                .ToList();
            if (candidates.Count > 0)
                __result = candidates[Updater.RandomInt(0, candidates.Count)];
        }
    }
}
