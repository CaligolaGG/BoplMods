using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BoplFixedMath;
using CustomRules.Core;
using HarmonyLib;
using UnityEngine;

namespace CustomRules.Rules;

/// <summary>
/// Abilities max use: every ability slot can be used <see cref="Amount"/> times per round (0 = never). A use is a press
/// that casts it (holding the button for a reel-in rope or a held ability counts once). Once used up, the slot can't be
/// cast; as soon as its last use is over it's rerolled into another allowed ability, which starts on cooldown
/// (<see cref="Reroll"/>), or lost (never castable, its icon hidden; an ability pickup fills it again). With <see cref="Destroy"/>, an ability a player used up never
/// comes back to them in rerolls, for the rest of the game, and every other slot holding it (now or from a later
/// pickup) is rerolled too, right away and never lost.
/// The Black hole's own once-per-round limit is lifted while the rule is on.
/// Sim code: counts and rerolls use synced inputs, sim state and the game's seeded RNG.
/// </summary>
internal static class AbilityMaxUseRule
{
    internal static Rule Enabled;
    internal static Rule Amount;
    internal static Rule Destroy;
    internal static Rule Reroll;

    private sealed class Slots
    {
        public AbilityMonoBehaviour[] Ability = new AbilityMonoBehaviour[0];
        public int[] Uses = new int[0];
        public bool[] Held = new bool[0];
        public bool[] Lost = new bool[0];

        public void Fit(int count)
        {
            if (Uses.Length >= count)
                return;
            System.Array.Resize(ref Ability, count);
            System.Array.Resize(ref Uses, count);
            System.Array.Resize(ref Held, count);
            System.Array.Resize(ref Lost, count);
        }
    }

    private static readonly ConditionalWeakTable<SlimeController, Slots> State = [];

    /// <summary>Seconds after the last use before the slot is swapped (lets instant abilities finish).</summary>
    private static readonly Fix SwapDelay = (Fix)0.25f;

    /// <summary>Per player id: abilities (list indices) used up with <see cref="Destroy"/> on, until back in the lobby.</summary>
    private static readonly Dictionary<int, HashSet<int>> UsedUp = [];

    internal static void Register()
    {
        Enabled = RuleRegistry.AddBool("AbilityMaxUse", "Abilities max use", false,
            "Each ability slot can only be used a limited number of times per round; then it's rerolled or lost.");
        Amount = RuleRegistry.AddInt("AbilityMaxUse.Amount", "Max use amount", 2, 0, 100,
            "Uses per ability slot each round (0 = abilities can't be used at all).", parent: Enabled);
        Destroy = RuleRegistry.AddBool("AbilityMaxUse.Destroy", "Destroy ability on max use", false,
            "An ability a player used up is gone for them for the rest of the game: never in their rerolls, and their other slots " +
            "holding it are rerolled too (never lost).", parent: Enabled);
        Reroll = RuleRegistry.AddBool("AbilityMaxUse.Reroll", "Reroll on max use", true,
            "A used-up slot gets a new random ability instead of being lost.", parent: Enabled);

        // A new game (match start from the lobby): used-up abilities are allowed again.
        RuleRegistry.RulesActivated += UsedUp.Clear;
    }

    /// <summary>
    /// Whether the slime still has an ability it can use now or later: a slot that isn't lost (a used-up one being
    /// rerolled counts). For game modes (Rotation).
    /// </summary>
    internal static bool HasAbilitiesLeft(SlimeController slime)
    {
        if (slime == null || slime.IsDestroyed || slime.abilities == null)
            return false;
        Slots slots = StateOf(slime);
        for (int i = 0; i < slime.abilities.Count; i++)
        {
            AbilityMonoBehaviour ability = slime.abilities[i];
            if (!slots.Lost[i] && ability != null && !ability.IsDestroyed)
                return true;
        }
        return false;
    }

    /// <summary>Every player gets their used-up abilities back (Destroy): for game modes that start fresh every round.</summary>
    internal static void ForgetUsedUp() => UsedUp.Clear();

    private static Slots StateOf(SlimeController slime)
    {
        Slots slots = State.GetOrCreateValue(slime);
        slots.Fit(slime.abilities.Count);
        return slots;
    }

    /// <summary>Used up (or lost, or abilities unusable): it can't be cast.</summary>
    [HarmonyPatch(typeof(SlimeController), nameof(SlimeController.isAbilityCastable))]
    private static class BlockUsedUp
    {
        private static bool Prefix(SlimeController __instance, int abilityIndex, ref bool __result)
        {
            if (!Enabled.IsOn)
                return true;
            Slots slots = StateOf(__instance);
            if (Amount.ActiveInt > 0 && !slots.Lost[abilityIndex] && slots.Uses[abilityIndex] < Amount.ActiveInt)
                return true;
            __result = false;
            return false;
        }
    }

    /// <summary>A cast: counts once per press.</summary>
    [HarmonyPatch(typeof(SlimeController), nameof(SlimeController.EnterAbility))]
    private static class CountUses
    {
        private static void Postfix(SlimeController __instance, int abilityIndex)
        {
            if (!Enabled.IsOn || abilityIndex >= __instance.abilities.Count)
                return;
            AbilityMonoBehaviour ability = __instance.abilities[abilityIndex];
            if (ability == null || ability.IsDestroyed)
                return;
            Slots slots = StateOf(__instance);
            if (slots.Held[abilityIndex])
                return;
            slots.Held[abilityIndex] = true;
            slots.Uses[abilityIndex]++;
        }
    }

    /// <summary>Every tick: presses end on release, new abilities (pickups, rerolls) start at 0, used-up slots go.</summary>
    [HarmonyPatch(typeof(SlimeController), nameof(SlimeController.OldUpdate))]
    private static class UpdateSlots
    {
        private static void Postfix(SlimeController __instance)
        {
            if (!Enabled.IsOn || __instance.IsDestroyed)
                return;
            Player player = PlayerHandler.Get().GetPlayer(__instance.playerNumber);
            if (player == null)
                return;

            Slots slots = StateOf(__instance);
            int max = Amount.ActiveInt;
            for (int i = 0; i < __instance.abilities.Count; i++)
            {
                AbilityMonoBehaviour ability = __instance.abilities[i];
                if (ability != slots.Ability[i])
                {
                    slots.Ability[i] = ability;
                    slots.Uses[i] = 0;
                    slots.Held[i] = false;
                    slots.Lost[i] = false;
                }
                if (!player.AbilityButtonIsDown(i))
                    slots.Held[i] = false;

                // Leaving an ability form shows every slot's icon again.
                if (slots.Lost[i])
                    HideIcon(__instance, i);

                if (slots.Lost[i] || ability == null || ability.IsDestroyed)
                    continue;
                bool usedUp = max > 0 && slots.Uses[i] >= max;
                // Destroyed (used up by this player with Destroy on): every copy goes too, even ones picked up later.
                bool destroyed = !usedUp && Destroy.ActiveBool && UsedUp.TryGetValue(player.Id, out HashSet<int> gone) &&
                                 gone.Contains(SlotAbility(__instance, i));
                if (!usedUp && !destroyed)
                    continue;
                // Right after the last use, once it's over: out of its form, button released, and a moment after the
                // cast (instant abilities such as a dash or a bat swing still run then; the Bat and Run keep their
                // timer at 0 while they do). A copy that wasn't just used goes at once.
                if (__instance.isInAbility || slots.Held[i] ||
                    __instance.abilityCooldownTimers[i] < Fix.Min(SwapDelay, ability.GetCooldown()))
                    continue;
                if (usedUp)
                    UseUp(__instance, player, slots, i);
                else
                    RerollCopy(__instance, player, slots, i);
            }
        }
    }

    /// <summary>The slot's ability as an index in the ability list (from its icon), -1 if unknown.</summary>
    private static int SlotAbility(SlimeController slime, int slot)
    {
        NamedSpriteList list = AbilityList(slime);
        return list != null && slot < slime.AbilityReadyIndicators.Length && slime.AbilityReadyIndicators[slot] != null
            ? list.IndexOf(slime.AbilityReadyIndicators[slot].GetPrimarySprite())
            : -1;
    }

    /// <summary>A new ability for the slot: allowed by the rules, not the one it has, not one this player destroyed.</summary>
    private static bool TryReroll(SlimeController slime, Player player, Slots slots, int slot, int current, HashSet<int> usedUp)
    {
        NamedSpriteList list = AbilityList(slime);
        if (list == null)
            return false;
        List<int> candidates = AbilityRules.RerollCandidates(list);
        candidates.RemoveAll(i => usedUp.Contains(i) || i == current);
        if (candidates.Count == 0)
            return false;
        Replace(slime, player, slots, slot, list.sprites[candidates[Updater.RandomInt(0, candidates.Count)]]);
        return true;
    }

    /// <summary>
    /// Another copy of a destroyed ability: always rerolled (whatever Reroll on max use says), never lost. If no other
    /// ability is left to give, it keeps the copy.
    /// </summary>
    private static void RerollCopy(SlimeController slime, Player player, Slots slots, int slot) =>
        TryReroll(slime, player, slots, slot, SlotAbility(slime, slot), UsedUp[player.Id]);

    private static void UseUp(SlimeController slime, Player player, Slots slots, int slot)
    {
        int current = SlotAbility(slime, slot);
        if (!UsedUp.TryGetValue(player.Id, out HashSet<int> usedUp))
            UsedUp[player.Id] = usedUp = new HashSet<int>();
        if (Destroy.ActiveBool && current >= 0)
            usedUp.Add(current);

        if (Reroll.ActiveBool && TryReroll(slime, player, slots, slot, current, usedUp))
            return;
        Lose(slime, slots, slot);
    }

    private static NamedSpriteList AbilityList(SlimeController slime)
    {
        bool full = SteamManager.instance == null || SteamManager.instance.dlc.HasDLC();
        return full && slime.abilityIconsFull != null ? slime.abilityIconsFull : slime.abilityIconsDemo;
    }

    /// <summary>Like an ability pickup into that slot, but the new ability starts its cooldown (as if just used).</summary>
    private static void Replace(SlimeController slime, Player player, Slots slots, int slot, NamedSprite entry)
    {
        GameObject created = FixTransform.InstantiateFixed(entry.associatedGameObject, Vec2.zero);
        created.SetActive(false);
        AbilityMonoBehaviour ability = created.GetComponent<AbilityMonoBehaviour>();
        AbilityMonoBehaviour old = slime.abilities[slot];
        if (old != null && !old.IsDestroyed)
            Updater.DestroyFix(old.gameObject);

        slime.abilities[slot] = ability;
        if (player.CurrentAbilities != null && slot < player.CurrentAbilities.Count)
            player.CurrentAbilities[slot] = created;
        AbilityReadyIndicator indicator = slot < slime.AbilityReadyIndicators.Length ? slime.AbilityReadyIndicators[slot] : null;
        if (indicator != null)
        {
            indicator.SetSprite(entry.sprite);
            indicator.ResetAnimation();
        }
        slime.abilityCooldownTimers[slot] = Fix.Zero;
        slots.Ability[slot] = ability;
        slots.Uses[slot] = 0;
        slots.Held[slot] = false;
        AudioManager.Get()?.Play("abilityPickup");
    }

    /// <summary>
    /// The slot is empty from now on (until a pickup fills it): never castable, icon hidden. The ability object stays
    /// (inactive): the game and other patches call into every slot's ability, a destroyed one would throw.
    /// </summary>
    private static void Lose(SlimeController slime, Slots slots, int slot)
    {
        slots.Lost[slot] = true;
        HideIcon(slime, slot);
    }

    private static void HideIcon(SlimeController slime, int slot)
    {
        if (slot < slime.AbilityReadyIndicators.Length && slime.AbilityReadyIndicators[slot] != null &&
            slime.AbilityReadyIndicators[slot].gameObject.activeSelf)
            slime.AbilityReadyIndicators[slot].gameObject.SetActive(false);
    }

    /// <summary>
    /// The Black hole can normally be cast once per round (BlackHoleClap turns its Ability's isCastable off when it
    /// fires). With max use on, the max use amount is the limit instead.
    /// </summary>
    [HarmonyPatch(typeof(BlackHoleClap), nameof(BlackHoleClap.FireCommon))]
    private static class BlackHoleNotOnce
    {
        private static void Postfix(BlackHoleClap __instance)
        {
            if (Enabled.IsOn && __instance.ability != null)
                __instance.ability.isCastable = true;
        }
    }
}
