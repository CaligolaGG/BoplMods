# CaliSkills

A pack of new abilities for Bopl Battle. New skills appear at the end of the ability grid.

## Skills

### Pinball

Throw a **pinball** by hand, aimed like the Grenade.

- **Charge it**: hold the button and the ball grows in your hand; the longer you hold, the bigger, faster and stronger it is when thrown.
- It bounces off platforms for **10 seconds**, then disappears.
- It doesn't kill: a player it hits is **knocked back** (ragdolled), and the ball bounces off them like a bumper. Players stuck to a floor, wall or ceiling get popped off it.
- Like grenades, it can hit you too, but not right after you throw it.
- Cooldown: **5 seconds**.

### Omae wa mo shindeiru

*"You are already dead."* A pressure-point poke straight out of Fist of the North Star.

- **Hold** to aim: a red cone shows what the poke will hit. **Release** to poke.
- Every player in the cone, unless a platform is in the way, is **marked**: they flash red (white if they're the red player), faster and stronger as their time runs out, and **die after 20 seconds**, squished.
- Poking an already marked player takes **5 seconds** off their mark.
- A poke that marks someone plays the famous line.
- A marked player who dies some other way is unmarked (a Revive brings them back clean). Marks don't carry over to the next round.
- Cooldown: **5 seconds**.

### Run

Hold the button to run **20% faster**. Release it and the **3 second** cooldown starts. You can use your other abilities while running, but that ends the run.

### Wooden Bat

Hold the button to raise the bat (aim it with your movement keys, like the Bow), release to **swing**. The bat is solid like a platform, but nothing sticks to it: players it hits get **shoved along and knocked a little**, and anyone caught **between the bat and a platform gets squished** (unless the host turns on the bat's `NoSquashing` stat: then they're only knocked and the bat passes through them, and the bat no longer counts as an offensive ability). It also **hits projectiles back**: arrows, grenades, smoke grenades, pinballs, missiles, mines... A deflected projectile becomes yours, so it can hit whoever threw it. It never hits you. Swing it into a platform to **bounce away** from it, e.g. hit the ground below you to pogo up. Cooldown: **3 seconds**, from the end of the swing.

### Nothing Personal Kid

Teleport **behind the player furthest from you**, Teleport-style: you vanish in a puff of smoke, a light beam runs to a portal behind them, and you appear there a tenth of a second later. For a moment after appearing you're **immune** (like after a respawn) but can't use other abilities. Cooldown: **10 seconds**.

### Rock Paper Scissors

Hold the button: a **yellow circle** shows around you (you can still move). Release it to **challenge** the closest enemy slime in the circle (nobody in it: nothing happens).

- **Time stops** for everyone, like the Time stop ability, and that includes the two of you: nobody can move, die or be knocked until the duel is over.
- A **VS screen** shows the two duelists for a second.
- Then your three ability icons turn into **rock, paper and scissors**: press the matching ability button within **2 seconds**, while the game chants *rock... paper... scissors...* (you can change your mind until then; no pick = a random one). Your pick is highlighted on your icons. When both duelists play on the same screen, they only see that the other is ready, not what they picked.
- The picks are revealed, time starts again and **the loser gets squashed**. On a **draw**, the challenged player's abilities all go on cooldown.
- Cooldown: **3 seconds**, from the release.

### Fan

**Hold** the button to turn into an **electric fan** (a quick transformation); **release** it and you turn back into a slime, like the Beam. It blows for at most **4 seconds**. Like the Beam, you can't move or jump meanwhile and **gravity doesn't pull you** (in the air you hover where you are; only pushes move you), and the fan **turns towards where you aim**. The fan is big and has your slime's colors.

- It blows a **cone of wind** (a trapezoid, narrow at the fan, wide at the far end) in the direction it faces.
- **Everything** caught in it is pushed away every moment: other players, projectiles and items (arrows, grenades, mines, pinballs...), platforms that can move, and smoke clouds.
- The wind is **strongest right at the fan** and fades towards the far end.
- Cooldown: **6 seconds**, from when you're a slime again.

### Chickenator

Throw a **potion**, aimed and charged like the Grenade.

- It **bursts** when it hits a player, a platform or a wall, or by itself when its fuse runs out, even in your hand if you hold it too long.
- Everyone caught in the burst, **you included**, unless a platform shields them, turns into a **chicken** for **2 seconds**: chickens can't use abilities, jump half as high, are 30% heavier (they fall faster and get pushed less) and 10% slower.
- Getting hit again while you're a chicken starts the 2 seconds again.
- Cooldown: **4 seconds**.

### Charge

Hold to **load a charge** for up to **3 seconds**, then release to **dash** where you aim.

- While loading, you hang in place like the Meteor, pointing where you aim, shaking and growing up to **50% bigger**. A line shows how far you'll go.
- The longer you load, the **further** you dash and the **harder** you hit. At a full charge you launch by yourself.
- Every player in your path is **knocked away**. If they then slam into a platform fast enough, they're **squashed**.
- Flying things in your path (arrows, grenades...) are knocked away too. A platform in the way stops you and you stick to it, but it gets knocked too: it's pushed along your dash and shakes, harder the more you charged.
- Cooldown: **4 seconds**, from the end of the dash.

### Chad mode

Become the **muscly slime** from the Throw ability until you've used all three moves. You still move and jump, but your three ability buttons become three moves, each usable **once**, shown under you in their usual order:

- **Slam** (first button): the Throw ability's opening jump and ground grab: on a platform, a short wind-up and a hard hit; in the air, a straight dive down. Anyone **under you is squashed**.
- **Charge** (second button): a short dash sideways, towards the side you're moving (A/D or left stick) or else facing: along the platform you're stuck to (walls and ceilings too), straight across in the air. Every slime you run through is **stunned** for **1.25 seconds**: it can't move, jump or use abilities, but keeps flying or sliding with the speed it had.
- **Aura farm** (third button): you **shine** for **1 second**: invincible and immune to knockback, ragdolls, stuns and the chicken curse. While shining you also **reflect** projectiles (sent back faster than they came), beams and the Blink gun / Growth ray / Shrink ray rays (they become yours), **push smoke away**, turn any **black hole** you touch into a **white hole**, and **reveal invisible slimes** nearby.
- Chad mode has no time limit: it ends once all three moves are used. Cooldown: **3 seconds**, from the end.

### Flamethrower

Hold the button to throw **flames** in a cone where you aim (mouse or right stick), for up to **2.5 seconds**. You keep moving and jumping while you fire. Platforms stop the flames.

- Anyone caught in the flames **dies**. Smoke clouds in them go off and mines blow up.
- **Normal maps**: platforms hit by the flames **catch fire** where they're hit. The fire slowly spreads along the platform and dies out **2 seconds** after the flames last touched it. Sweep the flames along a platform to set it on fire in several places (a new fire needs to be a little away from the others, at most one new fire per platform every 0.4 seconds). Players touching a burning part die (not you).
- **Ice maps**: the flames **melt** the platforms they hit: they slowly shrink (down to 40% of their size), dripping water and steaming (the bigger the platform, the bigger the effect), and grow back to their size starting **1.5 seconds** after the last hit.
- **Space maps**: the cone is **30% smaller** and platforms don't catch fire, but the flames push you the other way, like a **jetpack**, and the cooldown is only **1.5 seconds**.
- **With a grappling hook**: if you're hooked to a platform while you fire, the flames run down the rope to it, however long the rope is: it **catches fire** where the hook is (normal maps) or **melts** (ice maps).
- Cooldown: **3 seconds** (1.5 on space maps), from when the flames stop.

## Show hitboxes

With CustomRules, the host can tick **Show hitboxes** in the abilities window: the hidden hitboxes of Chad mode (slam, charge, shining area), Charge (the dash), the Flamethrower (the cone) and the Wooden Bat (the swing) are then drawn in red while they're active. Visual only, for testing.

## Ability grid

With extra skills the character select grid gets taller than the screen; this mod makes it scroll (mouse wheel, or it follows the keyboard/gamepad selection).

## Changing skill stats

With [CustomRules](https://thunderstore.io/c/bopl-battle/p/caligola/CustomRules/) installed, the lobby host can change every skill's stats (speed, cooldown, mark duration...): **Custom rules → Abilities → right-click the skill**. The changes are saved and every player in the lobby uses the host's values. Without CustomRules the skills use their built-in values.

## Multiplayer

Every player in the lobby needs this mod (same version). Players without it don't have these abilities.
