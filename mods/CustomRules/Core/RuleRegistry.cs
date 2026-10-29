using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace CustomRules.Core;

/// <summary>
/// Public API for rule mods (e.g. CaliGameModes). Add rules in your plugin's Awake, with
/// [BepInDependency(CustomRules.Plugin.Guid)] so this mod is loaded first.
/// Rules show up in the rules menu in the order they were added; the host's values are sent to every player.
/// </summary>
public static class RuleRegistry
{
    private static readonly List<Rule> RuleList = new();
    private static readonly List<SyncedValue> SyncedList = new();

    public static IReadOnlyList<Rule> Rules => RuleList;

    internal static IReadOnlyList<SyncedValue> SyncedValues => SyncedList;

    /// <summary>
    /// Raised on every machine when a match starts, after all active values (and synced values) were set from
    /// the host's (or, in local games, this machine's) values.
    /// </summary>
    public static event Action RulesActivated;

    public static Rule AddFloat(string key, string label, float defaultValue, float min, float max,
        string description, Rule parent = null) =>
        Add(key, label, description, RuleType.Float, defaultValue, min, max, parent);

    public static Rule AddInt(string key, string label, int defaultValue, int min, int max,
        string description, Rule parent = null) =>
        Add(key, label, description, RuleType.Int, defaultValue, min, max, parent);

    public static Rule AddBool(string key, string label, bool defaultValue, string description, Rule parent = null) =>
        Add(key, label, description, RuleType.Bool, defaultValue, 0f, 1f, parent);

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

    private static Rule Add(string key, string label, string description, RuleType type, object defaultValue,
        float min, float max, Rule parent)
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
            _ => Plugin.RulesConfig.Bind("Rules", key, (bool)defaultValue, description),
        };

        var rule = new Rule(key, label, description, type, defaultValue, min, max, parent, entry);
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
