# BoplMods — Claude guide

BepInEx 5 + Harmony mods for **Bopl Battle** (Unity 2022.3.60, Mono backend, Steam app 1686940), distributed through Thunderstore (Thunderstore Mod Manager / r2modman).

## Layout

```
Directory.Build.props     shared settings: netstandard2.1, BepInEx/Harmony NuGet refs, game DLL refs, paths
Directory.Build.targets   DeployToProfile (after every build) + PackThunderstore target (tcli)
Directory.Build.user.props  (optional, git-ignored) per-machine overrides; see *.example
BoplMods.sln
mods/<ModName>/           one project per mod: .csproj, Plugin.cs, *Patches.cs, thunderstore.toml, README.md, CHANGELOG.md, icon.png
decompiled/Assembly-CSharp/  ILSpy output of the game (git-ignored, reference only; never ship or commit)
tools/                    dnSpyEx (GUI decompiler/debugger), UnityExplorer (runtime inspector plugin)
dist/                     Thunderstore zips produced by PackThunderstore
```

## Commands

```bash
dotnet build mods/<Mod> -c Release                          # build + copy DLL into the Default mod manager profile
dotnet build mods/<Mod> -c Release -t:PackThunderstore      # also build dist/<Team>-<Mod>-<Version>.zip
dotnet build -c Release -p:DeployToProfile=false            # build all, no deploy
```

- Deploy target: `%APPDATA%\Thunderstore Mod Manager\DataFolder\BoplBattle\profiles\Default\BepInEx\plugins\<Team>-<Mod>-dev\`
- Runtime log: `<profile>\BepInEx\LogOutput.log` — check it after launching to confirm the plugin loaded and patches didn't throw.
- Launching modded from CLI (what the mod manager does):
  `steam.exe -applaunch 1686940 --doorstop-enabled true --doorstop-target-assembly "<profile>/BepInEx/core/BepInEx.Preloader.dll"`
- Re-decompile after a game update: `ilspycmd -p -o decompiled/Assembly-CSharp -r "<game>/BoplBattle_Data/Managed" "<game>/BoplBattle_Data/Managed/Assembly-CSharp.dll"` (ilspycmd lives in `%USERPROFILE%\.dotnet\tools`, which may not be on Git Bash PATH).

## Conventions

- Version lives only in the `.csproj` `<Version>`; namespace (team) only in `ThunderstoreTeam` in Directory.Build.props. `PackThunderstore` passes both to tcli, overriding `thunderstore.toml`.
- Plugin GUID: `com.boplmods.<modname>` via `<BepInExPluginGuid>`; code uses the generated `MyPluginInfo`.
- `Assembly-CSharp` is referenced with `Publicize="true"`: private game members can be used directly (`__instance.isInAbility`) — no `Traverse`/reflection needed. Harmony targets via `nameof(...)` still work.
- **Never `UnpatchSelf()` in a plugin's `OnDestroy`.** Bopl Battle destroys the BepInEx manager GameObject (all plugin MonoBehaviours) before the main menu, so it would silently remove every patch. Plugin `Update`/coroutines don't survive either; for per-frame logic, patch game methods or create your own `DontDestroyOnLoad` GameObject.
- Prefer Prefix/Postfix over Transpilers; keep per-object mod state in `ConditionalWeakTable<TComponent, TState>`, not static fields shared between players.
- Each mod's README is the Thunderstore page; keep it user-facing.

## Bopl Battle specifics (important)

- **Deterministic lockstep netcode, no rollback.** Gameplay runs in `UpdateSim(Fix dt)` / `LateUpdateSim` on `MonoUpdatable`s, using `BoplFixedMath.Fix`/`Vec2`, not floats or `Time.deltaTime`. Gameplay mods must:
  - only depend on simulated state + synced inputs (`Player.jumpButton_PressedThisFrame()`, `Player.InputVector()`, `AbilityButtonIsDown(i)`),
  - use `Fix` math, never `UnityEngine.Random`/`System.Random`/floats/wall-clock in sim logic,
  - be installed with identical config by every player in the lobby. Cosmetic/UI-only mods are client-side safe.
- Key classes: `SlimeController` (player state machine; `OldUpdate` handles input/abilities/jump), `PlayerPhysics` (`Jump()`, `IsGrounded()`, gravity, `jumpStrength`), `PlayerBody` (`position`, `selfImposedVelocity`, `externalVelocity`), `PlayerHandler.Get().GetPlayer(id)`, `Player` (inputs, scale, stats), `GameTime`, `Updater`, `AudioManager.Get().Play(...)`, ability classes (`Roll`, `Beam`, `MeteorSmash`, ...).
- Unity log appears in BepInEx log as `[Info : Unity Log]`.
- Keyboard+mouse gameplay bindings (sharedassets1.assets): Jump = Space, Ability0 = left mouse, Ability1 = right mouse, Ability2 = middle mouse (or mouse back/forward), aim = mouse position.
- Abilities: `docs/ABILITIES.md`. The list is the `abilityIcons` NamedSpriteList asset (shared by `AbilityGrid`, `SteamManager.instance`, `AchievementHandler`, `RandomAbility`); index = ability id, entry = name + icon + prefab (`associatedGameObject`) + `isOffensiveAbility`. Players/slimes: `CharacterSelectHandler.TryStartGame_inner` builds `Player`s, `GameSessionHandler.SpawnPlayers` instantiates `PlayerPrefab` per player.
- Missing sounds (`AudioManager.Play`) and translations (`LocalizationTable.GetText`) only log warnings.

## Mods

- `mods/CaliSkills` — the user's ability pack. `Core/Skill.cs` (base class: name, icon, prefab), `Core/SkillRegistry.cs` (appends every registered skill to the `abilityIcons` list, same patches as SpawnBot; prefabs live under an inactive DontDestroyOnLoad root), `Core/SpriteFactory.cs` (runtime sprites). Add a skill: new folder under `Abilities/`, a `Skill` subclass, `SkillRegistry.Add(...)` in `Plugin.Awake`. Tuning values live in a `<Skill>Settings.cs` static class per skill.
  - Pinball (`Abilities/Pinball`): clone of the Grenade ability prefab (`ThrowItem2`: hold in hand, aim, throw; `Ability.Cooldown` set, `minThrowForce`/`maxThrowForce` = 0, `ItemPrefab` swapped, hand "dummy" (child 1) sprite replaced). Ball = copy of the grenade item: keeps `Item` + `Grenade` (ThrowItem2 needs them), `IGrenadeDetonation` components removed and `timedExplosion` off, children/SpriteAnimator removed, grenade's "item" layer KEPT (abilities recognise items by layer: Duplicator, Black hole, Gust achievement, KillZone, bounds cleanup), bounciness = `BouncePower` (restitution, `1 + max` of both bodies in PhysTools) / no friction, runtime ball sprite. `PinballBall` (also a no-op `IGrenadeDetonation`): while held (`!grenade.hasBeenThrown`) sets `Scale` from the thrower's charge (`Clamp01((hasInputtedFire ? FireInputTimeStamp : timePassed) * ThrowForceGainSpeed)`, same as the aim indicator); on release sets velocity = `thrower.dir` × charge-scaled speed; lifetime counted from the throw (fixed, not charge-scaled); per-player hit cooldown; owner = `Item.OwnerId`, can hurt owner = `Grenade.DetonatesOnOwner`. Speed, size and knockback scale linearly from `...AtMinCharge` to the full values. State fields (`_thrown`, `_charge`, `_baseScale`, `_ageSinceThrow`) are `[SerializeField]` so the Duplicator's copies (`ShootDuplicator.DuplicateObject` = Instantiate of the live object, then `Grenade.Initialize(null)` + `hasBeenThrown = false`) don't re-launch and keep their fan-out velocity. Player hits: `PinballBall.OnCollide` (`ICollisionCallback` on its circle, same rules as `GrenadeExplode.OnCollide`: owner immunity via `DetonatesOnOwner`, Magnet-gun holder immunity) → `PinballKnock` detaches grounded players (impulse reflected/biased away from the attached surface's normal, else the landing raycast re-attaches them) then ragdolls the slime (`ApplyRagDoll`, like the Magnet gun throw) or pushes ability forms; the ball is reflected. "item" isn't a lethal layer in `PlayerCollision`, so no kill patch is needed.
  - In-game settings editor (dev): `Core/SkillSettingsEditor.cs` (IMGUI modal on its own DontDestroyOnLoad GameObject; reflects public static float/int/bool fields of `Skill.SettingsType`, `[Description]` shown as hint; Apply sets the fields and calls `Skill.OnSettingsChanged()` so skills re-apply values baked into prefabs; defaults snapshotted on first open). `Core/SkillSettingsEditorPatches.cs`: right-click in `AbilityGrid.Update` postfix opens it for the hovered skill; while open (and the closing frame) the character-select scripts that read the keyboard are skipped and EventSystems disabled. New skills: give them a `<Skill>Settings` static class with `[Description]`s and override `SettingsType`.
  - `Core/AbilityGridScroll.cs`: patches `AbilityGrid.Awake/Init/Update` to scroll the grid vertically when it's taller than the screen (keeps vanilla layout, shifts entries' localPosition, hides entries outside the window, follows selection, mouse wheel). Needs `UnityEngine.UIModule` + `Unity.InputSystem` references (in CaliSkills.csproj). Vanilla reference: arrow radius 0.61, gravity 1, `ArrowSpeed` 15 (×1–5 by charge); Magnet gun `FireRagdollForce` 60, `fireRagdollDuration` 0.3.
- `mods/SpawnBot` — dev tool. Adds a custom ability by appending a `NamedSprite` ("SpawnBot", runtime-drawn icon, inactive template GameObject with `FixTransform` + `SpawnBotAbility : AbilityMonoBehaviour`) to the `abilityIcons` NamedSpriteList (prefix on `AbilityGrid.Awake`, `CharacterSelectHandler.TryStartGame_inner`); excluded from `RandomAbility.GetRandomAbilityPrefab`. Casting it adds a bot `Player` (new id, unused team) to `PlayerHandler` and spawns its slime like `PlayerCollision.SpawnAbilityLessClone`. Bots are hidden from the player list during `GameSessionHandler.UpdateSim`/`EndGameImmediately` (win logic) and removed in `Updater.PreLevelLoad`. Local games only.
- `mods/TripleJump` — postfix on `SlimeController.OldUpdate`; if the player was airborne the whole frame and pressed jump, calls `PlayerPhysics.Jump()` (up to `AirJumps` config, default 2). Conflicts with `thepro-DoubleJump`.
