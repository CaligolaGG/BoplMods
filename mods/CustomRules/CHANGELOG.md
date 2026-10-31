# Changelog

## 1.0.1
- Abilities window (Rules tab → Abilities): every ability in a grid. Right-click one to change its stats (abilities from mods that offer them, e.g. CaliSkills), saved and used by every player. Middle-click to disable / re-enable it.
- Disabled abilities can't be picked (greyed out, red "no" sign) and Random never gives them, like the offensive filters. The host can also middle-click abilities in the character select grid.
- Abilities with changed stats get a yellow ring in character select and a yellow border in the abilities window.
- Public API for ability mods: `AbilityStats.Register`.
- New rules: Ability spawning time × (time between ability pickups appearing) and Ability cap per round (normally 3).

## 1.0.0
- Custom rules window in character select, set by the lobby host and synced to every player. Other mods can add rules, in their own tinted section.
- Game mode profiles: save, load/switch/unload, overwrite, rename and delete named sets of rules (Profiles tab).
- Rules: cooldown multiplier, player size (3 = normal, works like Growth/Shrink ray hits), gravity, player speed, permanent invisibility, disable ability spawning, infinite round duration (no sudden death), player bouncing mode (+ bounce on everything, bounce power), Extra Jump (+ number of air jumps; replaces the TripleJump mod), no offensive / only offensive abilities (disallowed ones are greyed out in character select), preselect abilities (force an ability per slot).
- Player bouncing: a hard enough hit knocks a slime stuck to a platform off it.
- The cooldown rule also applies to abilities from other mods (e.g. CaliSkills' Run).
- Fix: the rule frames disappeared after leaving a lobby.
