# Bopl Battle abilities (game version 2.5.1)

Bopl Battle 2.5.1 has **30 abilities**, plus a `NullAbility` placeholder for an empty slot. The C# code only contains reusable ability classes (`Dash`, `ThrowItem2`, `BowTransform`, …), not the names. The list itself is stored in the game data as the `abilityIcons` list (`NamedSpriteList`) in `BoplBattle_Data/sharedassets0.assets`.

| # | Ability | Offensive | | # | Ability | Offensive |
|---|---|---|---|---|---|---|
| 1 | Random | ✔ | | 16 | Roll | ✔ |
| 2 | Dash | | | 17 | Shrink ray | ✔ |
| 3 | Grenade | ✔ | | 18 | Black hole | ✔ |
| 4 | Bow | ✔ | | 19 | Invisibility | |
| 5 | Engine | | | 20 | Meteor | ✔ |
| 6 | Blink gun | | | 21 | Throw | ✔ |
| 7 | Gust | | | 22 | Push | |
| 8 | Growth ray | | | 23 | Tesla coil | ✔ |
| 9 | Rock | ✔ | | 24 | Mine | ✔ |
| 10 | Missile | ✔ | | 25 | Teleport | |
| 11 | Spike | ✔ | | 26 | Drill | ✔ |
| 12 | Time stop | | | 27 | Grappling hook | |
| 13 | Smoke | | | 28 | Beam | ✔ |
| 14 | Platform | ✔ | | 29 | Duplicator | |
| 15 | Revival | | | 30 | Magnet gun | ✔ |

Index 0 is `NullAbility`, the empty slot.

## Notes for modding

- **The number is the game's ability ID.** Character select stores your pick as an index into this list (`playerInit.ability0`), and `CharacterSelectHandler` uses it to look up the ability's prefab and icon (`abilityIcons.sprites[i].associatedGameObject` / `.sprite`).
- **The name is also a key:** the game uses it to look up the localized display name. Achievements check names directly in code, e.g. `name == "Blink gun"`, `"Dash"`, `"Random"`.
- **"Offensive" is the game's own flag** (`NamedSprite.isOffensiveAbility`). At least one achievement checks it.
- **The demo shows a shorter list.** Without the DLC, the select grid uses `abilityIcons_demo` instead. That list wasn't found in the asset files, so which abilities it includes is unknown.
- Each entry is a `NamedSprite { string name; Sprite sprite; GameObject associatedGameObject; bool isOffensiveAbility; }`. The ability classes don't map one-to-one to these names; for example, Grenade is `ThrowItem2` with `isExplosiveGrenade`. To find the class behind an ability, follow its `associatedGameObject` prefab.
