# CustomRules

Play matches with custom rules. The lobby host picks them and they apply to every player.

## How to use

In character select (local or online lobby), click **Custom rules** in the top-left corner.
- Local games: your rules are used.
- Online: the host's rules are used. Other players can open the window to see them (read-only).
- Rules are saved between sessions (`BepInEx/config/com.caligola.customrules.cfg`).

## Game mode profiles

The **Profiles** tab of the window saves sets of rules as named profiles (e.g. "Low gravity chaos"):
- **Save** the current rules as a new profile, or **Overwrite** an existing one with them.
- **Load** a profile to use its rules, **Switch** to another, or **Unload** to get back the rules you had before loading.
- To edit a profile: load it, change the rules in the Rules tab, then press **Save to 'profile'** there.
- **Rename** and **Delete** (click twice to confirm).

Profiles are files in `BepInEx/config/CustomRules.Profiles/`, so you can share them with friends.

## Rules

| Rule | Values | Default |
|---|---|---|
| Cooldowns × | 0–10 (2 = double, 0.5 = half, 0 = no cooldown) | 1 |
| Player size | 1–5 (3 = normal; each step up/down = one Growth ray / Shrink ray hit at round start) | 3 |
| Gravity × | 0–5, players and objects | 1 |
| Player speed × | 0.1–5 | 1 |
| Permanent invisibility | on/off | off |
| Disable ability spawning | on/off (no pickups during the round, none dropped on death) | off |
| Ability spawning time × | 0.1–10: time between ability pickups appearing (normally 40 s; 0.5 = twice as often, 2 = half as often) | 1 |
| Ability cap per round | 1–100: how many ability pickups can appear in a round | 3 |
| Infinite round duration | on/off (no sudden death: platforms never fall) | off |
| Player bouncing mode | on/off: slimes bounce off each other instead of passing through | off |
| ↳ Bounce on everything | on/off: slimes bounce off platforms instead of sticking; each bounce gives back one jump (press jump in the air) | off |
| ↳ Bounce power × | 0–5 | 1 |
| No offensive abilities | on/off: offensive abilities are greyed out and can't be picked (a ready player holding one is sent back to pick again, and the match can't start); Random (and pickups) only give non-offensive ones | off |
| Only offensive abilities | on/off: the opposite (can't be on together with No offensive) | off |
| **Preselect abilities** → Ability 1 / 2 / 3 | pick an ability (or Random) to force it on every player in that slot: ◀ ▶ step through them, click the icon to choose from a grid | Player's choice |
| Extra Jump | on/off: jump again in mid-air (replaces the ExtraJump mod: uninstall it) | off |
| ↳ Air jumps | 1–10 | 2 |

## Abilities

**Rules tab → Abilities** opens a grid of every ability:
- **Right-click** an ability to change its stats (for abilities from mods that offer them, e.g. CaliSkills: speed, cooldown...). Every player uses the host's values. Abilities with changed stats get a **yellow border** (a yellow ring in the character select grid).
- **Middle-click** an ability to **disable** it (red "no" sign) or enable it again. Disabled abilities can't be picked and Random never gives them. The host can also middle-click abilities directly in the character select grid.
- Press **Apply** to save. Stats and disabled abilities are saved in profiles too.

## Notes

- **Every player in the lobby needs this mod** (and the same rule mods, e.g. CaliGameModes), otherwise the game desyncs.
- Replays of matches with custom rules don't play back correctly.
- Other mods can add their own rules to the window (see CaliGameModes), and ability mods can offer stats (`AbilityStats.Register`, see CaliSkills).
