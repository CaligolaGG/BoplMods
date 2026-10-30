# Changelog

## 1.5.0
- New skill: Omae wa mo shindeiru. Aim a poke (red cone, blocked by platforms), players hit are marked (flashing faster and stronger as time runs out) and die after 20 s; poking a marked player takes 5 s off. Own icon, and plays "Omae wa mou shindeiru" when a poke marks someone.
- New skill: Nothing Personal Kid. Teleport behind the furthest player, Teleport-style (portal, light beam, smoke cloud, 0.1 s travel), then 0.3 s immune and unable to use other abilities. 10 s cooldown.
- New skill: Run. Hold to be 20% faster; the 3 s cooldown starts on release; using another ability ends the run.
- Runtime textures, sprites and sounds survive scene changes.
- Pinball: new `BouncePower` setting (default 1, same behaviour as before): multiplies the ball's bounce off surfaces, items and players (<1 weaker, >1 stronger).
- Support for CaliGameModes (spawning standalone pinballs).
- Fix: a slime hit by a Pinball while spawning or reviving no longer gets stuck in the spawn animation (spawning slimes are ignored).

## 1.4.0
- Pinball interacts with other abilities like the grenade: it stays on the grenade's "item" layer (Gust, Black hole, Magnet gun, explosions, water...).
- Fix: Duplicator copies of a Pinball keep their spread direction, speed and remaining lifetime instead of all flying straight up.
- Player hits are detected by the ball itself (like the grenade), so a Magnet gun holding it doesn't get hit by it.

## 1.3.0
- Pinball is now thrown by hand like the Grenade (instead of shot like the Bow); the ball grows in the slime's hand while you charge.
- Pinball duration no longer depends on the charge (removed `DurationAtMinCharge`).

## 1.2.0
- Dev tool: right-click a CaliSkills skill in the ability grid to edit its settings in game (applies from the next round, until the game restarts).

## 1.1.0
- Pinball: speed, size, knockback and duration scale with how long you charge the shot.
- Pinball: players stuck to a floor, wall or ceiling are detached and knocked away from it.
- The all-abilities grid in character select scrolls when it doesn't fit on screen (mouse wheel, or follows the keyboard/gamepad selection).

## 1.0.0
- New skill: Pinball.
