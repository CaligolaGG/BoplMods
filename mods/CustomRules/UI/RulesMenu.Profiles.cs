using System;
using System.Collections.Generic;
using CustomRules.Core;
using UnityEngine;

namespace CustomRules.UI;

/// <summary>
/// The Profiles tab: save the current rules as a game mode profile, then load (switch), unload, overwrite, rename and
/// delete profiles (<see cref="RuleProfiles"/>). Editing a profile = load it, change rules in the Rules tab, then
/// "Save to profile" there (or "Overwrite" here).
/// </summary>
internal sealed partial class RulesMenu
{
    private string _newProfileName = "";
    private string _renaming;
    private string _renameText = "";
    private string _confirmDelete;
    private Vector2 _profilesScroll;
    private GUIStyle _activeProfileStyle;
    private GUIStyle _profileNameStyle;
    private bool _activeModified;
    private float _nextModifiedCheck;

    /// <summary>The loaded profile's name: bold and bigger than the text around it.</summary>
    private GUIStyle ProfileNameStyle
    {
        get
        {
            if (_profileNameStyle != null)
                return _profileNameStyle;
            // fontSize 0 means "the font's own size": take the base size from the font then.
            int baseSize = GUI.skin.label.fontSize > 0 ? GUI.skin.label.fontSize
                : GUI.skin.font != null && GUI.skin.font.fontSize > 0 ? GUI.skin.font.fontSize : 14;
            _profileNameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = baseSize + 5 };
            return _profileNameStyle;
        }
    }

    private void ResetProfilesTab()
    {
        _newProfileName = "";
        _renaming = null;
        _confirmDelete = null;
        _profilesScroll = Vector2.zero;
    }

    /// <summary>Top of the Rules tab: which profile is loaded, and a shortcut to save edits back into it.</summary>
    private void DrawActiveProfileLine()
    {
        string active = RuleProfiles.Active;
        if (active == null)
            return;

        // Reads the profile file: checked twice a second, not on every GUI pass.
        if (Time.unscaledTime >= _nextModifiedCheck)
        {
            _activeModified = RuleProfiles.ActiveIsModified();
            _nextModifiedCheck = Time.unscaledTime + 0.5f;
        }
        bool modified = _activeModified;

        // Centered name, with its "Save to" button centered below when there are changes to save.
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label(active, ProfileNameStyle, GUILayout.ExpandWidth(false));
        if (modified)
            GUILayout.Label("(modified)", GUILayout.ExpandWidth(false));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (!modified)
            return;
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button($"Save to '{active}'", GUILayout.Width(200f)))
            RunProfileAction(() =>
            {
                if (!Apply())
                    return null;
                RuleProfiles.SaveCurrent(active);
                return $"Saved the rules to '{active}'.";
            });
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void DrawProfilesTab()
    {
        _activeProfileStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

        if (_readOnly)
            GUILayout.Label("You're a guest: profiles change your own rules, used when you host. This match uses the host's rules.");

        // Loaded profile.
        string active = RuleProfiles.Active;
        // Centered name, with its Unload button centered below.
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.Label(active ?? "No profile loaded: your own rules.", active != null ? ProfileNameStyle : _activeProfileStyle,
            GUILayout.ExpandWidth(false));
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        if (active != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Unload", GUILayout.Width(110f)))
                RunProfileAction(() =>
                {
                    RuleProfiles.Unload();
                    return "Profile unloaded: your rules from before are back.";
                });
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        // Save the current rules as a new profile.
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("New profile from the current rules:", GUILayout.Width(250f));
        _newProfileName = GUILayout.TextField(_newProfileName, 40, GUILayout.Width(180f));
        if (GUILayout.Button("Save", GUILayout.Width(80f)))
            RunProfileAction(SaveNewProfile);
        GUILayout.EndHorizontal();

        // Saved profiles.
        GUILayout.Space(10f);
        List<string> profiles = RuleProfiles.List();
        if (profiles.Count == 0)
            GUILayout.Label("No saved profiles yet.");

        _profilesScroll = GUILayout.BeginScrollView(_profilesScroll);
        foreach (string name in profiles)
            DrawProfileRow(name, string.Equals(name, active, StringComparison.OrdinalIgnoreCase));
        GUILayout.EndScrollView();

        if (!string.IsNullOrEmpty(_message))
            GUILayout.Label(_message);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Close (Esc)", GUILayout.Width(120f)))
            Close();
        GUILayout.EndHorizontal();
    }

    private void DrawProfileRow(string name, bool isActive)
    {
        GUILayout.BeginHorizontal(GUI.skin.box);

        if (_renaming == name)
        {
            _renameText = GUILayout.TextField(_renameText, 40, GUILayout.Width(220f));
            if (GUILayout.Button("OK", GUILayout.Width(50f)))
                RunProfileAction(() => RenameProfile(name));
            if (GUILayout.Button("Cancel", GUILayout.Width(70f)))
                _renaming = null;
            GUILayout.EndHorizontal();
            return;
        }

        GUILayout.Label(isActive ? $"{name}  (loaded)" : name, isActive ? _activeProfileStyle : GUI.skin.label, GUILayout.Width(200f));

        GUI.enabled = !isActive;
        if (GUILayout.Button(RuleProfiles.Active == null ? "Load" : "Switch", GUILayout.Width(70f)))
            RunProfileAction(() =>
            {
                RuleProfiles.Load(name);
                return $"Loaded '{name}'.";
            });
        GUI.enabled = true;

        if (GUILayout.Button("Overwrite", GUILayout.Width(80f)))
            RunProfileAction(() =>
            {
                if (!ApplyIfEditable())
                    return null;
                RuleProfiles.SaveCurrent(name);
                return $"Saved the current rules to '{name}'.";
            });

        if (GUILayout.Button("Rename", GUILayout.Width(70f)))
        {
            _renaming = name;
            _renameText = name;
            _confirmDelete = null;
        }

        bool confirming = _confirmDelete == name;
        if (GUILayout.Button(confirming ? "Sure?" : "Delete", GUILayout.Width(60f)))
        {
            if (confirming)
                RunProfileAction(() =>
                {
                    RuleProfiles.Delete(name);
                    return $"Deleted '{name}'.";
                });
            else
                _confirmDelete = name;
        }

        GUILayout.EndHorizontal();
    }

    private string SaveNewProfile()
    {
        string name = RuleProfiles.Sanitize(_newProfileName);
        if (name == null)
            return "Type a name for the profile first.";
        if (RuleProfiles.Exists(name))
            return $"'{name}' already exists: use its Overwrite button to replace it.";
        if (!ApplyIfEditable())
            return null;

        RuleProfiles.SaveCurrent(name);
        _newProfileName = "";
        return $"Saved the current rules as '{name}'.";
    }

    private string RenameProfile(string oldName)
    {
        string newName = RuleProfiles.Sanitize(_renameText);
        if (newName == null)
            return "Type a name for the profile first.";
        _renaming = null;
        if (newName == oldName)
            return null;
        if (RuleProfiles.Exists(newName) && !string.Equals(newName, oldName, StringComparison.OrdinalIgnoreCase))
            return $"'{newName}' already exists.";

        RuleProfiles.Rename(oldName, newName);
        return $"Renamed '{oldName}' to '{newName}'.";
    }

    /// <summary>Unapplied edits in the Rules tab are applied first, so a saved profile has what's on screen.</summary>
    private bool ApplyIfEditable() => _readOnly || Apply();

    /// <summary>Runs a profile action, shows its result (or the error), and refreshes the Rules tab's values.</summary>
    private void RunProfileAction(Func<string> action)
    {
        _confirmDelete = null;
        _nextModifiedCheck = 0f;
        try
        {
            string result = action();
            if (result != null)
                _message = result;
        }
        catch (Exception e)
        {
            _message = "Failed: " + e.Message;
            Plugin.Log.LogError($"Profile action failed: {e}");
        }
        if (!_readOnly)
            LoadTexts();
    }
}
