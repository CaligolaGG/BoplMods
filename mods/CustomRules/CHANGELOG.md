# Changelog

## 1.0.2
- For game mode mods: rules can be locked by another rule (greyed out in the menu, "locked by ..." shown, and forced when the match starts), a rule can raise another rule's lowest value while it's on, a rule can ban abilities while it's on, and a rule can be exclusive with several others.
- Player bouncing: new "Player bounce ×" setting for how hard slimes bounce off each other (default 2, stronger than before); "Bounce power ×" is now only for platforms. A slime big enough to eat another isn't bounced away from it anymore, so eating works again.
- Bounce on everything fixes: abilities that need the ground (Spike, Tesla coil...) work again: hold their button and you stick to the next platform you touch to use them. After Roll, Meteor, a ragdoll (gun hits) or anything else that left you stuck to a platform, you bounce off it again instead of staying stuck, and jumping works. Big slimes can eat small ones in the air.
- Abilities window: new "Show hitboxes" checkbox. Ability mods that support it (CaliSkills) draw their hidden hitboxes in red during matches, for testing. Saved and synced like every rule.

## 1.0.1
- Abilities window (Rules tab → Abilities): every ability in a grid. Right-click one to change its stats, saved and used by every player: the game's abilities get multipliers for cooldown, duration, charge time, charge power, knockback, speed and size (where they apply), abilities from mods that offer them (e.g. CaliSkills) their own settings. Middle-click to disable / re-enable it.
- Disabled abilities can't be picked (greyed out, red "no" sign) and Random never gives them, like the offensive filters. The host can also middle-click abilities in the character select grid.
- Abilities with changed stats get a yellow ring in character select and a yellow border in the abilities window.
- New rule: Abilities max use, with Max use amount, Destroy ability on max use and Reroll on max use: each ability slot can be used a limited number of times per round, then it's rerolled into another ability or lost. While it's on, the Black hole is no longer limited to one cast per round.
- New rule: Sudden death time (s), with Infinite round duration. The Abilities row is now the first one of the Rules tab.
- API: `RuleRegistry.AddPreset` (turning a rule on in the menu also sets others), `AbilityStats.AddMenuButton` (a rule's sub row that opens a stats window, e.g. a game mode's ball), `Rule.LobbyText`/`LobbyBool`.
- "Reset to defaults" in the abilities window: every ability's stats back to default and every ability enabled again.
- Public API for ability mods: `AbilityStats.Register`, `AbilityStats.TryGetLobbyValue`. Each stat is shown in its own frame; settings fields with the same `[Category]` share one, under the category's name.
- Replays on/off option in the game's Settings menu: off = matches aren't recorded or saved as replays. The settings menu scrolls when it's taller than the screen.
- New rules: Ability spawning time × (time between ability pickups appearing) and Ability cap per round (normally 3).

## 1.0.0
- Custom rules window in character select, set by the lobby host and synced to every player. Other mods can add rules, in their own tinted section.
- Game mode profiles: save, load/switch/unload, overwrite, rename and delete named sets of rules (Profiles tab).
- Rules: cooldown multiplier, player size (3 = normal, works like Growth/Shrink ray hits), gravity, player speed, permanent invisibility, disable ability spawning, infinite round duration (no sudden death), player bouncing mode (+ bounce on everything, bounce power), Extra Jump (+ number of air jumps; replaces the TripleJump mod), no offensive / only offensive abilities (disallowed ones are greyed out in character select), preselect abilities (force an ability per slot).
- Player bouncing: a hard enough hit knocks a slime stuck to a platform off it.
- The cooldown rule also applies to abilities from other mods (e.g. CaliSkills' Run).
- Fix: the rule frames disappeared after leaving a lobby.
