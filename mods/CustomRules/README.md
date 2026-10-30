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
| Infinite round duration | on/off (no sudden death: platforms never fall) | off |
| Player bouncing mode | on/off: slimes bounce off each other instead of passing through | off |
| ↳ Bounce on everything | on/off: slimes bounce off platforms instead of sticking; each bounce gives back one jump (press jump in the air) | off |
| ↳ Bounce power × | 0–5 | 1 |
| Extra Jump | on/off: jump again in mid-air (replaces the ExtraJump mod: uninstall it) | off |
| ↳ Air jumps | 1–10 | 2 |

## Notes

- **Every player in the lobby needs this mod** (and the same rule mods, e.g. CaliGameModes), otherwise the game desyncs.
- Replays of matches with custom rules don't play back correctly.
- Other mods can add their own rules to the window (see CaliGameModes).
