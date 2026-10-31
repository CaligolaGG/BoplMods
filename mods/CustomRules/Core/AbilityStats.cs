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
/// hint and its value at registration as default. Each stat is a hidden rule ("Stat.&lt;ability&gt;.&lt;field&gt;" in the
/// [AbilityStats] section of the config), so it's saved, sent by the host and kept in profiles like the other rules.
/// At match start, on every machine, the fields are set to the host's values and <c>onChanged</c> is called (re-apply
/// values baked into prefabs there).
/// </summary>
public static class AbilityStats
{
    private const string ConfigSection = "AbilityStats";

    private static readonly List<AbilityStatSet> Sets = new();

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
            set.Stats.Add(new AbilityStat(field, rule, description));
        }

        if (Sets.Count == 0)
            RuleRegistry.RulesActivated += ApplyActive;
        Sets.Add(set);
    }

    internal static AbilityStatSet Find(string abilityName) => Sets.FirstOrDefault(s => s.Ability == abilityName);

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
    public AbilityStat(FieldInfo field, Rule rule, string description)
    {
        Field = field;
        Rule = rule;
        Description = description;
    }

    public FieldInfo Field { get; }

    public Rule Rule { get; }

    public string Description { get; }
}
