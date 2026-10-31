using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CustomRules.Core;

/// <summary>
/// Public API for ability mods (e.g. CaliSkills): an ability's tuning values, edited by the host in the rules menu's
/// abilities window (right-click the ability) and used by every player in the match.
///
/// Register in your plugin's Awake (with [BepInDependency(CustomRules.Plugin.Guid)], soft or hard, so this mod loads
/// first). Every public static float/int/bool field of the settings class becomes a stat, with its [Description] as
/// hint and its value at registration as default. Fields with the same [Category] (next to each other) are shown
/// together in one frame, under the category's name. Each stat is a hidden rule ("Stat.&lt;ability&gt;.&lt;field&gt;" in the
/// [AbilityStats] section of the config), so it's saved, sent by the host and kept in profiles like the other rules.
/// At match start, on every machine, the fields are set to the host's values and <c>onChanged</c> is called (re-apply
/// values baked into prefabs there).
/// </summary>
public static class AbilityStats
{
    private const string ConfigSection = "AbilityStats";

    private static readonly List<AbilityStatSet> Sets = new();
    private static readonly List<StatsButton> ButtonList = new();

    internal static IReadOnlyList<StatsButton> Buttons => ButtonList;

    /// <param name="abilityName">The ability's name in the game's ability list (what the ability grid shows).</param>
    /// <param name="settingsType">Static class whose public static float/int/bool fields are the stats.</param>
    /// <param name="onChanged">Called after the fields were set at match start. Optional.</param>
    public static void Register(string abilityName, Type settingsType, Action onChanged = null)
    {
        if (string.IsNullOrEmpty(abilityName) || settingsType == null)
            throw new ArgumentException("An ability name and a settings type are needed.");
        if (Find(abilityName) != null)
            throw new ArgumentException($"Stats for '{abilityName}' are already registered.");

        var set = new AbilityStatSet(abilityName, onChanged);
        foreach (FieldInfo field in settingsType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsInitOnly || field.IsLiteral)
                continue;
            RuleType? type = field.FieldType == typeof(float) ? RuleType.Float
                : field.FieldType == typeof(int) ? RuleType.Int
                : field.FieldType == typeof(bool) ? RuleType.Bool
                : null;
            if (type == null)
                continue;

            string description = field.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
            Rule rule = RuleRegistry.AddHidden($"Stat.{Id(abilityName)}.{field.Name}", type.Value, field.GetValue(null),
                $"{abilityName}: {description}", ConfigSection);
            string group = field.GetCustomAttribute<CategoryAttribute>()?.Category;
            set.Stats.Add(new AbilityStat(field.Name, field, rule, description, group));
        }
        Add(set);
    }

    /// <summary>
    /// Stats that are multipliers (default 1, at least 0) read by the caller itself (<see cref="AbilityStat.Rule"/>'s
    /// active value), not fields: e.g. the vanilla abilities' stats. <paramref name="stats"/> = (key, label, hint).
    /// </summary>
    internal static AbilityStatSet RegisterMultipliers(string abilityName, IEnumerable<(string Key, string Label, string Description)> stats)
    {
        if (Find(abilityName) != null)
            throw new ArgumentException($"Stats for '{abilityName}' are already registered.");
        var set = new AbilityStatSet(abilityName, null);
        foreach ((string key, string label, string description) in stats)
        {
            Rule rule = RuleRegistry.AddHidden($"Stat.{Id(abilityName)}.{key}", RuleType.Float, 1f,
                $"{abilityName}: {description}", ConfigSection, 0f, 100f);
            set.Stats.Add(new AbilityStat(label, null, rule, description));
        }
        Add(set);
        return set;
    }

    /// <summary>
    /// A button in the rules menu, under the bool rule <paramref name="parent"/> (while it's on), that opens the stats
    /// window of <paramref name="statsName"/>: a set registered with <see cref="Register"/>, which doesn't have to be an
    /// ability (e.g. a game mode's ball). Its stats work like any ability's: host-set, saved, synced.
    /// </summary>
    public static void AddMenuButton(Rule parent, string label, string statsName, string description)
    {
        if (parent == null || parent.Type != RuleType.Bool)
            throw new ArgumentException("A stats button needs a bool parent rule.");
        ButtonList.Add(new StatsButton(parent, label, statsName, description));
    }

    private static void Add(AbilityStatSet set)
    {
        if (Sets.Count == 0)
            RuleRegistry.RulesActivated += ApplyActive;
        Sets.Add(set);
    }

    internal static AbilityStatSet Find(string abilityName) => Sets.FirstOrDefault(s => s.Ability == abilityName);

    /// <summary>
    /// A stat's value as set in the lobby right now (the host's for online guests, this machine's otherwise), as text
    /// (invariant culture; bools "True"/"False"). For lobby-side decisions before the match makes the values active,
    /// e.g. whether an ability currently counts as offensive. <paramref name="statName"/> = the settings field's name.
    /// </summary>
    public static bool TryGetLobbyValue(string abilityName, string statName, out string value)
    {
        AbilityStat stat = Find(abilityName)?.Stats.FirstOrDefault(s => s.Name == statName);
        value = stat != null ? RulesSync.LobbyText(stat.Rule) : null;
        return stat != null;
    }

    /// <summary>Whether a stat of the ability differs from its default, as set in the lobby (the host's for guests).</summary>
    internal static bool IsModifiedInLobby(string abilityName)
    {
        AbilityStatSet set = Find(abilityName);
        return set != null && set.Stats.Any(s => RulesSync.LobbyText(s.Rule) != s.Rule.DefaultText);
    }

    /// <summary>Config keys can't hold some characters: keep letters and digits only.</summary>
    private static string Id(string name)
    {
        var id = new StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
                id.Append(c);
        }
        return id.ToString();
    }

    private static void ApplyActive()
    {
        foreach (AbilityStatSet set in Sets)
        {
            try
            {
                foreach (AbilityStat stat in set.Stats)
                {
                    if (stat.Field == null)
                        continue;
                    Rule rule = stat.Rule;
                    object value = rule.Type switch
                    {
                        RuleType.Int => rule.ActiveInt,
                        RuleType.Bool => rule.ActiveBool,
                        _ => rule.ActiveFloat,
                    };
                    stat.Field.SetValue(null, value);
                }
                set.OnChanged?.Invoke();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Applying the stats of '{set.Ability}' failed: {e}");
            }
        }
    }
}

internal sealed class AbilityStatSet
{
    public AbilityStatSet(string ability, Action onChanged)
    {
        Ability = ability;
        OnChanged = onChanged;
    }

    public string Ability { get; }

    public Action OnChanged { get; }

    public List<AbilityStat> Stats { get; } = new();
}

internal sealed class AbilityStat
{
    public AbilityStat(string name, FieldInfo field, Rule rule, string description, string group = null)
    {
        Name = name;
        Field = field;
        Rule = rule;
        Description = description;
        Group = group;
    }

    /// <summary>Shown in the stats window.</summary>
    public string Name { get; }

    /// <summary>The settings field it's written to at match start; null for multipliers read by the caller.</summary>
    public FieldInfo Field { get; }

    public Rule Rule { get; }

    public string Description { get; }

    /// <summary>The field's [Category]: stats of the same group next to each other share a frame. Null if none.</summary>
    public string Group { get; }
}

internal sealed class StatsButton
{
    public StatsButton(Rule parent, string label, string statsName, string description)
    {
        Parent = parent;
        Label = label;
        StatsName = statsName;
        Description = description;
    }

    public Rule Parent { get; }

    public string Label { get; }

    public string StatsName { get; }

    public string Description { get; }
}
