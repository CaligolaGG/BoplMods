using System.Collections.Generic;
using System.Reflection;
using BoplFixedMath;
using HarmonyLib;

namespace CustomRules.Rules;

/// <summary>
/// Player movement: Move (slimes and most ability forms), MachoMove (Macho) and AirealMovement (called by both, and
/// directly by some forms) read maxSpeed / accel / airAccel. Scale them only for the duration of the outermost call,
/// so the stored values (which some abilities change) are never compounded.
/// </summary>
[HarmonyPatch]
internal static class SpeedRule
{
    private static int _depth;

    internal struct SavedSpeed
    {
        public bool Scaled;
        public Fix MaxSpeed;
        public Fix Accel;
        public Fix AirAccel;
    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(PlayerPhysics), nameof(PlayerPhysics.Move));
        yield return AccessTools.Method(typeof(PlayerPhysics), nameof(PlayerPhysics.MachoMove));
        yield return AccessTools.Method(typeof(PlayerPhysics), nameof(PlayerPhysics.AirealMovement));
    }

    private static void Prefix(PlayerPhysics __instance, out SavedSpeed __state)
    {
        __state = default;
        if (_depth++ > 0)
            return;

        Fix factor = BasicRules.PlayerSpeed.ActiveFix;
        if (factor == Fix.One)
            return;

        __state = new SavedSpeed
        {
            Scaled = true,
            MaxSpeed = __instance.maxSpeed,
            Accel = __instance.accel,
            AirAccel = __instance.airAccel,
        };
        __instance.maxSpeed *= factor;
        __instance.accel *= factor;
        __instance.airAccel *= factor;
    }

    private static void Finalizer(PlayerPhysics __instance, ref SavedSpeed __state)
    {
        _depth--;
        if (!__state.Scaled)
            return;

        __instance.maxSpeed = __state.MaxSpeed;
        __instance.accel = __state.Accel;
        __instance.airAccel = __state.AirAccel;
    }
}
