# Bopl Battle abilities (game version 2.5.1)

Bopl Battle 2.5.1 has **30 abilities**, plus a `NullAbility` placeholder for an empty slot. The C# code only contains reusable ability classes (`Dash`, `ThrowItem2`, `BowTransform`, …), not the names. The list itself is stored in the game data as the `abilityIcons` list (`NamedSpriteList`) in `BoplBattle_Data/sharedassets0.assets`.

| # | Ability | Offensive | Cooldown (s) |
|---|---|---|---|
| 1 | Random | ✔ | 0 (uses the drawn ability's) |
| 2 | Dash |  | 4 |
| 3 | Grenade | ✔ | 2.25 |
| 4 | Bow | ✔ | 2.5 |
| 5 | Engine |  | 3.5 |
| 6 | Blink gun |  | 2.5 |
| 7 | Gust |  | 5 |
| 8 | Growth ray |  | 2.5 |
| 9 | Rock | ✔ | 2 |
| 10 | Missile | ✔ | 4 |
| 11 | Spike | ✔ | 3 |
| 12 | Time stop |  | 3 |
| 13 | Smoke |  | 3 |
| 14 | Platform | ✔ | 6 |
| 15 | Revival |  | 10 |
| 16 | Roll | ✔ | 3.5 |
| 17 | Shrink ray | ✔ | 2.5 |
| 18 | Black hole | ✔ | 4.5 |
| 19 | Invisibility |  | 6 (second value: 2) |
| 20 | Meteor | ✔ | 4 |
| 21 | Throw | ✔ | 1 |
| 22 | Push |  | 1 |
| 23 | Tesla coil | ✔ | 1 |
| 24 | Mine | ✔ | 4.5 |
| 25 | Teleport |  | 3 |
| 26 | Drill | ✔ | 4 |
| 27 | Grappling hook |  | 3 |
| 28 | Beam | ✔ | 5.5 |
| 29 | Duplicator |  | 4 |
| 30 | Magnet gun | ✔ | 5 |

Cooldowns are the base `Cooldown` values of each ability's prefab, read from the game data (sharedassets0.assets, via UnityPy with a type tree generated from Assembly-CSharp). Some abilities start the cooldown only after the ability ends. Across the 29 real abilities (not Random) the average is about 3.6 s and the median 3.5 s; offensive ones average about 3.45 s, the others about 3.9 s.

Index 0 is `NullAbility`, the empty slot.

## Notes for modding

- **The number is the game's ability ID.** Character select stores your pick as an index into this list (`playerInit.ability0`), and `CharacterSelectHandler` uses it to look up the ability's prefab and icon (`abilityIcons.sprites[i].associatedGameObject` / `.sprite`).
- **The name is also a key:** the game uses it to look up the localized display name. Achievements check names directly in code, e.g. `name == "Blink gun"`, `"Dash"`, `"Random"`.
- **"Offensive" is the game's own flag** (`NamedSprite.isOffensiveAbility`). At least one achievement checks it.
- **The demo shows a shorter list.** Without the DLC, the select grid uses `abilityIcons_demo` instead. That list wasn't found in the asset files, so which abilities it includes is unknown.
- Each entry is a `NamedSprite { string name; Sprite sprite; GameObject associatedGameObject; bool isOffensiveAbility; }`. The ability classes don't map one-to-one to these names; for example, Grenade is `ThrowItem2` with `isExplosiveGrenade`. To find the class behind an ability, follow its `associatedGameObject` prefab.

## Modded abilities (CaliSkills)

Added by `mods/CaliSkills` to the end of the `abilityIcons` list (their IDs come after 30, in registration order). Cooldowns are the defaults in each skill's `<Skill>Settings.cs`; the lobby host can change them in CustomRules (Abilities → right-click the skill).

| Ability | Offensive | Cooldown (s) | What it does |
|---|---|---|---|
| Pinball | | 5 | Thrown like the Grenade: a bouncing ball that knocks players back (never kills) for 10 s. |
| Omae wa mo shindeiru | ✔ | 6 | A poke (red cone); marked players die after 20 s, poking them again takes 5 s off. |
| Run | | 3 | Hold to be 20% faster; the cooldown starts on release. |
| Nothing Personal Kid | | 10 | Teleports behind the furthest player, then briefly immune. |
| Wooden Bat | ✔ (not with `NoSquashing`) | 3 | Hold and release to swing: shoves and knocks players, squashes them against platforms, deflects projectiles, pogo off platforms. |
| Rock Paper Scissors | ✔ | 3 | Hold (yellow circle), release to stop time and duel the closest enemy slime in it at rock paper scissors; the loser is squashed, a draw puts the challenged player's abilities on cooldown. |
| Fan | | 6 | Become a fan for 3 s that turns towards your aim and blows a trapezoid of wind, pushing players, projectiles, platforms and smoke away (strongest near the fan). |
