using BoplFixedMath;
using HarmonyLib;

namespace CustomRules.Rules;

/// <summary>
/// Players (and every ability form, each with its own PlayerPhysics): gravity is added in AddGravityFactor using
/// gravity_modifier, which abilities also set themselves (e.g. the Grenade throw resets it to 1). Scale it only
/// for the duration of the call. Terminal fall speed stays the vanilla one.
/// </summary>
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.AddGravityFactor))]
internal static class GravityRule
{
    private static void Prefix(PlayerPhysics __instance, out Fix __state)
    {
        __state = __instance.gravity_modifier;
        __instance.gravity_modifier *= BasicRules.Gravity.ActiveFix;
    }

    private static void Finalizer(PlayerPhysics __instance, Fix __state) => __instance.gravity_modifier = __state;
}

/// <summary>Objects (arrows, grenades, boulders...): the physics engine's global gravity, set at every level start.</summary>
[HarmonyPatch(typeof(GameSessionHandler), nameof(GameSessionHandler.SpawnPlayers))]
internal static class ObjectGravityRule
{
    private static Fix? _vanillaGravity;

    private static void Postfix()
    {
        DetPhysics physics = DetPhysics.Get();
        if (physics == null)
            return;

        _vanillaGravity ??= physics.gravityAccel;
        physics.gravityAccel = _vanillaGravity.Value * BasicRules.Gravity.ActiveFix;
    }
}
