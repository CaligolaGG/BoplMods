using System;
using System.Collections.Generic;
using System.Linq;
using CustomRules.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CustomRules.UI;

/// <summary>
/// "Custom rules" button on the character select screens (local and online lobby) and the rules window (IMGUI).
/// The host (or, locally, whoever plays) edits the rules; online clients see the host's rules read-only.
/// Rules registered with a parent are shown under it, only while the parent is on (sub menu).
/// Two tabs: Rules, and Profiles (saved sets of rules, see RulesMenu.Profiles.cs).
/// </summary>
internal sealed partial class RulesMenu : MonoBehaviour
{
    private const int WindowId = 0x0C5713;
    private const float WindowWidth = 600f;
    private const float SectionGap = 16f;
    private const float RuleGap = 8f;
    private static readonly string[] LobbyScenes = { "CharacterSelect", "ChSelect_online" };

    private static RulesMenu _instance;
    private static int _closedOnFrame = -1;

    private bool _open;
    private bool _readOnly;
    private int _tab;
    private static readonly string[] Tabs = { "Rules", "Profiles" };
    private Dictionary<Rule, string> _texts = new();
    private string _message;
    private Vector2 _scroll;
    private Rect _window;
    private Texture2D _backdrop;
    private GUIStyle _sectionTitleStyle;
    private GUIStyle _ruleFrameStyle;
    private readonly List<Behaviour> _disabledEventSystems = new();

    /// <summary>True while open, and for the frame it closed in (so the closing key/click doesn't reach the menu).</summary>
    internal static bool BlocksMenuInput => (_instance != null && _instance._open) || Time.frameCount == _closedOnFrame;

    /// <summary>Called once from a menu patch: own GameObject, the plugin's one is destroyed before the main menu.</summary>
    internal static void EnsureCreated()
    {
        if (_instance != null)
            return;
        var go = new GameObject("CustomRules.Menu");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<RulesMenu>();
    }

    private static bool InLobbyScene => Array.IndexOf(LobbyScenes, SceneManager.GetActiveScene().name) >= 0;

    private void Open()
    {
        _readOnly = RulesSync.IsOnlineClient;
        LoadTexts();
        _message = _readOnly && RulesSync.HostValues == null ? "The host's rules haven't arrived yet." : null;
        _tab = 0;
        _picking = null;
        ResetProfilesTab();
        _scroll = Vector2.zero;
        _window = default;
        _open = true;
        SetMenuEventSystems(enabled: false);
    }

    /// <summary>The values shown in the Rules tab: this machine's rules, or the host's for online guests.</summary>
    private void LoadTexts()
    {
        IReadOnlyDictionary<string, string> hostValues = RulesSync.HostValues;
        _texts = RuleRegistry.Rules.ToDictionary(
            r => r,
            r => !_readOnly ? r.LocalText
                : hostValues != null && hostValues.TryGetValue(r.Key, out string text) ? text
                : r.DefaultText);
    }

    private void Close()
    {
        _open = false;
        _closedOnFrame = Time.frameCount;
        SetMenuEventSystems(enabled: true);
    }

    private void Update()
    {
        if (!_open)
            return;
        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (!InLobbyScene)
            Close();
        else if (escape && _picking != null)
            _picking = null; // Esc closes the ability picker first
        else if (escape)
            Close();
    }

    private void OnGUI()
    {
        if (!InLobbyScene)
            return;

        // Scale for high resolutions: the layout is designed for 720p.
        float scale = Mathf.Max(1f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale, height = Screen.height / scale;

        if (!_open)
        {
            string label = RulesSync.IsOnlineClient ? "Custom rules (host)" : "Custom rules";
            if (GUI.Button(new Rect(10f, 10f, 150f, 30f), label))
                Open();
            return;
        }

        if (_backdrop == null)
        {
            _backdrop = new Texture2D(1, 1) { hideFlags = HideFlags.DontUnloadUnusedAsset };
            _backdrop.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            _backdrop.Apply();
        }
        GUI.DrawTexture(new Rect(0, 0, width, height), _backdrop);

        if (_window.width <= 0f)
        {
            float h = Mathf.Min(height - 40f, 200f + RuleRegistry.Rules.Count * 64f + RuleRegistry.Sections.Count * 60f);
            _window = new Rect((width - WindowWidth) / 2f, (height - h) / 2f, WindowWidth, h);
        }
        // Only one modal window at a time: the option picker replaces the rules window while it's open.
        if (_picking != null)
            DrawPickerWindow(width, height);
        else
            _window = GUI.ModalWindow(WindowId, _window, DrawWindow, _readOnly ? "Custom rules (set by the host)" : "Custom rules");
    }

    private void DrawWindow(int id)
    {
        int tab = GUILayout.Toolbar(_tab, Tabs);
        if (tab != _tab)
        {
            _tab = tab;
            _message = null;
            _scroll = Vector2.zero;
        }

        if (_tab == 1)
            DrawProfilesTab();
        else
            DrawRulesTab();

        GUI.DragWindow();
    }

    private void DrawRulesTab()
    {
        if (!_readOnly)
            DrawActiveProfileLine();

        GUILayout.Label(_readOnly
            ? "Only the lobby host can change the rules. Every player needs the same rule mods."
            : "Applies to everyone in the match. Online, the host's rules are used (every player needs the same rule mods).");

        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (Rule rule in RuleRegistry.Rules.Where(r => r.Parent == null && r.Section == null))
            DrawRule(rule, 0);
        foreach (RuleSection section in RuleRegistry.Sections)
            DrawSection(section);
        GUILayout.EndScrollView();

        if (!string.IsNullOrEmpty(_message))
            GUILayout.Label(_message);

        GUILayout.BeginHorizontal();
        if (!_readOnly)
        {
            if (GUILayout.Button("Apply"))
                Apply();
            if (GUILayout.Button("Apply & close") && Apply())
                Close();
            if (GUILayout.Button("Reset to defaults"))
            {
                foreach (Rule rule in RuleRegistry.Rules)
                    _texts[rule] = rule.DefaultText;
                Apply();
            }
        }
        if (GUILayout.Button("Close (Esc)"))
            Close();
        GUILayout.EndHorizontal();
    }

    /// <summary>A mod's own rules: set apart by a gap, in a box tinted with the section's color, under its title.</summary>
    private void DrawSection(RuleSection section)
    {
        List<Rule> rules = RuleRegistry.Rules.Where(r => r.Parent == null && r.Section == section).ToList();
        if (rules.Count == 0)
            return;

        // fontSize 0 means "the font's own size", so the base size comes from the font when the skin doesn't set one.
        int baseSize = GUI.skin.label.fontSize > 0 ? GUI.skin.label.fontSize
            : GUI.skin.font != null && GUI.skin.font.fontSize > 0 ? GUI.skin.font.fontSize : 14;
        _sectionTitleStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = baseSize + 3 };

        GUILayout.Space(SectionGap);
        Color oldBackground = GUI.backgroundColor;
        Color oldContent = GUI.contentColor;
        GUI.backgroundColor = section.Tint;
        GUILayout.BeginVertical(GUI.skin.box);
        GUI.backgroundColor = oldBackground;

        GUI.contentColor = Color.Lerp(section.Tint, Color.white, 0.35f);
        GUILayout.Label(section.Title, _sectionTitleStyle);
        GUI.contentColor = oldContent;

        foreach (Rule rule in rules)
            DrawRule(rule, 0);
        GUILayout.EndVertical();
    }

    private void DrawRule(Rule rule, int depth)
    {
        // Each rule (with its sub rules inside) sits in its own thin rounded frame, apart from the next one.
        if (depth == 0)
            GUILayout.BeginVertical(RuleFrameStyle);

        float indent = depth * 24f;
        GUILayout.BeginHorizontal();
        GUILayout.Space(indent);
        GUILayout.Label(rule.Label, GUILayout.Width(200f - indent));

        GUI.enabled = !_readOnly;
        switch (rule.Type)
        {
            case RuleType.Bool:
                bool wasOn = _texts[rule] == bool.TrueString;
                bool on = GUILayout.Toggle(wasOn, "");
                _texts[rule] = on ? bool.TrueString : bool.FalseString;
                // Exclusive pair: turning this one on turns the other off.
                if (on && !wasOn && rule.ExclusiveWith != null)
                    _texts[rule.ExclusiveWith] = bool.FalseString;
                break;
            case RuleType.Choice:
                DrawChoice(rule);
                break;
            default:
                _texts[rule] = GUILayout.TextField(_texts[rule], GUILayout.Width(90f));
                break;
        }
        GUI.enabled = true;

        if (rule.Type != RuleType.Choice)
            GUILayout.Label(rule.Type == RuleType.Bool
                ? $"default {rule.DefaultText}"
                : $"default {rule.DefaultText}  ({Rule.Format(rule.Min)}–{Rule.Format(rule.Max)})");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Space(indent + 12f);
        GUILayout.Label(rule.Description);
        GUILayout.EndHorizontal();

        // Sub menu: children only while this (bool) rule is on.
        if (rule.Type == RuleType.Bool && _texts[rule] == bool.TrueString)
        {
            foreach (Rule child in RuleRegistry.Rules.Where(r => r.Parent == rule))
                DrawRule(child, depth + 1);
        }

        if (depth == 0)
        {
            GUILayout.EndVertical();
            GUILayout.Space(RuleGap);
        }
    }

    /// <summary>A 9-sliced texture: transparent inside, thin white rounded outline.</summary>
    private GUIStyle RuleFrameStyle
    {
        get
        {
            // The texture is only referenced through the GUIStyle, which Resources.UnloadUnusedAssets (run on scene
            // loads, e.g. leaving a lobby) doesn't see: keep it explicitly, and rebuild if it's gone anyway.
            if (_ruleFrameStyle != null && _ruleFrameStyle.normal.background != null)
                return _ruleFrameStyle;

            const int size = 32, radius = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontUnloadUnusedAsset,
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance from the rounded rectangle's edge (negative inside).
                    float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius), 0f);
                    float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius), 0f);
                    float edge = Mathf.Sqrt(dx * dx + dy * dy) - (radius - 0.5f);
                    float alpha = Mathf.Clamp01(1f - Mathf.Abs(edge + 0.75f) / 0.9f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * 0.7f));
                }
            }
            tex.Apply();

            _ruleFrameStyle = new GUIStyle
            {
                normal = { background = tex },
                border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1),
                padding = new RectOffset(10, 10, 6, 8),
                margin = new RectOffset(2, 2, 0, 0),
            };
            return _ruleFrameStyle;
        }
    }

    /// <returns>False if a value couldn't be parsed (nothing is changed then).</returns>
    private bool Apply()
    {
        var parsed = new Dictionary<Rule, object>();
        foreach (Rule rule in RuleRegistry.Rules)
        {
            if (!rule.TryParse(_texts[rule], out object value))
            {
                _message = $"'{_texts[rule]}' is not a valid value for {rule.Label}.";
                return false;
            }
            parsed[rule] = value;
        }

        foreach (KeyValuePair<Rule, object> pair in parsed)
        {
            pair.Key.SetLocal(pair.Value);
            _texts[pair.Key] = pair.Key.LocalText; // shows clamped values
        }

        RulesSync.BroadcastIfHost();
        Plugin.Log.LogInfo("Rules changed: " + string.Join(", ", parsed.Keys.Select(r => $"{r.Key}={r.LocalText}")));
        _message = $"Applied at {DateTime.Now:HH:mm:ss}.";
        return true;
    }

    /// <summary>Stops the menu's uGUI from receiving clicks (e.g. a button under the window) while open.</summary>
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
