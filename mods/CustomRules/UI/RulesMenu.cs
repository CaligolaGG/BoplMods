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
/// </summary>
internal sealed class RulesMenu : MonoBehaviour
{
    private const int WindowId = 0x0C5713;
    private const float WindowWidth = 600f;
    private static readonly string[] LobbyScenes = { "CharacterSelect", "ChSelect_online" };

    private static RulesMenu _instance;
    private static int _closedOnFrame = -1;

    private bool _open;
    private bool _readOnly;
    private Dictionary<Rule, string> _texts = new();
    private string _message;
    private Vector2 _scroll;
    private Rect _window;
    private Texture2D _backdrop;
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
        IReadOnlyDictionary<string, string> hostValues = RulesSync.HostValues;
        _texts = RuleRegistry.Rules.ToDictionary(
            r => r,
            r => !_readOnly ? r.LocalText
                : hostValues != null && hostValues.TryGetValue(r.Key, out string text) ? text
                : r.DefaultText);
        _message = _readOnly && hostValues == null ? "The host's rules haven't arrived yet." : null;
        _scroll = Vector2.zero;
        _window = default;
        _open = true;
        SetMenuEventSystems(enabled: false);
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
        if (!InLobbyScene || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
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
            _backdrop = new Texture2D(1, 1);
            _backdrop.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
            _backdrop.Apply();
        }
        GUI.DrawTexture(new Rect(0, 0, width, height), _backdrop);

        if (_window.width <= 0f)
        {
            float h = Mathf.Min(height - 40f, 160f + RuleRegistry.Rules.Count * 48f);
            _window = new Rect((width - WindowWidth) / 2f, (height - h) / 2f, WindowWidth, h);
        }
        _window = GUI.ModalWindow(WindowId, _window, DrawWindow, _readOnly ? "Custom rules (set by the host)" : "Custom rules");
    }

    private void DrawWindow(int id)
    {
        GUILayout.Label(_readOnly
            ? "Only the lobby host can change the rules. Every player needs the same rule mods."
            : "Applies to everyone in the match. Online, the host's rules are used (every player needs the same rule mods).");

        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (Rule rule in RuleRegistry.Rules.Where(r => r.Parent == null))
            DrawRule(rule, 0);
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

        GUI.DragWindow();
    }

    private void DrawRule(Rule rule, int depth)
    {
        float indent = depth * 24f;
        GUILayout.BeginHorizontal();
        GUILayout.Space(indent);
        GUILayout.Label(rule.Label, GUILayout.Width(200f - indent));

        GUI.enabled = !_readOnly;
        if (rule.Type == RuleType.Bool)
            _texts[rule] = GUILayout.Toggle(_texts[rule] == bool.TrueString, "") ? bool.TrueString : bool.FalseString;
        else
            _texts[rule] = GUILayout.TextField(_texts[rule], GUILayout.Width(90f));
        GUI.enabled = true;

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
