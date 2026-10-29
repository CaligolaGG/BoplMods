using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using HarmonyLib;
using Steamworks;
using Steamworks.Data;

namespace CustomRules.Core;

/// <summary>
/// Decides which rule values are in effect and makes them the same on every machine.
///
/// Local games: this machine's values, at <see cref="CharacterSelectHandler.TryStartGame_inner"/>.
/// Online: the lobby host sends its values to every lobby member as a custom P2P packet ("CRULES1\nkey=value\n...",
/// at least <see cref="MinPacketSize"/> bytes so its size never matches a vanilla packet, which the game tells apart by
/// size; unmodded clients ignore it). It's sent when the host changes the rules, when a player connects, and right
/// before the start-game packet (same reliable connection, so it arrives first). Every machine then activates the
/// values in <see cref="CharacterSelectHandler_online.ForceStartGame"/>: the host its own, clients the host's.
/// </summary>
internal static class RulesSync
{
    private const string Header = "CRULES1";
    private const int MinPacketSize = 128;

    private static Dictionary<string, string> _hostValues;

    /// <summary>The values last received from the lobby host, or null if none arrived yet.</summary>
    internal static IReadOnlyDictionary<string, string> HostValues => _hostValues;

    internal static bool IsOnlineClient =>
        GameLobby.isOnlineGame && SteamManager.instance != null && !SteamManager.LocalPlayerIsLobbyOwner;

    internal static void ActivateLocalValues()
    {
        var values = new Dictionary<string, string>();
        foreach (Rule rule in RuleRegistry.Rules)
            values[rule.Key] = rule.LocalText;
        foreach (SyncedValue synced in RuleRegistry.SyncedValues)
            values[synced.Key] = synced.ReadLocal();
        Activate(values, "local");
    }

    private static void Activate(IReadOnlyDictionary<string, string> values, string source)
    {
        foreach (Rule rule in RuleRegistry.Rules)
            rule.SetActive(values.TryGetValue(rule.Key, out string text) ? text : rule.DefaultText);

        foreach (SyncedValue synced in RuleRegistry.SyncedValues)
        {
            if (!values.TryGetValue(synced.Key, out string text))
                continue;
            try
            {
                synced.ApplyActive(text);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Applying synced value {synced.Key}={text} failed: {e}");
            }
        }

        RuleRegistry.RaiseRulesActivated();

        var summary = new StringBuilder();
        foreach (Rule rule in RuleRegistry.Rules)
            summary.Append(rule.Key).Append('=').Append(rule.ActiveText).Append(' ');
        Plugin.Log.LogInfo($"Rules activated ({source}): {summary}");
    }

    private static byte[] EncodeLocalValues()
    {
        var text = new StringBuilder(Header).Append('\n');
        foreach (Rule rule in RuleRegistry.Rules)
            text.Append(rule.Key).Append('=').Append(rule.LocalText).Append('\n');
        foreach (SyncedValue synced in RuleRegistry.SyncedValues)
            text.Append(synced.Key).Append('=').Append(synced.ReadLocal()).Append('\n');
        while (text.Length < MinPacketSize)
            text.Append('\n');
        return Encoding.UTF8.GetBytes(text.ToString());
    }

    private static bool TryDecode(byte[] bytes, out Dictionary<string, string> values)
    {
        values = null;
        string text = Encoding.UTF8.GetString(bytes);
        if (!text.StartsWith(Header + "\n", StringComparison.Ordinal))
            return false;

        values = new Dictionary<string, string>();
        foreach (string line in text.Substring(Header.Length + 1).Split('\n'))
        {
            int eq = line.IndexOf('=');
            if (eq > 0)
                values[line.Substring(0, eq)] = line.Substring(eq + 1);
        }
        return true;
    }

    /// <summary>Host only: sends the current rules to every connected lobby member.</summary>
    internal static void BroadcastIfHost()
    {
        SteamManager steam = SteamManager.instance;
        if (steam == null || !SteamManager.LocalPlayerIsLobbyOwner)
            return;

        byte[] packet = EncodeLocalValues();
        foreach (SteamConnection player in steam.connectedPlayers)
        {
            if (player.Connected)
                player.Connection.SendMessage(packet);
        }
    }

    // --- Patches ---

    [HarmonyPatch(typeof(CharacterSelectHandler), nameof(CharacterSelectHandler.TryStartGame_inner))]
    private static class ActivateOnLocalGameStart
    {
        private static void Prefix() => ActivateLocalValues();
    }

    [HarmonyPatch(typeof(CharacterSelectHandler_online), nameof(CharacterSelectHandler_online.ForceStartGame))]
    private static class ActivateOnOnlineGameStart
    {
        private static void Prefix()
        {
            if (SteamManager.LocalPlayerIsLobbyOwner)
                ActivateLocalValues();
            else
                Activate(_hostValues ?? new Dictionary<string, string>(), _hostValues != null ? "host" : "defaults, nothing received from host");
        }
    }

    [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.HostGame))]
    private static class SendBeforeGameStart
    {
        private static void Prefix() => BroadcastIfHost();
    }

    [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.HostNextLevel))]
    private static class SendBeforeNextLevel
    {
        private static void Prefix() => BroadcastIfHost();
    }

    [HarmonyPatch(typeof(SteamConnection), nameof(SteamConnection.OnConnected))]
    private static class SendOnPlayerConnected
    {
        private static void Postfix() => BroadcastIfHost();
    }

    /// <summary>A new lobby: forget the previous host's rules (the new host sends its own when we connect).</summary>
    [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.OnLobbyEnteredCallback))]
    private static class ForgetHostValuesOnNewLobby
    {
        private static void Prefix() => _hostValues = null;
    }

    [HarmonyPatch(typeof(SteamSocket), nameof(SteamSocket.OnMessage))]
    private static class ReceiveFromHost
    {
        /// <returns>False (skip the game's handler) for our packet.</returns>
        private static bool Prefix(ref NetIdentity identity, IntPtr data, int size) => !TryReceive(identity.SteamId, data, size);
    }

    private static bool TryReceive(SteamId sender, IntPtr data, int size)
    {
        if (size < MinPacketSize || size > 64 * 1024)
            return false;

        var bytes = new byte[size];
        Marshal.Copy(data, bytes, 0, size);
        if (!TryDecode(bytes, out Dictionary<string, string> values))
            return false;

        SteamManager steam = SteamManager.instance;
        if (steam != null && sender.IsValid && steam.currentLobby.IsOwnedBy(sender))
        {
            _hostValues = values;
            Plugin.Log.LogInfo($"Received rules from the lobby host ({values.Count} values).");
        }
        return true;
    }
}
