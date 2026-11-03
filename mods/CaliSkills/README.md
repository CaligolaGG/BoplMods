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

## Ability grid

With extra skills the character select grid gets taller than the screen; this mod makes it scroll (mouse wheel, or it follows the keyboard/gamepad selection).

## Changing skill stats

With [CustomRules](https://thunderstore.io/c/bopl-battle/p/caligola/CustomRules/) installed, the lobby host can change every skill's stats (speed, cooldown, mark duration...): **Custom rules → Abilities → right-click the skill**. The changes are saved and every player in the lobby uses the host's values. Without CustomRules the skills use their built-in values.

## Multiplayer

Every player in the lobby needs this mod (same version). Players without it don't have these abilities.
