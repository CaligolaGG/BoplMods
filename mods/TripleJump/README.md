# Triple Jump

Jump up to two extra times while airborne. Air jumps refill when you land or respawn.

## Configuration

After the first launch, edit `BepInEx/config/com.boplmods.triplejump.cfg` (in the mod manager: **Config editor**):

| Setting | Default | Description |
|---|---|---|
| `AirJumps` | `2` | Extra jumps in mid-air. `1` = double jump, `2` = triple jump, max `10`. |
| `DebugLogging` | `false` | Writes every jump press and the air-jump decision to `BepInEx/LogOutput.log`. |

## Multiplayer

Bopl Battle uses deterministic lockstep netcode. **Every player in the lobby must have this mod installed with the same `AirJumps` value**, otherwise the game will desync.

## Incompatibilities

Don't combine with other air-jump mods such as `thepro-DoubleJump`: both would trigger on the same button press.
