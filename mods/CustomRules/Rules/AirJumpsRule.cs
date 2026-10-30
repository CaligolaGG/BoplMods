using System.Runtime.CompilerServices;
using HarmonyLib;

namespace CustomRules.Rules;

/// <summary>
/// Extra jump (formerly the TripleJump mod): extra jumps in mid-air. Vanilla jumping lives in
/// SlimeController.OldUpdate and only calls Jump() when the player is grounded. We let the original run, then
/// perform an extra PlayerPhysics.Jump() if the player was airborne for the whole frame and pressed jump.
/// Air jumps come back when the slime lands, (re)spawns, or bounces off a platform ("bounce on everything").
/// </summary>
[HarmonyPatch(typeof(SlimeController))]
internal static class AirJumpsRule
{
    private sealed class AirJumpState
    {
        public int Remaining;
    }

    // Weak keys: entries vanish together with the destroyed slime.
    private static readonly ConditionalWeakTable<SlimeController, AirJumpState> States = new();

    private static int MaxAirJumps => BasicRules.ExtraJump.IsOn ? BasicRules.AirJumps.ActiveInt : 0;

    private static AirJumpState GetState(SlimeController slime) =>
        States.GetValue(slime, _ => new AirJumpState { Remaining = MaxAirJumps });

    internal static void ResetAirJumps(SlimeController slime) => GetState(slime).Remaining = MaxAirJumps;

    [HarmonyPatch(nameof(SlimeController.OldUpdate))]
    [HarmonyPrefix]
    private static void OldUpdatePrefix(SlimeController __instance, out bool __state)
    {
        __state = __instance.playerPhysics.IsGrounded();
    }

    [HarmonyPatch(nameof(SlimeController.OldUpdate))]
    [HarmonyPostfix]
    private static void OldUpdatePostfix(SlimeController __instance, bool __state)
    {
        if (!BasicRules.ExtraJump.IsOn)
            return;

        PlayerPhysics physics = __instance.playerPhysics;
        AirJumpState state = GetState(__instance);
        if (physics.IsGrounded())
        {
            state.Remaining = MaxAirJumps;
            return;
        }

        // Grounded at frame start means vanilla already handled this frame's jump (or we just walked off a ledge).
        // jumpedThisFrame: another mod rule (e.g. the bounce jump) already jumped on this press.
        if (__state || state.Remaining <= 0 || physics.jumpedThisFrame)
            return;
        if (!CanAirJump(__instance))
            return;

        Player player = PlayerHandler.Get().GetPlayer(__instance.playerNumber);
        if (player == null || !player.jumpButton_PressedThisFrame())
            return;

        state.Remaining--;
        AirJump(__instance);
    }

    /// <summary>States in which a slime controls itself and may jump in the air.</summary>
    internal static bool CanAirJump(SlimeController slime)
    {
        if (slime.suppressInput || slime.isInAbility)
            return false;
        switch (slime.currentState)
        {
            case SlimeController.State.nothing:
            case SlimeController.State.ability:
            case SlimeController.State.biting:
            case SlimeController.State.ragdoll:
                return false;
            default:
                return true;
        }
    }

    /// <summary>An upright jump in mid-air.</summary>
    internal static void AirJump(SlimeController slime)
    {
        // SlimeController.Jump() reads the attached ground's normal, which is null in the air: mirror it manually.
        slime.wasUpsideDown = false;
        slime.playerPhysics.Jump();
        slime.bodyAnimator.UpdateAnimations();
    }

    [HarmonyPatch(nameof(SlimeController.Spawn))]
    [HarmonyPostfix]
    private static void SpawnPostfix(SlimeController __instance) => ResetAirJumps(__instance);

    [HarmonyPatch(nameof(SlimeController.ReSpawn))]
    [HarmonyPostfix]
    private static void ReSpawnPostfix(SlimeController __instance) => ResetAirJumps(__instance);
}
