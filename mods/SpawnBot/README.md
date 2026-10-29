# SpawnBot

A development tool: adds a **SpawnBot** ability (robot icon, last slot of the ability grid). Casting it spawns an idle bot player in front of you, so you can test offensive abilities on it.

- Bots are real players on a team no human uses, with their own color. They don't move or use abilities.
- Killing a bot **doesn't** end the round; you still lose if you die.
- Bots disappear at the end of the round.
- The "Random" ability never rolls SpawnBot.
- **Local games only.** In online games the ability does nothing, and you shouldn't pick it there: players without the mod don't have this ability.
- Needs the DLC ability grid; without the DLC, the slot is locked like the other DLC abilities.

## Configuration

`BepInEx/config/com.boplmods.spawnbot.cfg`:

| Setting | Default | Description |
|---|---|---|
| `Cooldown` | `1` | Seconds between casts. |
| `MaxBots` | `3` | Maximum bots alive at once. |
| `SpawnDistance` | `6` | How far in front of you the bot appears. |
