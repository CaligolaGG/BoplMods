using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace CaliSkills.Core;

/// <summary>
/// Dev tool: a modal window (Unity IMGUI) to change a skill's settings while the game runs.
/// Opened by right-clicking a skill in the character select ability grid (see <see cref="SkillSettingsEditorPatches"/>).
/// Edits the public static float/int/bool fields of <see cref="Skill.SettingsType"/> directly, so they last
/// until the game is restarted; the defaults in code are untouched.
/// </summary>
internal sealed class SkillSettingsEditor : MonoBehaviour
{
    private const int WindowId = 0x0CA11;
    private const float BaseWidth = 560f;

    private static SkillSettingsEditor _instance;

    /// <summary>Values at the first time a settings type was opened, i.e. the defaults from code.</summary>
    private static readonly Dictionary<Type, object[]> Defaults = new();

    private Skill _skill;
    private FieldInfo[] _fields;
    private string[] _texts;
    private string _message;
    private Vector2 _scroll;
    private Rect _window;
    private readonly List<Behaviour> _disabledEventSystems = new();
    private Texture2D _backdrop;

    private static int _closedOnFrame = -1;

    internal static bool IsOpen => _instance != null && _instance._skill != null;

    /// <summary>
    /// True while open, and for the frame it closed in: the key or click that closed it
    /// (e.g. Esc) must not also reach the menu, which reads input later in that frame.
    /// </summary>
    internal static bool BlocksMenuInput => IsOpen || Time.frameCount == _closedOnFrame;

    internal static void Open(Skill skill)
    {
        if (skill?.SettingsType == null)
            return;

        if (_instance == null)
        {
            // Own GameObject: the plugin's one is destroyed by the game before the main menu.
            var go = new GameObject("CaliSkills.SettingsEditor");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SkillSettingsEditor>();
        }
        _instance.Show(skill);
    }

    private void Show(Skill skill)
    {
        _skill = skill;
        _fields = skill.SettingsType
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => !f.IsInitOnly && !f.IsLiteral &&
                        (f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(bool)))
            .ToArray();

        if (!Defaults.ContainsKey(skill.SettingsType))
            Defaults[skill.SettingsType] = _fields.Select(f => f.GetValue(null)).ToArray();

        LoadTexts();
        _message = null;
        _scroll = Vector2.zero;
        _window = default;
        SetMenuEventSystems(enabled: false);
    }

    private void Close()
    {
        _skill = null;
        _closedOnFrame = Time.frameCount;
        SetMenuEventSystems(enabled: true);
    }

    private void LoadTexts() => _texts = _fields.Select(f => Format(f.GetValue(null))).ToArray();

    private void Update()
    {
        if (IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    private void OnDisable()
    {
        if (IsOpen)
            Close();
    }

    private void OnGUI()
    {
        if (!IsOpen)
            return;

        // Scale for high resolutions: the layout is designed for 720p.
        float scale = Mathf.Max(1f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale, height = Screen.height / scale;

        if (_backdrop == null)
        {
            _backdrop = new Texture2D(1, 1) { hideFlags = HideFlags.DontUnloadUnusedAsset };
            _backdrop.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            _backdrop.Apply();
        }
        GUI.DrawTexture(new Rect(0, 0, width, height), _backdrop);

        if (_window.width <= 0f)
        {
            float h = Mathf.Min(height - 40f, 140f + _fields.Length * 46f);
            _window = new Rect((width - BaseWidth) / 2f, (height - h) / 2f, BaseWidth, h);
        }
        _window = GUI.ModalWindow(WindowId, _window, DrawWindow, $"{_skill.Name} settings (dev)");
    }

    private void DrawWindow(int id)
    {
        GUILayout.Label("Applies from the next round, until the game restarts. All players in a match need the same values.");

        _scroll = GUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _fields.Length; i++)
        {
            FieldInfo field = _fields[i];
            GUILayout.BeginHorizontal();
            GUILayout.Label(field.Name, GUILayout.Width(220f));
            if (field.FieldType == typeof(bool))
                _texts[i] = GUILayout.Toggle(_texts[i] == bool.TrueString, "")
                    ? bool.TrueString
                    : bool.FalseString;
            else
                _texts[i] = GUILayout.TextField(_texts[i], GUILayout.Width(110f));
            GUILayout.Label("default " + Format(Defaults[_skill.SettingsType][i]));
            GUILayout.EndHorizontal();

            string description = field.GetCustomAttribute<DescriptionAttribute>()?.Description;
            if (!string.IsNullOrEmpty(description))
                GUILayout.Label("    " + description);
        }
        GUILayout.EndScrollView();

        if (!string.IsNullOrEmpty(_message))
            GUILayout.Label(_message);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply"))
            Apply();
        if (GUILayout.Button("Apply & close") && Apply())
            Close();
        if (GUILayout.Button("Reset to defaults"))
        {
            object[] defaults = Defaults[_skill.SettingsType];
            _texts = defaults.Select(Format).ToArray();
            Apply();
        }
        if (GUILayout.Button("Close (Esc)"))
            Close();
        GUILayout.EndHorizontal();

        GUI.DragWindow();
    }

    /// <returns>False if a value couldn't be parsed (nothing is changed then).</returns>
    private bool Apply()
    {
        var parsed = new object[_fields.Length];
        for (int i = 0; i < _fields.Length; i++)
        {
            if (!TryParse(_fields[i].FieldType, _texts[i], out parsed[i]))
            {
                _message = $"'{_texts[i]}' is not a valid {_fields[i].FieldType.Name} for {_fields[i].Name}.";
                return false;
            }
        }

        for (int i = 0; i < _fields.Length; i++)
            _fields[i].SetValue(null, parsed[i]);
        _skill.OnSettingsChanged();
        LoadTexts();

        string summary = string.Join(", ", _fields.Select((f, i) => $"{f.Name}={_texts[i]}"));
        Plugin.Log.LogInfo($"{_skill.Name} settings changed in game: {summary}");
        _message = $"Applied at {DateTime.Now:HH:mm:ss}.";
        return true;
    }

    private static bool TryParse(Type type, string text, out object value)
    {
        text = (text ?? "").Trim().Replace(',', '.');
        value = null;
        if (type == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            value = f;
        else if (type == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            value = n;
        else if (type == typeof(bool) && bool.TryParse(text, out bool b))
            value = b;
        return value != null;
    }

    private static string Format(object value) =>
        value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "";

    /// <summary>Stops the menu's uGUI from receiving clicks (e.g. an ability entry under the window) while open.</summary>
    private void SetMenuEventSystems(bool enabled)
    {
        if (!enabled)
        {
            foreach (EventSystem system in FindObjectsOfType<EventSystem>())
            {
                if (system.enabled)
                {
                    system.enabled = false;
                    _disabledEventSystems.Add(system);
                }
            }
            return;
        }

        foreach (Behaviour system in _disabledEventSystems)
        {
            if (system != null)
                system.enabled = true;
        }
        _disabledEventSystems.Clear();
    }
}
