using HarmonyLib;

namespace CustomRules.Rules;

/// <summary>
/// After GameSessionHandler.TimeBeforeSuddenDeath (120 s) the round goes into sudden death: platforms start falling
/// (AnimateVelocity and others watch GameSessionHandler.SuddenDeathInProgress) and the music shifts.
/// Never turning it on removes all of that.
/// </summary>
[HarmonyPatch(typeof(GameSessionHandler), nameof(GameSessionHandler.ActivateSuddenDeath))]
internal static class InfiniteRoundRule
{
    private static bool Prefix(bool activate) => !(activate && BasicRules.InfiniteRound.ActiveBool);
}
