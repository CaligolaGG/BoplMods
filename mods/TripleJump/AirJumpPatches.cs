using System.Runtime.CompilerServices;
using HarmonyLib;

namespace TripleJump;

/// <summary>
/// Vanilla jumping lives in SlimeController.OldUpdate: it only calls Jump() when the player is grounded.
/// We let the original run, then perform an extra PlayerPhysics.Jump() if the player was airborne
/// for the whole frame and pressed jump.
///
/// The game simulation is deterministic lockstep (BoplFixedMath.Fix, no rollback), and this logic depends
/// only on the simulated state and the synced inputs, so every client computes the same air jumps.
/// </summary>
[HarmonyPatch(typeof(SlimeController))]
internal static class AirJumpPatches
{
    private sealed class AirJumpState
    {
        public int Remaining;
    }

    // Weak keys: entries vanish together with the destroyed player object.
    private static readonly ConditionalWeakTable<SlimeController, AirJumpState> States = new();

    private static AirJumpState GetState(SlimeController slime) =>
        States.GetValue(slime, _ => new AirJumpState { Remaining = Plugin.AirJumps.Value });

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
        bool wasGroundedAtFrameStart = __state;
        PlayerPhysics physics = __instance.playerPhysics;
        AirJumpState state = GetState(__instance);

        if (Plugin.DebugLogging.Value)
            LogJumpPress(__instance, physics, state, wasGroundedAtFrameStart);

        if (physics.IsGrounded())
        {
            state.Remaining = Plugin.AirJumps.Value;
            return;
        }

        // Grounded at frame start means vanilla already handled this frame's jump (or we just walked off a ledge).
        if (wasGroundedAtFrameStart || state.Remaining <= 0)
            return;

        if (__instance.suppressInput || __instance.isInAbility)
            return;

        switch (__instance.currentState)
        {
            case SlimeController.State.nothing:
            case SlimeController.State.ability:
            case SlimeController.State.biting:
            case SlimeController.State.ragdoll:
                return;
        }

        Player player = PlayerHandler.Get().GetPlayer(__instance.playerNumber);
        if (player == null || !player.jumpButton_PressedThisFrame())
            return;

        state.Remaining--;
        // SlimeController.Jump() reads the attached ground's normal, which is stale/null in the air,
        // so mirror it manually: an air jump is always upright.
        __instance.wasUpsideDown = false;
        physics.Jump();
        __instance.bodyAnimator.UpdateAnimations();
    }

    private static void LogJumpPress(SlimeController slime, PlayerPhysics physics, AirJumpState state, bool wasGrounded)
    {
        Player player = PlayerHandler.Get().GetPlayer(slime.playerNumber);
        if (player == null || !player.jumpButton_PressedThisFrame())
            return;

        Plugin.Log.LogInfo(
            $"[P{slime.playerNumber}] jump pressed: groundedAtStart={wasGrounded} groundedNow={physics.IsGrounded()} " +
            $"remaining={state.Remaining} state={slime.currentState} suppressInput={slime.suppressInput} " +
            $"inAbility={slime.isInAbility} jumpedThisFrame={physics.jumpedThisFrame} vel={physics.body.selfImposedVelocity}");
    }

    [HarmonyPatch(nameof(SlimeController.Spawn))]
    [HarmonyPostfix]
    private static void SpawnPostfix(SlimeController __instance) => ResetAirJumps(__instance);

    [HarmonyPatch(nameof(SlimeController.ReSpawn))]
    [HarmonyPostfix]
    private static void ReSpawnPostfix(SlimeController __instance) => ResetAirJumps(__instance);

    private static void ResetAirJumps(SlimeController slime)
    {
        GetState(slime).Remaining = Plugin.AirJumps.Value;
    }
}
