using BepInEx.Configuration;
using HarmonyLib;

namespace CustomRules.Core;

/// <summary>
/// "Replays" option in the game's settings menu (<see cref="UI.ReplaysSettingRow"/>): when off, matches aren't recorded
/// or saved to the replays folder. A setting of this machine (not a match rule), saved in the config ([Replays] Save).
///
/// The game's own switch (<c>Host.recordReplay</c>) is left alone: local games also pick their random seed in the
/// block it guards (Updater.Update). Instead, local recording (<c>Host.RecordReplayFrame</c>) and saving
/// (<c>Host.SaveReplay</c>) are skipped. Online games still collect inputs in memory, dropped instead of saved.
/// </summary>
internal static class ReplaySaving
{
    private static ConfigEntry<bool> _save;

    internal static bool Enabled
    {
        get => _save == null || _save.Value;
        set => _save.Value = value;
    }

    internal static void Init()
    {
        _save = Plugin.RulesConfig.Bind("Replays", "Save", true,
            "Record matches and save them as replays (BoplBattle/replays). Also in the game's settings menu.");
    }

    [HarmonyPatch(typeof(Host), nameof(Host.RecordReplayFrame))]
    private static class SkipRecording
    {
        private static bool Prefix() => Enabled;
    }

    [HarmonyPatch(typeof(Host), nameof(Host.SaveReplay))]
    private static class SkipSaving
    {
        private static bool Prefix(Host __instance)
        {
            if (Enabled)
                return true;
            // What SaveReplay does besides writing the file.
            Updater.gameHasStopped = true;
            __instance.inputRecording.Clear();
            return false;
        }
    }
}
