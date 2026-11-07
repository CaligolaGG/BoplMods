using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Configuration;

namespace CustomRules.Core;

/// <summary>
/// Game mode profiles: named sets of rule values, one text file each ("key=value" lines) in
/// BepInEx/config/CustomRules.Profiles/&lt;name&gt;.rules. Loading a profile replaces this machine's rules (the local
/// values); the rules from before the first load are kept aside, and unloading puts them back.
/// The loaded profile's name is remembered in the config ([Profiles] Active).
/// </summary>
internal static class RuleProfiles
{
    private const string Extension = ".rules";

    private static ConfigEntry<string> _activeEntry;

    private static string Folder => Path.Combine(Paths.ConfigPath, "CustomRules.Profiles");

    /// <summary>The rules from before a profile was loaded (not a profile itself: other extension).</summary>
    private static string BackupFile => Path.Combine(Folder, "_before_profile.txt");

    internal static void Init()
    {
        _activeEntry = Plugin.RulesConfig.Bind("Profiles", "Active", "",
            "Name of the loaded game mode profile (managed by the Profiles tab of the rules window).");
    }

    /// <summary>The loaded profile, or null (own rules).</summary>
    internal static string Active
    {
        get
        {
            string name = _activeEntry?.Value;
            if (string.IsNullOrEmpty(name))
                return null;
            // The rules window asks on every GUI event: the file check is redone twice a second (or after a change).
            if (name != _checkedName || UnityEngine.Time.unscaledTime >= _checkedUntil)
            {
                _checkedName = name;
                _checkedExists = Exists(name);
                _checkedUntil = UnityEngine.Time.unscaledTime + 0.5f;
            }
            return _checkedExists ? name : null;
        }
        private set
        {
            _activeEntry.Value = value ?? "";
            FilesChanged();
        }
    }

    private static string _checkedName;
    private static bool _checkedExists;
    private static float _checkedUntil, _listedUntil;
    private static List<string> _listed;

    /// <summary>A profile file was written, renamed or deleted: the cached checks look again.</summary>
    private static void FilesChanged()
    {
        _checkedName = null;
        _listed = null;
    }

    /// <summary>The saved profiles (a copy; the folder is read at most twice a second, or after a change).</summary>
    internal static List<string> List()
    {
        if (_listed == null || UnityEngine.Time.unscaledTime >= _listedUntil)
        {
            _listed = !Directory.Exists(Folder)
                ? new List<string>()
                : Directory.GetFiles(Folder, "*" + Extension)
                    .Select(Path.GetFileNameWithoutExtension)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            _listedUntil = UnityEngine.Time.unscaledTime + 0.5f;
        }
        return new List<string>(_listed);
    }

    /// <summary>A usable profile name (file-name safe), or null if nothing is left of it.</summary>
    internal static string Sanitize(string name)
    {
        if (name == null)
            return null;
        char[] invalid = Path.GetInvalidFileNameChars();
        string clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim().TrimEnd('.');
        return clean.Length == 0 ? null : clean;
    }

    internal static bool Exists(string name) => File.Exists(PathOf(name));

    /// <summary>Saves this machine's current rules as (or over) the profile.</summary>
    internal static void SaveCurrent(string name) => Write(PathOf(name), CurrentValues());

    /// <summary>Applies a profile to this machine's rules. The first load keeps the previous rules aside.</summary>
    internal static void Load(string name)
    {
        Dictionary<string, string> values = Read(PathOf(name));
        if (Active == null)
            Write(BackupFile, CurrentValues());
        ApplyToLocal(values);
        Active = name;
        Plugin.Log.LogInfo($"Game mode profile '{name}' loaded.");
    }

    /// <summary>Back to the rules from before the first profile load.</summary>
    internal static void Unload()
    {
        if (File.Exists(BackupFile))
        {
            ApplyToLocal(Read(BackupFile));
            File.Delete(BackupFile);
        }
        Active = null;
        Plugin.Log.LogInfo("Game mode profile unloaded.");
    }

    internal static void Rename(string oldName, string newName)
    {
        File.Move(PathOf(oldName), PathOf(newName));
        FilesChanged();
        if (string.Equals(_activeEntry.Value, oldName, StringComparison.OrdinalIgnoreCase))
            Active = newName;
    }

    /// <summary>Deletes the file. If it was loaded, the current rules stay as they are, no longer tied to a profile.</summary>
    internal static void Delete(string name)
    {
        File.Delete(PathOf(name));
        FilesChanged();
        if (string.Equals(_activeEntry.Value, name, StringComparison.OrdinalIgnoreCase))
        {
            Active = null;
            if (File.Exists(BackupFile))
                File.Delete(BackupFile);
        }
    }

    /// <summary>True if the current rules differ from what the loaded profile has saved.</summary>
    internal static bool ActiveIsModified()
    {
        string active = Active;
        if (active == null)
            return false;
        Dictionary<string, string> saved = Read(PathOf(active));
        return RuleRegistry.Rules.Any(r => (saved.TryGetValue(r.Key, out string text) && r.TryParse(text, out object value)
            ? Rule.Format(value)
            : r.DefaultText) != r.LocalText);
    }

    private static string PathOf(string name) => Path.Combine(Folder, name + Extension);

    private static Dictionary<string, string> CurrentValues() =>
        RuleRegistry.Rules.ToDictionary(r => r.Key, r => r.LocalText);

    /// <summary>Missing or invalid values (e.g. a rule added after the profile was saved) get the default.</summary>
    private static void ApplyToLocal(IReadOnlyDictionary<string, string> values)
    {
        foreach (Rule rule in RuleRegistry.Rules)
        {
            if (!values.TryGetValue(rule.Key, out string text) || !rule.TryParse(text, out object value))
                value = rule.DefaultValue;
            rule.SetLocal(value);
        }
        RulesSync.BroadcastIfHost();
    }

    private static void Write(string path, IReadOnlyDictionary<string, string> values)
    {
        Directory.CreateDirectory(Folder);
        var text = new StringBuilder("# CustomRules game mode profile\n");
        foreach (KeyValuePair<string, string> pair in values)
            text.Append(pair.Key).Append('=').Append(pair.Value).Append('\n');
        File.WriteAllText(path, text.ToString());
        FilesChanged();
    }

    private static Dictionary<string, string> Read(string path)
    {
        var values = new Dictionary<string, string>();
        foreach (string line in File.ReadAllLines(path))
        {
            int eq = line.IndexOf('=');
            if (eq > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }
        return values;
    }
}
