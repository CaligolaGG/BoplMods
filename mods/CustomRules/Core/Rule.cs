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

    /// <summary>A bool rule that is turned off when this one is turned on in the menu (and the other way round).</summary>
    internal Rule ExclusiveWith { get; set; }

    /// <summary>
    /// Not drawn as a row of the Rules tab (edited elsewhere, e.g. the abilities window), but saved, synced and kept in
    /// profiles like any rule.
    /// </summary>
    internal bool Hidden { get; set; }

    public string ActiveText { get; private set; }

    public float ActiveFloat { get; private set; }

    public int ActiveInt => (int)ActiveFloat;

    public bool ActiveBool { get; private set; }

    public Fix ActiveFix { get; private set; }

    /// <summary>True if the rule is active and its parent (if any) is on too.</summary>
    public bool IsOn => ActiveBool && (Parent == null || Parent.IsOn);

    internal string LocalText => Format(Entry.BoxedValue);

    internal string DefaultText => Format(DefaultValue);

    internal void SetLocal(object value) => Entry.BoxedValue = value;

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
    }

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

    internal static string Format(object value) => value switch
    {
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? "",
    };
}
