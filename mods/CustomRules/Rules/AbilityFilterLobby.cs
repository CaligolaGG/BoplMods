using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CustomRules.Rules;

/// <summary>
/// "No / Only offensive abilities" in character select: abilities the rule doesn't allow are greyed out and can't be
/// picked (clicking or confirming one in the grid is refused, the slot arrows skip them). A player whose slot still
/// holds one (picked before the host changed the rule) can't get ready, is un-readied if already ready, and the match
/// can't start while anyone holds one.
/// Uses the rules as they are in the lobby: the host's for online guests, this machine's otherwise. Random is always
/// allowed (its draws are filtered in the match, see <see cref="AbilityRules"/>).
/// </summary>
internal static class AbilityFilterLobby
{
    private static readonly Color Disallowed = new(0.35f, 0.35f, 0.35f, 0.55f);

    /// <summary>Whether entry <paramref name="index"/> of <paramref name="list"/> can be picked right now.</summary>
    internal static bool CanPick(NamedSpriteList list, int index)
    {
        if (list == null || index < 0 || index >= list.sprites.Count)
            return true;
        return AbilityRules.Allowed(list.sprites[index], AbilityRules.LobbyNoOffensive, AbilityRules.LobbyOnlyOffensive);
    }

    /// <summary>Greys out the grid entries that can't be picked.</summary>
    [HarmonyPatch(typeof(AbilityGrid), nameof(AbilityGrid.Update))]
    private static class GreyOutGrid
    {
        private static void Postfix(AbilityGrid __instance)
        {
            if (__instance.grid == null)
                return;
            for (int k = 0; k < __instance.grid.Length; k++)
            {
                Image icon = __instance.grid[k] != null ? __instance.grid[k].iconRenderer : null;
                if (icon != null)
                    icon.color = CanPick(__instance.abilityIcons, k + 1) ? Color.white : Disallowed;
            }
        }
    }

    /// <summary>Picking a greyed-out ability in the grid (click or confirm) is refused; the grid stays open.</summary>
    [HarmonyPatch(typeof(CharacterSelectBox), nameof(CharacterSelectBox.CloseAbilityGrid))]
    private static class RefuseDisallowedPick
    {
        private static bool Prefix(CharacterSelectBox __instance, int abilityChoice)
        {
            if (CanPick(__instance.abilityGrid.abilityIcons, abilityChoice))
                return true;
            AudioManager.Get()?.Play("decline");
            return false;
        }
    }

    /// <summary>The slot's ◀ ▶ (left/right) skip abilities that can't be picked.</summary>
    [HarmonyPatch(typeof(SelectAbility), nameof(SelectAbility.Move))]
    private static class SkipDisallowedOnMove
    {
        private static bool Prefix(SelectAbility __instance, bool moveRight)
        {
            if (!AbilityRules.LobbyFilterActive || __instance.Skip || __instance.csb.menuState == CharSelectMenu.abilityGrid)
                return true;

            int count = __instance.abilityIcons.sprites.Count;
            int step = moveRight ? 1 : -1;
            int index = __instance.SelectedIndex;
            // Index 0 is the empty slot (Select maps it to Random or the last one): skip it like vanilla effectively does.
            for (int i = 0; i < count; i++)
            {
                index = (index + step + count) % count;
                if (index != 0 && CanPick(__instance.abilityIcons, index))
                    break;
            }

            __instance.animateCircle.Animate();
            __instance.Select(index);
            AudioManager.Get()?.Play("navigateMenu2");
            return false;
        }
    }

    /// <summary>A slot holding an ability that can't be picked is shown greyed out.</summary>
    [HarmonyPatch(typeof(SelectAbility), nameof(SelectAbility.Update))]
    private static class GreyOutSlot
    {
        private static void Postfix(SelectAbility __instance)
        {
            if (__instance.abilityIconRenderer != null)
                __instance.abilityIconRenderer.color =
                    __instance.Skip || CanPick(__instance.abilityIcons, __instance.SelectedIndex) ? Color.white : Disallowed;
        }
    }

    /// <summary>Whether one of the box's ability slots holds an ability that can't be picked.</summary>
    private static bool HoldsDisallowed(CharacterSelectBox box)
    {
        foreach (SelectableMonoBehaviour selectable in box.selectables)
        {
            if (selectable is SelectAbility slot && !slot.Skip && !CanPick(slot.abilityIcons, slot.SelectedIndex))
                return true;
        }
        return false;
    }

    /// <summary>
    /// A ready player whose abilities aren't allowed anymore (the host changed the rule) goes back to picking abilities.
    /// Checked every frame, so it also catches the host's rules arriving.
    /// </summary>
    [HarmonyPatch(typeof(CharacterSelectBox), nameof(CharacterSelectBox.Update))]
    private static class UnreadyWithDisallowed
    {
        private static void Postfix(CharacterSelectBox __instance)
        {
            CharSelectMenu state = __instance.menuState;
            if ((state != CharSelectMenu.ready && state != CharSelectMenu.trueReady) || !AbilityRules.LobbyFilterActive ||
                !HoldsDisallowed(__instance))
                return;

            // Same as backing out of ready with Esc.
            if (state == CharSelectMenu.trueReady)
                CharacterSelectHandler_online.CancelLocalReady();
            __instance.OnEnterSelect();
            AudioManager.Get()?.Play("decline");
        }
    }

    /// <summary>Local games: no start while a ready player holds an ability that isn't allowed.</summary>
    [HarmonyPatch(typeof(CharacterSelectHandler), nameof(CharacterSelectHandler.TryStartGame_inner))]
    private static class BlockLocalStart
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(CharacterSelectHandler __instance)
        {
            if (!AbilityRules.LobbyFilterActive)
                return true;
            foreach (CharacterSelectBox box in __instance.characterSelectBoxes)
            {
                if (box != null && box.menuState == CharSelectMenu.ready && HoldsDisallowed(box))
                {
                    AudioManager.Get()?.Play("decline");
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Online host: no start while anyone in the lobby holds an ability that isn't allowed (their own client un-readies
    /// them, but a start could beat that). Remote players' picks are the ability indices they sent with "ready".
    /// </summary>
    [HarmonyPatch(typeof(CharacterSelectHandler_online), nameof(CharacterSelectHandler_online.TryStartGame))]
    private static class BlockOnlineStart
    {
        private static bool Prefix()
        {
            if (!SteamManager.LocalPlayerIsLobbyOwner || !AbilityRules.LobbyFilterActive)
                return true;

            CharacterSelectHandler_online handler = CharacterSelectHandler_online.selfRef;
            bool blocked = handler != null && handler.characterSelectBox != null && HoldsDisallowed(handler.characterSelectBox);

            NamedSpriteList list = SteamManager.instance.abilityIcons;
            int abilities = Settings.Get() != null ? Settings.Get().NumberOfAbilities : 3;
            foreach (SteamConnection player in SteamManager.instance.connectedPlayers)
            {
                if (!player.Connected)
                    continue;
                byte[] picks = { player.lobby_ability1, player.lobby_ability2, player.lobby_ability3 };
                for (int i = 0; i < abilities && i < picks.Length; i++)
                    blocked |= !CanPick(list, picks[i]);
            }

            if (blocked)
                AudioManager.Get()?.Play("decline");
            return !blocked;
        }
    }

    /// <summary>Can't get ready with an ability that isn't allowed.</summary>
    [HarmonyPatch(typeof(CharacterSelectBox), nameof(CharacterSelectBox.OnEnterReady))]
    private static class BlockReadyWithDisallowed
    {
        private static bool Prefix(CharacterSelectBox __instance)
        {
            if (!AbilityRules.LobbyFilterActive || __instance.menuState != CharSelectMenu.select || !HoldsDisallowed(__instance))
                return true;
            AudioManager.Get()?.Play("decline");
            return false;
        }
    }
}
