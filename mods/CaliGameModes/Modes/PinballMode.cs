using System.Globalization;
using System.Linq;
using System.Reflection;
using BoplFixedMath;
using CaliSkills.Abilities.Pinball;
using CustomRules.Core;
using HarmonyLib;
using UnityEngine;

namespace CaliGameModes.Modes;

/// <summary>
/// Pinball mode: every few seconds a pinball is shot into the arena from the left or right edge.
/// The balls are the CaliSkills Pinball (fully charged, owned by nobody, so they knock every player). Every Pinball
/// setting is synced from the host with the rules, so the spawned balls (and the Pinball ability) behave like the
/// host's Pinball on every machine.
/// </summary>
internal static class PinballMode
{
    internal static Rule Enabled;
    internal static Rule SpawnInterval;

    internal static void Register()
    {
        Enabled = RuleRegistry.AddBool("PinballMode", "Pinball mode", false,
            "Pinballs are shot into the arena from the sides of the screen. They use the host's Pinball ability settings.",
            section: Plugin.Section);
        SpawnInterval = RuleRegistry.AddFloat("PinballMode.SpawnInterval", "Spawn timing (s)", 1f, 0.25f, 60f,
            "Seconds between two pinballs.", parent: Enabled);

        foreach (FieldInfo field in typeof(PinballSettings).GetFields(BindingFlags.Public | BindingFlags.Static)
                     .Where(f => !f.IsInitOnly && !f.IsLiteral))
        {
            RuleRegistry.AddSyncedValue("Pinball." + field.Name,
                () => Format(field.GetValue(null)),
                text => SetField(field, text));
        }

        // Re-bake the values the Pinball keeps on its prefabs (cooldown, size, gravity, bounce) after syncing.
        RuleRegistry.RulesActivated += () => PinballSkill.Instance?.OnSettingsChanged();
    }

    private static string Format(object value) => value switch
    {
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        int n => n.ToString(CultureInfo.InvariantCulture),
        _ => value?.ToString() ?? "",
    };

    private static void SetField(FieldInfo field, string text)
    {
        if (field.FieldType == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            field.SetValue(null, f);
        else if (field.FieldType == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            field.SetValue(null, n);
        else if (field.FieldType == typeof(bool) && bool.TryParse(text, out bool b))
            field.SetValue(null, b);
    }

    /// <summary>Every level, on every machine: add the spawner (a scene object, gone with the level).</summary>
    [HarmonyPatch(typeof(GameSessionHandler), nameof(GameSessionHandler.SpawnPlayers))]
    private static class AddSpawnerOnLevelStart
    {
        private static void Postfix()
        {
            if (Enabled.IsOn && PinballSkill.Instance != null)
                new GameObject("CaliGameModes.PinballSpawner").AddComponent<PinballSpawner>();
        }
    }
}

/// <summary>Shoots a pinball from a random side every <see cref="PinballMode.SpawnInterval"/> seconds (sim time).</summary>
internal sealed class PinballSpawner : MonoUpdatable
{
    // Spawn area, as fractions of the playable area (camera width, water/floor to camera top):
    // a quarter of the half-width in from the side (~24 units), between 25% and 55% of the height.
    private static readonly Fix EdgeInset01 = (Fix)0.25f;
    private static readonly Fix MinHeight01 = (Fix)0.25f;
    private static readonly Fix MaxHeight01 = (Fix)0.55f;

    // Launch direction: up/horizontal ratio, i.e. 0.5 -> ~27° to 2-> ~63° above horizontal, towards the center.
    private static readonly Fix MinUpward = (Fix)0.8f;
    private static readonly Fix MaxUpward = (Fix)2L;

    // Sky shots: 1 in SkyShotOneIn balls drops from near the top (at 90% of the height, anywhere between the side
    // spawn points), aimed downwards up to ~60° either side of straight down (sideways/down ratio up to 1.7).
    private const int SkyShotOneIn = 4;
    private static readonly Fix SkyHeight01 = (Fix)0.9f;
    private static readonly Fix MaxSkySideways = (Fix)1.7f;

    // Every ball is randomly 10% to 40% faster than the host's Pinball speed.
    private static readonly Fix MinSpeedMultiplier = (Fix)1.1f;
    private static readonly Fix MaxSpeedMultiplier = (Fix)1.4f;

    private Fix _timer;

    private void Awake() => Updater.RegisterUpdatable(this);

    public override void Init()
    {
    }

    public override void UpdateSim(Fix simDeltaTime)
    {
        // Same clock as the balls, so time stop pauses the spawning too.
        _timer += simDeltaTime * GameTime.PlayerTimeScale;
        Fix interval = PinballMode.SpawnInterval.ActiveFix;
        if (_timer < interval)
            return;
        _timer -= interval;

        // Game RNG (seeded identically everywhere), never UnityEngine.Random: the spawn must match on every machine.
        Fix bottom = Fix.Max(SceneBounds.WaterHeight, SceneBounds.Camera_YMin);
        Fix height = SceneBounds.Camera_YMax - bottom;
        Fix inset = (SceneBounds.Camera_XMax - SceneBounds.Camera_XMin) * EdgeInset01 / (Fix)2L;
        Fix left = SceneBounds.Camera_XMin + inset;
        Fix right = SceneBounds.Camera_XMax - inset;

        Vec2 position, dir;
        if (Updater.RandomInt(0, SkyShotOneIn) == 0)
        {
            position = new Vec2(Updater.RandomFix(left, right), bottom + height * SkyHeight01);
            dir = new Vec2(Updater.RandomFix(-MaxSkySideways, MaxSkySideways), -Fix.One);
        }
        else
        {
            bool fromLeft = Updater.RandomInt(0, 2) == 0;
            position = new Vec2(fromLeft ? left : right, bottom + height * Updater.RandomFix(MinHeight01, MaxHeight01));
            dir = new Vec2(fromLeft ? Fix.One : -Fix.One, Updater.RandomFix(MinUpward, MaxUpward));
        }

        Fix speedMultiplier = Updater.RandomFix(MinSpeedMultiplier, MaxSpeedMultiplier);
        PinballSkill.Instance?.SpawnBall(position, dir, speedMultiplier);
    }
}
