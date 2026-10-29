# CustomRules

Play matches with custom rules. The lobby host picks them and they apply to every player.

## How to use

In character select (local or online lobby), click **Custom rules** in the top-left corner.
- Local games: your rules are used.
- Online: the host's rules are used. Other players can open the window to see them (read-only).
- Rules are saved between sessions (`BepInEx/config/com.caligola.customrules.cfg`).

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

## Notes

- **Every player in the lobby needs this mod** (and the same rule mods, e.g. CaliGameModes), otherwise the game desyncs.
- Replays of matches with custom rules don't play back correctly.
- Other mods can add their own rules to the window (see CaliGameModes).
