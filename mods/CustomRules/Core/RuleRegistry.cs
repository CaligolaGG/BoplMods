using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace CustomRules.Core;

/// <summary>
/// Public API for rule mods (e.g. CaliGameModes). Add rules in your plugin's Awake, with
/// [BepInDependency(CustomRules.Plugin.Guid)] so this mod is loaded first.
/// Rules show up in the rules menu in the order they were added; the host's values are sent to every player.
/// Rules added with a <see cref="RuleSection"/> (see <see cref="AddSection"/>) are drawn in their own tinted box
/// below the mod's basic rules, so a mod's rules stand out as a group.
/// </summary>
public static class RuleRegistry
{
    private static readonly List<Rule> RuleList = new();
    private static readonly List<SyncedValue> SyncedList = new();
    private static readonly List<RuleSection> SectionList = new();

    public static IReadOnlyList<Rule> Rules => RuleList;

    internal static IReadOnlyList<SyncedValue> SyncedValues => SyncedList;

    /// <summary>Sections in the order they were added.</summary>
    public static IReadOnlyList<RuleSection> Sections => SectionList;

    /// <summary>A titled group of rules, drawn in a box tinted with <paramref name="tint"/>. Pass it to the Add methods.</summary>
    public static RuleSection AddSection(string title, Color tint)
    {
        var section = new RuleSection(title, tint);
        SectionList.Add(section);
        return section;
    }

    /// <summary>
    /// Raised on every machine when a match starts, after all active values (and synced values) were set from
    /// the host's (or, in local games, this machine's) values.
    /// </summary>
    public static event Action RulesActivated;

    public static Rule AddFloat(string key, string label, float defaultValue, float min, float max,
        string description, Rule parent = null, RuleSection section = null) =>
        Add(key, label, description, RuleType.Float, defaultValue, min, max, parent, section);

    public static Rule AddInt(string key, string label, int defaultValue, int min, int max,
        string description, Rule parent = null, RuleSection section = null) =>
        Add(key, label, description, RuleType.Int, defaultValue, min, max, parent, section);

    public static Rule AddBool(string key, string label, bool defaultValue, string description, Rule parent = null,
        RuleSection section = null) =>
        Add(key, label, description, RuleType.Bool, defaultValue, 0f, 1f, parent, section);

    /// <summary>
    /// A rule that picks one name from <paramref name="options"/> (asked each time the menu draws, so the list can come
    /// from the game, e.g. ability names). The value is the name, or "" for none (shown as <paramref name="noneLabel"/>).
    /// With <paramref name="icon"/> (name → sprite, null if none) the menu shows pictures and picks from a grid.
    /// </summary>
    public static Rule AddChoice(string key, string label, Func<IReadOnlyList<string>> options, string noneLabel,
        string description, Rule parent = null, RuleSection section = null, Func<string, Sprite> icon = null)
    {
        Rule rule = Add(key, label, description, RuleType.Choice, "", 0f, 0f, parent, section);
        rule.Options = options;
        rule.NoneLabel = noneLabel;
        rule.OptionIcon = icon;
        return rule;
    }

    /// <summary>
    /// Two bool rules that can't both be on: turning one on in the menu turns the other off. A rule can be exclusive
    /// with several others.
    /// </summary>
    public static void MakeExclusive(Rule a, Rule b)
    {
        if (a.Type != RuleType.Bool || b.Type != RuleType.Bool)
            throw new ArgumentException("Only bool rules can be exclusive.");
        if (!a.ExclusiveWith.Contains(b))
            a.ExclusiveWith.Add(b);
        if (!b.ExclusiveWith.Contains(a))
            b.ExclusiveWith.Add(a);
    }

    /// <summary>
    /// While <paramref name="rule"/> (a bool rule) is on, the rule <paramref name="key"/> is forced to
    /// <paramref name="value"/>: set when it's turned on in the menu, shown greyed out there (can't be changed), and
    /// enforced when the rules become active. E.g. rules a game mode can't work without.
    /// </summary>
    public static void AddLock(Rule rule, string key, object value)
    {
        AddPreset(rule, key, value);
        Rule target = Find(key);
        rule.Locks[target] = rule.Presets[target];
    }

    /// <summary>
    /// While <paramref name="rule"/> (a bool rule) is on, the number rule <paramref name="key"/> can't go below
    /// <paramref name="min"/>: the menu raises lower values on Apply (and shows the new range), and they're raised
    /// when the rules become active. E.g. a game mode that can't work with 0.
    /// </summary>
    public static void AddMinimum(Rule rule, string key, float min)
    {
        if (rule.Type != RuleType.Bool)
            throw new ArgumentException("Only bool rules can set minimums.");
        Rule target = Find(key) ?? throw new ArgumentException($"No rule '{key}'.");
        if (target.Type != RuleType.Float && target.Type != RuleType.Int)
            throw new ArgumentException($"Rule '{key}' isn't a number.");
        rule.Minimums[target] = min;
    }

    /// <summary>
    /// The lowest value the rules that are on (by <paramref name="text"/>) allow for <paramref name="target"/>, and
    /// the rule asking for it (null if none: the rule's own Min).
    /// </summary>
    internal static float MinimumOf(Rule target, Func<Rule, string> text, out Rule by)
    {
        float min = target.Min;
        by = null;
        foreach (Rule rule in RuleList)
        {
            if (rule.Minimums.TryGetValue(target, out float value) && value > min && IsOnBy(rule, text))
            {
                min = value;
                by = rule;
            }
        }
        return min;
    }

    /// <summary>
    /// While <paramref name="rule"/> (a bool rule) is on, the ability <paramref name="abilityName"/> (its name in the
    /// game's ability list) is disabled as if it were in the disabled abilities: nobody can pick it or get it.
    /// </summary>
    public static void AddBannedAbility(Rule rule, string abilityName)
    {
        if (rule.Type != RuleType.Bool)
            throw new ArgumentException("Only bool rules can ban abilities.");
        if (!rule.BannedAbilities.Contains(abilityName))
            rule.BannedAbilities.Add(abilityName);
    }

    /// <summary>A bool rule on by <paramref name="text"/> (one of its values), with every parent on too.</summary>
    internal static bool IsOnBy(Rule rule, Func<Rule, string> text) =>
        text(rule) == bool.TrueString && (rule.Parent == null || IsOnBy(rule.Parent, text));

    /// <summary>The rule that locks <paramref name="target"/> (on by <paramref name="text"/>), or null.</summary>
    internal static Rule LockedBy(Rule target, Func<Rule, string> text, out string value)
    {
        foreach (Rule rule in RuleList)
        {
            if (rule.Locks.TryGetValue(target, out value) && IsOnBy(rule, text))
                return rule;
        }
        value = null;
        return null;
    }

    /// <summary>Abilities banned by the rules that are on (by <paramref name="text"/>).</summary>
    internal static IEnumerable<string> BannedAbilities(Func<Rule, string> text) =>
        RuleList.Where(r => r.BannedAbilities.Count > 0 && IsOnBy(r, text)).SelectMany(r => r.BannedAbilities);

    /// <summary>After the active values are set: locked rules get their locked value.</summary>
    internal static void ApplyLocks()
    {
        foreach (Rule rule in RuleList.Where(r => r.Locks.Count > 0 && r.IsOn).ToList())
        {
            foreach (KeyValuePair<Rule, string> locked in rule.Locks)
                locked.Key.SetActive(locked.Value);
            foreach (KeyValuePair<Rule, float> minimum in rule.Minimums)
            {
                if (minimum.Key.ActiveFloat < minimum.Value)
                    minimum.Key.SetActive(Rule.Format(minimum.Key.NumberValue(minimum.Value)));
            }
        }
    }

    /// <summary>
    /// When <paramref name="rule"/> (a bool rule) is turned on in the menu, the rule <paramref name="key"/> is set to
    /// <paramref name="value"/> too (still pending until Apply, and the host can change it afterwards). E.g. a game mode
    /// that usually goes with other rules.
    /// </summary>
    public static void AddPreset(Rule rule, string key, object value)
    {
        if (rule.Type != RuleType.Bool)
            throw new ArgumentException("Only bool rules can have presets.");
        Rule target = Find(key) ?? throw new ArgumentException($"No rule '{key}'.");
        if (!target.TryParse(Rule.Format(value), out object parsed))
            throw new ArgumentException($"Invalid value '{value}' for rule '{key}'.");
        rule.Presets[target] = Rule.Format(parsed);
    }

    /// <summary>
    /// A value that isn't shown in the menu but is sent from the host with the rules, e.g. a skill's tuning so every
    /// player uses the host's. <paramref name="readLocal"/> gives this machine's value as text (invariant culture);
    /// <paramref name="applyActive"/> is called with the host's value at match start (also on the host itself).
    /// </summary>
    public static void AddSyncedValue(string key, Func<string> readLocal, Action<string> applyActive)
    {
        if (SyncedList.Any(s => s.Key == key) || RuleList.Any(r => r.Key == key))
            throw new ArgumentException($"Duplicate rule key '{key}'.");
        SyncedList.Add(new SyncedValue(key, readLocal, applyActive));
    }

    public static Rule Find(string key) => RuleList.FirstOrDefault(r => r.Key == key);

    /// <summary>
    /// A rule with no row in the Rules tab (see <see cref="Rule.Hidden"/>), saved in <paramref name="configSection"/>.
    /// Numbers are clamped to [<paramref name="min"/>, <paramref name="max"/>] (by default only to ±1e9).
    /// </summary>
    internal static Rule AddHidden(string key, RuleType type, object defaultValue, string description, string configSection,
        float min = -1e9f, float max = 1e9f)
    {
        if (Find(key) != null || SyncedList.Any(s => s.Key == key))
            throw new ArgumentException($"Duplicate rule key '{key}'.");

        ConfigEntryBase entry = type switch
        {
            RuleType.Float => Plugin.RulesConfig.Bind(configSection, key, (float)defaultValue, description),
            RuleType.Int => Plugin.RulesConfig.Bind(configSection, key, (int)defaultValue, description),
            RuleType.Bool => Plugin.RulesConfig.Bind(configSection, key, (bool)defaultValue, description),
            _ => Plugin.RulesConfig.Bind(configSection, key, (string)defaultValue, description),
        };

        var rule = new Rule(key, key, description, type, defaultValue, min, max, null, null, entry) { Hidden = true };
        RuleList.Add(rule);
        return rule;
    }

    private static Rule Add(string key, string label, string description, RuleType type, object defaultValue,
        float min, float max, Rule parent, RuleSection section)
    {
        if (Find(key) != null || SyncedList.Any(s => s.Key == key))
            throw new ArgumentException($"Duplicate rule key '{key}'.");
        if (parent != null && parent.Type != RuleType.Bool)
            throw new ArgumentException($"Parent of rule '{key}' must be a bool rule.");

        ConfigEntryBase entry = type switch
        {
            RuleType.Float => Plugin.RulesConfig.Bind("Rules", key, (float)defaultValue,
                new ConfigDescription(description, new AcceptableValueRange<float>(min, max))),
            RuleType.Int => Plugin.RulesConfig.Bind("Rules", key, (int)defaultValue,
                new ConfigDescription(description, new AcceptableValueRange<int>((int)min, (int)max))),
            RuleType.Choice => Plugin.RulesConfig.Bind("Rules", key, (string)defaultValue, description),
            _ => Plugin.RulesConfig.Bind("Rules", key, (bool)defaultValue, description),
        };

        // A sub rule always sits in its parent's section.
        var rule = new Rule(key, label, description, type, defaultValue, min, max, parent, parent?.Section ?? section, entry);
        RuleList.Add(rule);
        return rule;
    }

    internal static void RaiseRulesActivated()
    {
        try
        {
            RulesActivated?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"RulesActivated handler failed: {e}");
        }
    }
}

internal sealed class SyncedValue
{
    public SyncedValue(string key, Func<string> readLocal, Action<string> applyActive)
    {
        Key = key;
        ReadLocal = readLocal;
        ApplyActive = applyActive;
    }

    public string Key { get; }

    public Func<string> ReadLocal { get; }

    public Action<string> ApplyActive { get; }
}

/// <summary>A titled, tinted group of rules in the rules menu. Create with <see cref="RuleRegistry.AddSection"/>.</summary>
public sealed class RuleSection
{
    internal RuleSection(string title, Color tint)
    {
        Title = title;
        Tint = tint;
    }

    public string Title { get; }

    public Color Tint { get; }
}
