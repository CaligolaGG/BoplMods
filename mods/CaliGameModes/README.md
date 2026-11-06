# CaliGameModes

Game modes for Bopl Battle, picked by the lobby host in the **Custom rules** window of [CustomRules](https://thunderstore.io/c/bopl-battle/p/caligola/CustomRules/).

## Modes

### Pinball mode
Every few seconds a pinball (from [CaliSkills](https://thunderstore.io/c/bopl-battle/p/caligola/CaliSkills/)) is shot into the arena from the left or right side, upwards towards the center; every now and then one drops from the sky at a random angle. Each ball is randomly 10%–40% faster than the host's Pinball. It bounces around and knocks back anyone it hits.
- **Spawn timing (s)**: seconds between two pinballs (default 3).
- The balls use the host's Pinball settings: if the host changes the Pinball's stats (Custom rules → Abilities → right-click Pinball: speed, size, knockback, bounce, duration...), the spawned balls change the same way.

### Football mode
Two teams, two goals, one ball. Players who spawn on the left play against those on the right: score by getting the ball into the other team's goal. The score is shown at the top of the screen.
- The ball appears in the middle of the map. After a goal every ball disappears and new ones come back after 2 seconds, out of a puff of smoke. A ball lost any other way (e.g. in the water) comes back right away, without a goal.
- **Nobody dies**: anyone who would die comes back in the middle of the map.
- Only the mode's balls count: Pinball abilities don't score.
- **First to**: goals a team needs to win (0 = no goal limit). **Time (s)**: match length; then the team with more goals wins, a tie is a draw (0 = no time limit). **Number of balls**: balls in play at once. **Edit football**: the ball's own stats (size, knockback, bounce off players and off the rest...), separate from the Pinball ability's.
- Turning it on also sets the usual rules for it: Deactivate custom maps off, Extra jump on with 1 air jump, Infinite round duration, Player bouncing mode, No offensive abilities (you can change them afterwards).
- **Needs the [Map Loader](https://thunderstore.io/c/bopl-battle/p/MapMakerTeam/Map_Loader/) mod**: football is played on its own maps, which come with this mod (copied into Map Loader's `Maps` folder): Football Field. While football is on only football maps are played; while it's off Map Loader gets the other maps (none: the game's own levels, in local games; online Map Loader can't play without custom maps, so it keeps the football maps).

### Rotation mode
Use every ability, score the most kills.
- Every ability can be used a limited number of times (**Max use amount** under Abilities max use, default 2, at least 1 in this mode). Once used up it's gone for you for the rest of the round, and the slot gets a new random ability (starting on cooldown) that you haven't used up yet. When none is left, the slot stays empty.
- **Nobody dies**: a slime that would die comes back at the spawn point farthest from the other slimes, and whoever killed it gets a point (none for killing a teammate).
- **Knocked out**: if you die to the water, the edge of the map or your own attack within 5 seconds of being knocked around by someone (Push, an explosion, a pinball, a black hole...), they get the point.
- **Suicide**: if you kill yourself with nobody to blame, every player of the other teams gets a point.
- The round ends **only when every player has used up every ability**. The highest score wins the round (in team games, the team with the highest total). The scores are shown at the top of the screen, with how many players still have abilities.
- While it's on these rules are locked (greyed out in the menu): Abilities max use, Destroy ability on max use, Reroll on max use and Infinite round duration are on. Revival is banned. It can't be on with Football mode.
- Ability pickups still drop: they give you more abilities to use up, so they make the round longer.

### Deactivate custom maps (default on)
With the [Map Loader](https://thunderstore.io/c/bopl-battle/p/MapMakerTeam/Map_Loader/) mod installed, every match is normally played on its custom maps. With this rule on they're left out of the rotation and matches use the game's own levels. Football mode turns it off and always plays on its own football maps only. Without Map Loader it does nothing.

## Notes

- Requires CustomRules and CaliSkills. Football mode also needs Map Loader.
- **Every player in the lobby needs this mod.** The host's Pinball settings are also applied to every player's Pinball ability.
