using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using CaliGameModes.Modes.Football;
using HarmonyLib;

namespace CaliGameModes.Maps;

/// <summary>
/// Talks to the Map Loader mod (MapMakerTeam, "com.MLT.MapLoader"), a soft dependency, through reflection only.
///
/// Map Loader plays custom maps (zips in BepInEx/plugins/Maps) instead of the game's levels as soon as it has any.
/// Game modes that need their own maps (football) ship them in this mod: they're copied into that folder at start-up,
/// and the host / local player's map list is filtered to the mode's maps while the mode is on, and to the other maps
/// while it's off (none left = the game's own levels, offline). Online guests get the host's map from Map Loader itself.
/// </summary>
internal static class MapLoaderBridge
{
    internal const string Guid = "com.MLT.MapLoader";

    // Map Loader's static state (MapMaker.Plugin): the map list (four arrays, same indices) and the picks.
    private static Type _plugin;
    private static FieldInfo _mapJsons, _metaDataJsons, _zipArchives, _myZipArchives, _mapIndexesLeft, _nextMapIndex,
        _currentMapUuid;
    private static MethodInfo _randomBagLevel;
    private static FieldInfo _hasReceivedLatestZip;

    // The full list, as Map Loader loaded it.
    private static string[] _allJsons, _allMeta;
    private static Array _allZips;
    private static int[] _allUuids;

    private static bool? _footballShown;

    internal static bool Available => _allJsons != null;

    /// <summary>The custom map being played (Map Loader's pick), or 0 without Map Loader.</summary>
    internal static int CurrentMapUuid => _currentMapUuid != null ? (int)_currentMapUuid.GetValue(null) : 0;

    internal static void Init(Harmony harmony)
    {
        if (!Chainloader.PluginInfos.ContainsKey(Guid))
        {
            Plugin.Log.LogInfo("Map Loader not installed: football is played on the game's own levels, without goals.");
            return;
        }

        _plugin = AccessTools.TypeByName("MapMaker.Plugin");
        Type networking = AccessTools.TypeByName("MapMaker.NetworkingStuff");
        MethodInfo start = _plugin != null ? AccessTools.Method(_plugin, "Start") : null;
        if (start == null)
        {
            Plugin.Log.LogWarning("Map Loader found, but not the version this mod knows: football maps unavailable.");
            return;
        }
        _mapJsons = AccessTools.Field(_plugin, "MapJsons");
        _metaDataJsons = AccessTools.Field(_plugin, "MetaDataJsons");
        _zipArchives = AccessTools.Field(_plugin, "zipArchives");
        _myZipArchives = AccessTools.Field(_plugin, "MyZipArchives");
        _mapIndexesLeft = AccessTools.Field(_plugin, "MapIndexsLeft");
        _nextMapIndex = AccessTools.Field(_plugin, "NextMapIndex");
        _currentMapUuid = AccessTools.Field(_plugin, "CurrentMapUUID");
        _randomBagLevel = AccessTools.Method(_plugin, "RandomBagLevel");
        _hasReceivedLatestZip = networking != null ? AccessTools.Field(networking, "HasRecevedLatestZip") : null;

        CopyShippedMaps();
        harmony.Patch(start, postfix: new HarmonyMethod(typeof(MapLoaderBridge), nameof(AfterMapsLoaded)));
    }

    /// <summary>Our maps (embedded "CaliGameModes.Maps.*.zip") into Map Loader's folder, unless already there unchanged.</summary>
    private static void CopyShippedMaps()
    {
        string folder = Path.Combine(Paths.PluginPath, "Maps");
        Assembly assembly = typeof(MapLoaderBridge).Assembly;
        foreach (string resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith("CaliGameModes.Maps.") && n.EndsWith(".zip")))
        {
            try
            {
                byte[] bytes;
                using (Stream stream = assembly.GetManifestResourceStream(resource))
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    bytes = memory.ToArray();
                }

                Directory.CreateDirectory(folder);
                if (Directory.GetFiles(folder, "*.zip").Any(f => new FileInfo(f).Length == bytes.Length && File.ReadAllBytes(f).SequenceEqual(bytes)))
                    continue;
                string name = "CaliGameModes " + resource.Substring("CaliGameModes.Maps.".Length);
                File.WriteAllBytes(Path.Combine(folder, name), bytes);
                Plugin.Log.LogInfo($"Added the map '{name}' to Map Loader's maps.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't add the map {resource}: {e}");
            }
        }
    }

    /// <summary>After Map Loader read its maps: keep the full list (one map per UUID), then filter for the mode.</summary>
    private static void AfterMapsLoaded()
    {
        try
        {
            var jsons = (string[])_mapJsons.GetValue(null);
            var meta = (string[])_metaDataJsons.GetValue(null);
            var mine = (Array)_myZipArchives.GetValue(null);
            Array zips = mine != null && mine.Length == jsons.Length ? mine : (Array)_zipArchives.GetValue(null);
            if (jsons == null || meta == null || zips == null || zips.Length != jsons.Length || meta.Length != jsons.Length)
            {
                Plugin.Log.LogWarning("Map Loader's map list looks different than expected: football maps unavailable.");
                return;
            }

            // The same map twice (e.g. the copy we added and the player's own): Map Loader would play it twice as
            // often and online it checks UUIDs are unique.
            var keep = new List<int>();
            var seen = new HashSet<int>();
            for (int i = 0; i < meta.Length; i++)
            {
                int uuid = Uuid(meta[i]);
                if (uuid == 0 || seen.Add(uuid))
                    keep.Add(i);
            }

            _allJsons = keep.Select(i => jsons[i]).ToArray();
            _allMeta = keep.Select(i => meta[i]).ToArray();
            _allUuids = _allMeta.Select(Uuid).ToArray();
            _allZips = Array.CreateInstance(zips.GetType().GetElementType(), keep.Count);
            for (int i = 0; i < keep.Count; i++)
                _allZips.SetValue(zips.GetValue(keep[i]), i);

            Plugin.Log.LogInfo($"Map Loader: {_allJsons.Length} maps, {_allUuids.Count(FootballMaps.IsFootballMap)} for football.");
            _footballShown = null;
            ShowMapsFor(football: false);
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Reading Map Loader's maps failed: {e}");
            _allJsons = null;
        }
    }

    /// <summary>
    /// Host / local player, in the lobby: Map Loader picks only football maps while <paramref name="football"/>, only
    /// the others otherwise. Cheap when nothing changed (called every frame).
    /// </summary>
    internal static void ShowMapsFor(bool football)
    {
        if (!Available || _footballShown == football)
            return;
        _footballShown = football;

        List<int> picked = Enumerable.Range(0, _allUuids.Length).Where(i => FootballMaps.IsFootballMap(_allUuids[i]) == football).ToList();
        // Online, Map Loader can't host without maps: rather play the football maps without the mode.
        if (picked.Count == 0 && !football && GameLobby.isOnlineGame)
            picked = Enumerable.Range(0, _allUuids.Length).ToList();

        Array zips = Array.CreateInstance(_allZips.GetType().GetElementType(), picked.Count);
        for (int i = 0; i < picked.Count; i++)
            zips.SetValue(_allZips.GetValue(picked[i]), i);
        _mapJsons.SetValue(null, picked.Select(i => _allJsons[i]).ToArray());
        _metaDataJsons.SetValue(null, picked.Select(i => _allMeta[i]).ToArray());
        _zipArchives.SetValue(null, zips);
        _myZipArchives.SetValue(null, zips.Clone());
        (_mapIndexesLeft.GetValue(null) as IList)?.Clear();

        // The host told its guests which map comes next: pick again and let Map Loader resend it.
        if (picked.Count > 0 && GameLobby.isOnlineGame && SteamManager.LocalPlayerIsLobbyOwner)
        {
            _nextMapIndex.SetValue(null, _randomBagLevel.Invoke(null, null));
            if (_hasReceivedLatestZip != null)
                _hasReceivedLatestZip.SetValue(null, Activator.CreateInstance(_hasReceivedLatestZip.FieldType));
        }
        Plugin.Log.LogInfo($"Map Loader: {picked.Count} maps for {(football ? "football" : "normal matches")}.");
    }

    private static int Uuid(string metaJson)
    {
        Match match = Regex.Match(metaJson ?? "", "\"MapUUID\"\\s*:\\s*(-?\\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out int uuid) ? uuid : 0;
    }
}
