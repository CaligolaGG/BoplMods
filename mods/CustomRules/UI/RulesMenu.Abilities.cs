using System.Collections.Generic;
using System.Linq;
using CustomRules.Core;
using CustomRules.Rules;
using UnityEngine;

namespace CustomRules.UI;

/// <summary>
/// The "Abilities" row of the Rules tab opens a grid of every ability (like the preselect picker):
/// - right-click (or click) an ability to edit its stats (<see cref="AbilityStats"/>; abilities without registered
///   stats, e.g. vanilla ones, can't be edited);
/// - middle-click to disable / re-enable it (<see cref="AbilityRules.Disabled"/>).
/// Changed abilities get a yellow border, disabled ones a red "no" sign (in the ability grid: a yellow ring and the
/// same sign, <see cref="AbilityBadges"/>).
/// Like every rule, edits are pending until Apply. Online guests see the host's values, read-only.
/// </summary>
internal sealed partial class RulesMenu
{
    private const int AbilitiesWindowId = 0x0C5715;
    private const int StatsWindowId = 0x0C5716;
    private const float StatsWindowWidth = 620f;
    private static readonly Color ChangedColor = new(1f, 0.85f, 0.2f);

    private bool _abilitiesOpen;
    private string _editingStats;
    private Vector2 _abilitiesScroll;
    private Vector2 _statsScroll;

    private void ResetAbilitiesWindow()
    {
        _abilitiesOpen = false;
        _editingStats = null;
    }

    /// <summary>Esc: closes the stats window, else the abilities window. False if neither was open.</summary>
    private bool CloseTopAbilitiesWindow()
    {
        if (_editingStats != null)
            _editingStats = null;
        else if (_abilitiesOpen)
            _abilitiesOpen = false;
        else
            return false;
        _message = null;
        return true;
    }

    private HashSet<string> PendingDisabled => AbilityRules.ParseNames(_texts[AbilityRules.Disabled]);

    private bool IsPendingChange(Rule rule) =>
        !rule.TryParse(_texts[rule], out object value) || Rule.Format(value) != rule.DefaultText;

    private bool PendingModified(string ability)
    {
        AbilityStatSet set = AbilityStats.Find(ability);
        return set != null && set.Stats.Any(s => IsPendingChange(s.Rule));
    }

    /// <summary>The row in the Rules tab, framed like a rule.</summary>
    private void DrawAbilitiesField()
    {
        IReadOnlyList<string> names = AbilityRules.AbilityNames();
        int changed = names.Count(PendingModified);
        int disabled = PendingDisabled.Count;

        GUILayout.BeginVertical(RuleFrameStyle);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Abilities", GUILayout.Width(200f));
        if (GUILayout.Button(_readOnly ? "View abilities" : "Edit abilities", GUILayout.Width(130f)))
        {
            _abilitiesOpen = true;
            _abilitiesScroll = Vector2.zero;
            _message = null;
        }
        GUILayout.Label(changed == 0 && disabled == 0 ? "all as in the game" : $"{changed} changed, {disabled} disabled");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Space(12f);
        GUILayout.Label("Right-click an ability to change its stats, middle-click to disable it (also in the ability grid). " +
                        "Changed abilities get a yellow border, disabled ones a red sign.");
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        GUILayout.Space(RuleGap);
    }

    /// <returns>True if an abilities window was drawn (in place of the rules window).</returns>
    private bool DrawAbilitiesWindows(float screenWidth, float screenHeight)
    {
        if (_editingStats != null)
        {
            int count = AbilityStats.Find(_editingStats)?.Stats.Count ?? 0;
            float height = Mathf.Min(screenHeight - 40f, 150f + count * 50f);
            var rect = new Rect((screenWidth - StatsWindowWidth) / 2f, (screenHeight - height) / 2f, StatsWindowWidth, height);
            GUI.ModalWindow(StatsWindowId, rect, DrawStatsWindow, _readOnly ? $"{_editingStats} stats (set by the host)" : $"{_editingStats} stats");
            return true;
        }
        if (_abilitiesOpen)
        {
            float width = PickerColumns * (PickerCellSize + 6f) + 40f;
            float height = Mathf.Min(screenHeight - 40f, 560f);
            var rect = new Rect((screenWidth - width) / 2f, (screenHeight - height) / 2f, width, height);
            GUI.ModalWindow(AbilitiesWindowId, rect, DrawAbilitiesWindow, _readOnly ? "Abilities (set by the host)" : "Abilities");
            return true;
        }
        return false;
    }

    private void DrawAbilitiesWindow(int id)
    {
        _cellLabelStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true, fontSize = 11 };

        GUILayout.Label(_readOnly
            ? "Set by the host. Right-click an ability to see its stats."
            : "Right-click: change the ability's stats. Middle-click: disable / re-enable it. Applies to every player.");

        IReadOnlyList<string> names = AbilityRules.AbilityNames();
        HashSet<string> disabled = PendingDisabled;
        _abilitiesScroll = GUILayout.BeginScrollView(_abilitiesScroll);
        for (int i = 0; i < names.Count; i += PickerColumns)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < i + PickerColumns && j < names.Count; j++)
                DrawAbilityCell(names[j], disabled.Contains(names[j]));
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        if (!string.IsNullOrEmpty(_message))
            GUILayout.Label(_message);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (!_readOnly && GUILayout.Button("Apply", GUILayout.Width(120f)))
            Apply();
        if (GUILayout.Button("Back (Esc)", GUILayout.Width(120f)))
            CloseTopAbilitiesWindow();
        GUILayout.EndHorizontal();
    }

    private void DrawAbilityCell(string name, bool disabled)
    {
        bool editable = AbilityStats.Find(name) != null;
        GUILayout.BeginVertical(GUILayout.Width(PickerCellSize));

        float height = PickerCellSize - 20f;
        Rect rect = GUILayoutUtility.GetRect(PickerCellSize, height, GUILayout.Width(PickerCellSize), GUILayout.Height(height));
        Event e = Event.current;
        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            if (e.button == 2)
                ToggleDisabled(name);
            else
                OpenStats(name, editable);
            e.Use();
        }

        GUI.Box(rect, GUIContent.none, GUI.skin.button);
        DrawSprite(rect, AbilityRules.AbilityIcon(name));
        if (PendingModified(name))
            DrawBorder(rect, AbilityBadges.Yellow, 3f);
        if (disabled)
        {
            float side = Mathf.Min(rect.width, rect.height) - 14f;
            var square = new Rect(rect.center.x - side / 2f, rect.center.y - side / 2f, side, side);
            GUI.DrawTexture(square, AbilityBadges.NoTexture, ScaleMode.ScaleToFit);
        }

        // Abilities without stats to edit have a dimmer name.
        Color oldContent = GUI.contentColor;
        if (!editable)
            GUI.contentColor = new Color(1f, 1f, 1f, 0.55f);
        GUILayout.Label(name, _cellLabelStyle, GUILayout.Width(PickerCellSize));
        GUI.contentColor = oldContent;

        GUILayout.EndVertical();
    }

    /// <summary>A plain border of <paramref name="thickness"/> just inside <paramref name="rect"/>.</summary>
    private static void DrawBorder(Rect rect, Color color, float thickness)
    {
        Color oldColor = GUI.color;
        GUI.color = color;
        Texture2D white = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), white);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), white);
        GUI.DrawTexture(new Rect(rect.x, rect.y + thickness, thickness, rect.height - 2f * thickness), white);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y + thickness, thickness, rect.height - 2f * thickness), white);
        GUI.color = oldColor;
    }

    private void ToggleDisabled(string name)
    {
        if (_readOnly)
        {
            _message = "Only the lobby host can disable abilities.";
            return;
        }
        _texts[AbilityRules.Disabled] = AbilityRules.ToggleName(_texts[AbilityRules.Disabled], name);
        _message = PendingDisabled.Contains(name)
            ? $"{name} disabled (press Apply)."
            : $"{name} enabled again (press Apply).";
    }

    private void OpenStats(string name, bool editable)
    {
        if (!editable)
        {
            _message = $"{name} has no stats to change (only abilities from mods that offer them, e.g. CaliSkills).";
            return;
        }
        _editingStats = name;
        _statsScroll = Vector2.zero;
        _message = null;
    }

    private void DrawStatsWindow(int id)
    {
        AbilityStatSet set = AbilityStats.Find(_editingStats);
        if (set == null)
        {
            _editingStats = null;
            return;
        }

        GUILayout.Label(_readOnly
            ? "The host's values for this match."
            : "Used by every player from the next match. Press Apply to save.");

        _statsScroll = GUILayout.BeginScrollView(_statsScroll);
        foreach (AbilityStat stat in set.Stats)
        {
            Rule rule = stat.Rule;
            GUILayout.BeginHorizontal();
            GUILayout.Label(stat.Field.Name, GUILayout.Width(200f));

            GUI.enabled = !_readOnly;
            if (rule.Type == RuleType.Bool)
                _texts[rule] = GUILayout.Toggle(_texts[rule] == bool.TrueString, "") ? bool.TrueString : bool.FalseString;
            else
                _texts[rule] = GUILayout.TextField(_texts[rule], GUILayout.Width(90f));
            GUI.enabled = true;

            Color oldContent = GUI.contentColor;
            if (IsPendingChange(rule))
                GUI.contentColor = ChangedColor;
            GUILayout.Label($"default {rule.DefaultText}");
            GUI.contentColor = oldContent;
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(stat.Description))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(12f);
                GUILayout.Label(stat.Description);
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndScrollView();

        if (!string.IsNullOrEmpty(_message))
            GUILayout.Label(_message);

        GUILayout.BeginHorizontal();
        if (!_readOnly)
        {
            if (GUILayout.Button("Apply"))
                Apply();
            if (GUILayout.Button("Reset to defaults"))
            {
                foreach (AbilityStat stat in set.Stats)
                    _texts[stat.Rule] = stat.Rule.DefaultText;
                _message = "Back to the defaults (press Apply).";
            }
        }
        if (GUILayout.Button("Back (Esc)"))
            CloseTopAbilitiesWindow();
        GUILayout.EndHorizontal();
    }
}
