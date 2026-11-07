using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using BoplFixedMath;

namespace CustomRules.Core;

public enum RuleType
{
    Float,
    Int,
    Bool,

    /// <summary>One of a list of names (e.g. an ability); "" = none of them (shown as the rule's NoneLabel).</summary>
    Choice,
}

/// <summary>
/// One match rule shown in the rules menu. Two values:
/// - the local value: what this player set in the menu, saved in BepInEx/config/com.caligola.customrules.cfg.
///   Only the host's (or, in local games, this machine's) local values are used;
/// - the active value: what is in effect in the current match, the same on every machine.
///   Gameplay code reads only the active value (<see cref="ActiveFloat"/>, <see cref="ActiveFix"/>...).
/// Create rules with <see cref="RuleRegistry"/>.
/// </summary>
public sealed class Rule
{
    internal Rule(string key, string label, string description, RuleType type, object defaultValue,
        float min, float max, Rule parent, RuleSection section, ConfigEntryBase entry)
    {
        Key = key;
        Label = label;
        Description = description;
        Type = type;
        DefaultValue = defaultValue;
        Min = min;
        Max = max;
        Parent = parent;
        Section = section;
        Entry = entry;
        SetActive(Format(defaultValue));
    }

    /// <summary>Unique id, also used in the network packet and the config file.</summary>
    public string Key { get; }

    public string Label { get; }

    public string Description { get; }

    public RuleType Type { get; }

    public object DefaultValue { get; }

    public float Min { get; }

    public float Max { get; }

    /// <summary>A bool rule this one belongs to: it's shown under it, and only while the parent is on.</summary>
    public Rule Parent { get; }

    /// <summary>The menu group it's drawn in; null = the basic rules at the top.</summary>
    public RuleSection Section { get; }

    internal ConfigEntryBase Entry { get; }

    /// <summary>Choice rules: the names to pick from (asked when the menu draws, so they can come from the game).</summary>
    internal Func<IReadOnlyList<string>> Options { get; set; }

    /// <summary>Choice rules, optional: the picture of an option (shown in the menu like the game's ability picker).</summary>
    internal Func<string, UnityEngine.Sprite> OptionIcon { get; set; }

    /// <summary>Choice rules: how the "" value (no choice) is shown.</summary>
    internal string NoneLabel { get; set; }

    /// <summary>Bool rules that are turned off when this one is turned on in the menu (and the other way round).</summary>
    internal List<Rule> ExclusiveWith { get; } = new();

    /// <summary>
    /// Not drawn as a row of the Rules tab (edited elsewhere, e.g. the abilities window), but saved, synced and kept in
    /// profiles like any rule.
    /// </summary>
    internal bool Hidden { get; set; }

    /// <summary>A sub rule drawn under its parent even while the parent is off (it applies either way).</summary>
    internal bool ShownWhenParentOff { get; set; }

    /// <summary>Bool rules: other rules' values set in the menu when this one is turned on (<see cref="RuleRegistry.AddPreset"/>).</summary>
    internal Dictionary<Rule, string> Presets { get; } = new();

    /// <summary>
    /// Bool rules: other rules forced to a value while this one is on (<see cref="RuleRegistry.AddLock"/>): greyed out
    /// in the menu and overridden when the rules become active.
    /// </summary>
    internal Dictionary<Rule, string> Locks { get; } = new();

    /// <summary>
    /// Bool rules: lowest values of other (number) rules while this one is on (<see cref="RuleRegistry.AddMinimum"/>).
    /// </summary>
    internal Dictionary<Rule, float> Minimums { get; } = new();

    /// <summary>Bool rules: abilities nobody can pick or get while this one is on (<see cref="RuleRegistry.AddBannedAbility"/>).</summary>
    internal List<string> BannedAbilities { get; } = new();

    public string ActiveText { get; private set; }

    public float ActiveFloat { get; private set; }

    public int ActiveInt => (int)ActiveFloat;

    public bool ActiveBool { get; private set; }

    public Fix ActiveFix { get; private set; }

    /// <summary>True if the rule is active and its parent (if any) is on too.</summary>
    public bool IsOn => ActiveBool && (Parent == null || Parent.IsOn);

    internal string LocalText => Format(Entry.BoxedValue);

    /// <summary>
    /// The value as set in the lobby right now, before a match makes it active: the host's for online guests, this
    /// machine's otherwise. For lobby-side decisions (e.g. which maps to offer); gameplay reads the active value.
    /// </summary>
    public string LobbyText => RulesSync.LobbyText(this);

    public bool LobbyBool => LobbyText == bool.TrueString;

    internal string DefaultText => Format(DefaultValue);

    internal void SetLocal(object value)
    {
        Entry.BoxedValue = value;
        LocalVersion++;
    }

    /// <summary>Bumped whenever a local value is set through <see cref="SetLocal"/>.</summary>
    internal static int LocalVersion { get; private set; }

    /// <summary>Sets the active value from its text (from this machine or the host). Invalid text = default.</summary>
    internal void SetActive(string text)
    {
        if (!TryParse(text, out object value))
            value = DefaultValue;

        ActiveText = Format(value);
        ActiveBool = value is bool b && b;
        ActiveFloat = value switch
        {
            float f => f,
            int n => n,
            bool b2 => b2 ? 1f : 0f,
            _ => 0f,
        };
        ActiveFix = (Fix)ActiveFloat;
        ActiveVersion++;
    }

    /// <summary>Bumped whenever any rule's active value is set: lets hot code cache what it derives from them.</summary>
    internal static int ActiveVersion { get; private set; }

    /// <summary>Parses and clamps to [Min, Max].</summary>
    internal bool TryParse(string text, out object value)
    {
        if (Type != RuleType.Choice)
            text = (text ?? "").Trim().Replace(',', '.');
        value = null;
        switch (Type)
        {
            case RuleType.Float when float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)
                                     && !float.IsNaN(f) && !float.IsInfinity(f):
                value = Math.Max(Min, Math.Min(Max, f));
                break;
            case RuleType.Int when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n):
                value = (int)Math.Max(Min, Math.Min(Max, n));
                break;
            case RuleType.Bool when bool.TryParse(text, out bool b):
                value = b;
                break;
            case RuleType.Choice:
                // Any name is kept: an unknown one (e.g. from a mod this player doesn't have) is ignored where used.
                value = (text ?? "").Trim();
                break;
        }
        return value != null;
    }

    /// <summary>A number as this (number) rule's value type: rounded up for Int rules.</summary>
    internal object NumberValue(float value) => Type == RuleType.Int ? (int)Math.Ceiling(value) : value;

    internal static string Format(object value) => value switch
    {
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? "",
    };
}
