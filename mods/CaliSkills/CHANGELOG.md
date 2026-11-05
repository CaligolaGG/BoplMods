# Changelog

## 1.5.2
- New skill: Flamethrower. Hold to throw flames in a cone where you aim for up to 2.5 s, still moving and jumping; platforms stop them. Players in the flames die; smoke clouds go off; mines blow up. Normal maps: platforms hit catch fire there; the fire spreads along the platform and dies out 2 s after the last hit; players touching it die (not the owner by default). The same platform can burn in several places: flames hitting it at least 2.5 units from where its other fires started light a new one there (at most one new fire per platform every 0.4 s). Hooked to a platform with the Grappling hook while firing: the flames run down the rope (any length) and set it on fire at the hook, or melt it on ice maps. Ice maps: platforms hit melt (shrink, down to 40%, dripping and steaming) and grow back from 1.5 s after the last hit. Space maps: 30% smaller cone, no burning platforms, the flames push you back like a jetpack, 1.5 s cooldown. 3 s cooldown elsewhere. Stats: duration, flame length / angle, smoke ignition, mine detonation, platform fire on/off / duration / spread speed / max spread / min distance between fires / re-ignite grace / flames down the rope / kills / hurts owner, melt speed / max melt / regrow delay / regrow speed, space cone shrink, jetpack push, space cooldown. Show hitboxes draws its cone.
- Fan: turning is smoother and quicker: it eases towards your aim (up to 450°/s instead of a steady 150°/s), follows only the aim (no longer the movement keys or stick when you don't aim), turns smoothly between game ticks, and flips over with a quick roll only once it's clearly past straight up or down instead of popping to the other side.
- Chad mode: fixed the charge sometimes stunning slimes anywhere on the map (on its last, tiny step its direction was nearly zero, which put everyone inside the hitbox).
- Show hitboxes: with CustomRules 1.0.2's "Show hitboxes" checkbox (abilities window) on, hidden hitboxes are drawn in red during matches: Chad mode's slam, charge and shining area, the Charge dash, the Wooden Bat's swing.
- New skill: Chad mode. Become the muscly slime from the Throw ability (it walks and idles like it) until you've used all three moves, still moving and jumping; your three ability buttons become three one-time moves: Slam (the Throw ability's opening: the jump up, then the hands plunging into the ground at the impact; a dive down in the air; squashes players under you), Charge (the throw animation; a short dash sideways, along the platform you're stuck to or straight across in the air; slimes it runs through are stunned for 1.25 s: no moving, jumping or abilities, but they keep their momentum), Aura farm (shine for 1 s: invincible, no knockback, ragdolls, stuns or chicken curse; reflects projectiles, beams and Blink gun / Growth ray / Shrink ray rays, which become yours; pushes smoke away; turns black holes it touches into white holes; reveals invisible slimes nearby). No time limit: the mode ends once all three are used. 3 s cooldown. Stats: slam wind-up / dive speed / recovery / width / depth / forward and up offsets, slam shake, charge distance / speed / hit length / hit height / forward and up offsets, stun duration, aura duration / size, smoke push radius / speed, reveal radius, reflect power (how hard projectiles are pushed back).
- Chad mode: the move icons sit under the slime while it's active (same order as usual, moved below it), and Aura farm's glow is sized to the big chad.
- Chad mode: the last move no longer also fires the normal ability on the same button when the mode ends (each button must be released first).
- Charge: the platform it slams into shakes much less (the shake was far too strong).
- New skill: Charge. Hold to load a charge for up to 3 s: you hang in place like the Meteor, pointing where you aim, shaking and growing up to 50% bigger, with a line showing how far you'll go. Release (or reach a full charge) to dash that way: the longer the charge, the longer the dash and the stronger the knockback. Players in the path are knocked away and squashed if they then hit a platform fast enough; flying projectiles and items are knocked away too; a platform in the way stops the dash and is knocked too (pushed along the dash where it was hit, and shaken). 4 s cooldown. Stats: charge time, growth, dash distances and speed, momentum kept, knockbacks, platform knockback, sideways push, tumble time, squash speed.
- New skill: Rock Paper Scissors. Hold to show a yellow circle around you, release to challenge the closest enemy slime in it. Time stops for everyone, the two of you included (Time stop's effect), a VS screen shows the duel, then both players pick rock, paper or scissors with their three ability buttons within 2 s (no pick = random). The picks are revealed and the loser is squashed; on a draw the challenged player's abilities all go on cooldown. 3 s cooldown. Stats: circle radius, VS / choice / reveal durations.
- Chickenator: platforms block the burst: players behind one (not in line of sight of the potion) aren't turned into chickens.
- Chickenator: the potion only bursts on platforms and walls 0.3 s after the throw (`ArmTime` stat), so throwing it while standing on the floor no longer bursts it at your feet.
- Chickenator: the burst also catches players in an ability form, so the thrower is chickened by a potion bursting in their hand or right after the throw (they were still in the throwing pose, which isn't a slime); the 2 s start once they're a slime again.
- New skill: Chickenator. Throw a potion like the Grenade (same aiming and charge); it bursts on players, platforms, walls and explosions, or by itself when its fuse runs out (also in your hand if you hold it too long). Everyone in the burst, you included, turns into a chicken for 2 s: no abilities, half the jump height, 30% heavier, 10% slower. 4 s cooldown. Stats: fuse, burst radius, chicken duration, jump height, weight, speed.
- Pinball (and CaliGameModes' football): fixed the ball sometimes bouncing back towards the player who hit it (hitting it on its left could send it left). The bounce now always sends the ball away from the player: it's reflected in the player's frame (their speed counts) when it comes at them, and pushed further away when it was already leaving (the physics had bounced it first, or you ran into it from behind). Players are knocked along the ball's travel only when it came at them, else away from it.
- Wooden Bat: the swing follows the slime's rotation: on a wall or upside down on a ceiling, the bat is raised away from the platform and swings into it (before, it always started above in screen terms, inside the platform when upside down), so squashing works there too.
- New skill: Fan. Turn into a big electric fan in your slime's colors (with a transformation animation); like the Beam, no gravity while transformed (you hover in the air) and blow a trapezoid of wind where you aim (the fan turns towards your aim like the Beam): players, projectiles and items (arrows, grenades...), platforms and smoke clouds caught in it are pushed away, hardest near the fan. 6 s cooldown. Stats: duration, transformation time, turn speed, wind length and widths, strength at the fan and at the edge, share for platforms and smoke.

## 1.5.1
- New skill: Wooden Bat. Hold to raise the bat (aim with movement), release to swing it. The bat is solid: it shoves players along and knocks them slightly, squishes anyone caught between it and a platform, never hits you, and hitting a platform with it bounces you away (pogo). 3 s cooldown. Hits flying projectiles back (arrows, grenades, smoke grenades, pinballs, missiles, mines...), which then count as yours (`DeflectProjectiles`, `DeflectPower` stats). `NoSquashing` stat (off by default): no squishing, caught players are only knocked, and the bat stops counting as offensive (CustomRules' offensive filters, Random).
- Skill stats are now set by the lobby host in CustomRules (Custom rules → Abilities → right-click a skill), saved and used by every player. CustomRules is optional: without it the skills use their built-in values.
- Removed the old right-click settings editor in the ability grid (local only).
- Wooden Bat: squishing is consistent. The swing is checked in small steps (a fast swing no longer jumps over players, or hits the floor and stops before reaching a player standing on it), and a player is squished when the bat would push them into a platform (not only straight into one), while a sideways shove along the floor isn't a squish.
- Fix: a ball spawned without a thrower (Pinball mode, football) that the Wooden Bat hit no longer passes through the batter forever. A deflected grenade or pinball now spares the batter for 0.3 s, then can hit them again.
- Pinball: a ball at rest hit by a player now flies off away from them (used by CaliGameModes' football, which also gives its balls their own stats).
- Pinball: `BouncePower` split into `EnvironmentBouncePower` (platforms, items, abilities) and `PlayerBouncePower` (players it hits), shown together under "Bounce power".

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
