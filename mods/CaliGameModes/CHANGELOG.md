# Changelog

## 1.1.3
- Rotation mode: the respawn invulnerability is now always taken off the slime that got it (a player with Duplicator copies could keep it on another one).
- Needs CaliSkills 1.5.4 (fixes slimes that could stay invulnerable or stuck after a respawn or a Nothing Personal Kid teleport).

## 1.1.2
- Performance: Rotation mode's knock tracking is removed again in rounds without Rotation mode (it slowed every later round down), and finds who owns what once per object instead of every tick; the Rotation and Football scoreboards are rebuilt only when they change; the football goals are no longer redrawn every frame while the level animates in.
- Rotation mode: a slime that comes back is invulnerable for 1 second once its revive animation ends.
- Needs CustomRules 1.0.3 and CaliSkills 1.5.3.

## 1.1.1
- Rotation mode: every ability you use up is gone for you and replaced by a new one, until you've used them all. Nobody dies: a kill gives the killer a point and the slime comes back at the spawn point farthest from the others. Dying to the water, the edge of the map or your own attack within 5 s of being knocked by someone gives them the point; a suicide with nobody to blame gives a point to every player of the other teams. The round ends only when every player has used every ability; the highest score wins (in teams, the team's total). Scores at the top of the screen. While it's on, Abilities max use, Destroy ability on max use, Reroll on max use and Infinite round duration are locked on, Max use amount can't be 0, and Revival is banned. Can't be on with Football mode.
- Needs CustomRules 1.0.2.

## 1.1.0
- Football mode: left team against right team, score in the other team's goal (drawn on the map with posts, crossbar and net), score at the top of the screen, nobody dies. Sub rules: First to, Time, Number of balls, Edit football (the ball's own stats). Turning it on sets Extra jump (1 air jump), Infinite round duration, Player bouncing mode and No offensive abilities. Played on football maps with the Map Loader mod; first map: Football Field (shipped with the mod).
- Pinball mode and Football mode can't both be on.
- New rule: Deactivate custom maps (default on): with Map Loader installed, matches use the game's own levels instead of its custom maps. Football mode turns it off and only plays on football maps.

## 1.0.1
- The Pinball settings now come from CustomRules' ability stats (Custom rules → Abilities → right-click Pinball).

## 1.0.0
- Pinball mode: every few seconds a pinball is shot from inside the left or right side of the arena, upwards towards the center, or (1 in 4) dropped from the sky at a random angle. Balls use the host's Pinball settings and are randomly 10%-40% faster.
