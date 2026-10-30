using System.Collections.Generic;
using CustomRules.Core;
using UnityEngine;

namespace CustomRules.UI;

/// <summary>
/// Choice rules in the rules window. With option icons (e.g. the preselected abilities) they look like the game's
/// ability picker: ◀ [icon] ▶, and clicking the icon opens a grid of every option (the "picker", drawn as its own modal
/// window in place of the rules window). Without icons: ◀ name ▶.
/// </summary>
internal sealed partial class RulesMenu
{
    private const int PickerWindowId = 0x0C5714;
    private const float IconSize = 56f;
    private const float PickerCellSize = 84f;
    private const int PickerColumns = 6;

    private Rule _picking;
    private Vector2 _pickerScroll;
    private GUIStyle _cellLabelStyle;

    private static List<string> ChoiceValues(Rule rule)
    {
        var values = new List<string> { "" };
        IReadOnlyList<string> options = rule.Options?.Invoke();
        if (options != null)
            values.AddRange(options);
        return values;
    }

    private string ChoiceLabel(Rule rule, string value, List<string> values) =>
        string.IsNullOrEmpty(value) ? rule.NoneLabel
        : values.Contains(value) ? value
        : value + " (not installed)";

    private void DrawChoice(Rule rule)
    {
        List<string> values = ChoiceValues(rule);
        int index = values.IndexOf(_texts[rule]);
        bool withIcons = rule.OptionIcon != null;

        if (GUILayout.Button("◀", GUILayout.Width(28f), GUILayout.Height(withIcons ? IconSize : 22f)))
            index = index <= 0 ? values.Count - 1 : index - 1;

        if (withIcons)
        {
            // The icon is a button: it opens the picker.
            if (GUILayout.Button(GUIContent.none, GUILayout.Width(IconSize), GUILayout.Height(IconSize)) && !_readOnly)
            {
                _picking = rule;
                _pickerScroll = Vector2.zero;
            }
            DrawOptionIcon(GUILayoutUtility.GetLastRect(), rule, _texts[rule]);
        }

        if (GUILayout.Button("▶", GUILayout.Width(28f), GUILayout.Height(withIcons ? IconSize : 22f)))
            index = index >= values.Count - 1 ? 0 : index + 1;

        GUILayout.Label(ChoiceLabel(rule, _texts[rule], values), withIcons ? GUI.skin.label : GUI.skin.textField,
            GUILayout.Width(170f), GUILayout.Height(withIcons ? IconSize : 22f));

        if (index >= 0)
            _texts[rule] = values[index];
    }

    /// <summary>The option's sprite inside <paramref name="rect"/>, or a "?" for none / unknown.</summary>
    private static void DrawOptionIcon(Rect rect, Rule rule, string value)
    {
        Sprite sprite = string.IsNullOrEmpty(value) ? null : rule.OptionIcon?.Invoke(value);
        Rect inner = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f);
        if (sprite == null || sprite.texture == null)
        {
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24 };
            GUI.Label(inner, "?", style);
            return;
        }

        // Sprites can sit in an atlas: draw just their part of the texture, keeping the aspect ratio.
        Texture tex = sprite.texture;
        Rect tr;
        try
        {
            tr = sprite.textureRect;
        }
        catch (System.Exception)
        {
            tr = sprite.rect; // tightly packed atlas sprites have no textureRect
        }
        var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
        float aspect = tr.width / tr.height;
        Rect fitted = aspect >= 1f
            ? new Rect(inner.x, inner.center.y - inner.width / aspect / 2f, inner.width, inner.width / aspect)
            : new Rect(inner.center.x - inner.height * aspect / 2f, inner.y, inner.height * aspect, inner.height);
        GUI.DrawTextureWithTexCoords(fitted, tex, uv);
    }

    private void DrawPickerWindow(float screenWidth, float screenHeight)
    {
        float width = PickerColumns * (PickerCellSize + 6f) + 40f;
        float height = Mathf.Min(screenHeight - 40f, 520f);
        var rect = new Rect((screenWidth - width) / 2f, (screenHeight - height) / 2f, width, height);
        GUI.ModalWindow(PickerWindowId, rect, DrawPicker, _picking.Label);
    }

    private void DrawPicker(int id)
    {
        _cellLabelStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, wordWrap = true, fontSize = 11 };

        Rule rule = _picking;
        List<string> values = ChoiceValues(rule);
        GUILayout.Label("Click an ability to choose it.");

        _pickerScroll = GUILayout.BeginScrollView(_pickerScroll);
        for (int i = 0; i < values.Count; i += PickerColumns)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < i + PickerColumns && j < values.Count; j++)
            {
                string value = values[j];
                bool selected = value == _texts[rule];
                GUILayout.BeginVertical(GUILayout.Width(PickerCellSize));

                Color oldBackground = GUI.backgroundColor;
                if (selected)
                    GUI.backgroundColor = new Color(0.5f, 1f, 0.6f);
                bool clicked = GUILayout.Button(GUIContent.none, GUILayout.Width(PickerCellSize), GUILayout.Height(PickerCellSize - 20f));
                GUI.backgroundColor = oldBackground;
                DrawOptionIcon(GUILayoutUtility.GetLastRect(), rule, value);
                GUILayout.Label(ChoiceLabel(rule, value, values), _cellLabelStyle, GUILayout.Width(PickerCellSize));

                GUILayout.EndVertical();
                if (clicked)
                {
                    _texts[rule] = value;
                    _picking = null;
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Cancel (Esc)", GUILayout.Width(120f)))
            _picking = null;
        GUILayout.EndHorizontal();
    }
}
