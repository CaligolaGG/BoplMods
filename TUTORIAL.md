# Modding Unity games, and Bopl Battle in particular

This guide assumes you know C# and Unity and have never modded a compiled game. It goes from "how is this even possible" to publishing on Thunderstore, using the `TripleJump` mod as the worked example.

> **Note:** TripleJump has since been merged into CustomRules as the "Triple jump" rule (`mods/CustomRules/Rules/AirJumpsRule.cs`, nearly the same code). The standalone project quoted below is in git history (`git show 9dbd21d:mods/TripleJump/AirJumpPatches.cs`); to follow along, restore it with `git checkout 9dbd21d -- mods/TripleJump`.

---

## 1. How Unity modding works

A Unity game ships your C# scripts **compiled**, not as source. How they ship depends on the scripting backend:

| Backend | What you find in the game folder | How to mod it |
|---|---|---|
| **Mono** | `<Game>_Data/Managed/Assembly-CSharp.dll` + other .NET DLLs | Easy: the game code is regular .NET IL. You can decompile it almost back to source and patch it at runtime. |
| **IL2CPP** | `GameAssembly.dll` + `<Game>_Data/il2cpp_data/Metadata/global-metadata.dat` | Harder: code is compiled to native C++. Needs BepInEx 6 / MelonLoader + Il2CppInterop and generated proxy assemblies. |

**Bopl Battle is Mono** (there's a `BoplBattle_Data/Managed` folder, Unity 2022.3.60). This is the comfortable case.

Your scripts end up in `Assembly-CSharp.dll`. Everything a mod does boils down to:

1. **Get our code to run inside the game process.** Unity never loads arbitrary DLLs, so we need a loader.
2. **Change the game's behavior.** We can't recompile `Assembly-CSharp.dll`, so we rewrite its methods in memory at runtime.

### The modding stack

```
BoplBattle.exe starts
 └─ winhttp.dll  ← Unity Doorstop: a fake system DLL Windows loads from the game folder.
     It hooks Mono startup and runs a .NET assembly of our choice before any game code.
     └─ BepInEx.Preloader.dll  ← BepInEx: the mod loader / framework.
         ├─ Logging (BepInEx/LogOutput.log + Unity's log)
         ├─ Configuration files (BepInEx/config/*.cfg)
         └─ Chainloader: loads every DLL in BepInEx/plugins/** that contains a BaseUnityPlugin
             └─ YourMod.dll  →  Harmony patches game methods
```

- **Unity Doorstop** (`winhttp.dll` + `doorstop_config.ini`) is the injection trick.
- **BepInEx** is the de-facto standard loader. Bopl Battle mods on Thunderstore use **BepInEx 5** (BepInExPack 5.4.2305).
- **Harmony** (HarmonyX, bundled with BepInEx as `0Harmony.dll`) patches methods at runtime: it generates a new version of a method that calls your code before/after/instead of the original, and redirects the original to it.

### Where Thunderstore fits

Thunderstore Mod Manager (Overwolf) and r2modman are the same app. They don't touch the game folder beyond `winhttp.dll`; each **profile** is a folder with its own BepInEx:

```
%APPDATA%\Thunderstore Mod Manager\DataFolder\BoplBattle\profiles\Default\
  winhttp.dll, doorstop_config.ini
  BepInEx\core\       BepInEx + Harmony
  BepInEx\plugins\    one folder per mod: <Team>-<Mod>\
  BepInEx\config\     the .cfg files of each mod
  BepInEx\LogOutput.log
```

"Start modded" launches the game through Steam with `--doorstop-target-assembly <profile>\BepInEx\core\BepInEx.Preloader.dll`, so the profile's BepInEx is used. "Start vanilla" doesn't pass it, so no mods load.

---

## 2. The toolbox (all installed already)

| Tool | Use it to |
|---|---|
| **ILSpy CLI** (`ilspycmd`) | Decompile the whole game into a C# project: `decompiled/Assembly-CSharp/`. Open that folder in your IDE and use *Find in files*, *Go to definition*, etc. |
| **dnSpyEx** (`tools/dnSpyEx/dnSpy.exe`) | GUI decompiler. Great for browsing, "Analyze → Used By" (who calls this method?), reading IL, and **debugging the running game** (see §7). |
| **UnityExplorer** (`tools/UnityExplorer`) | In-game inspector: browse the live scene hierarchy, components, field values; run C# in a console. Install by copying `tools/UnityExplorer/plugins/sinai-dev-UnityExplorer` into `<profile>\BepInEx\plugins\`. Toggle with **F7**. |
| **BepInEx NuGet packages** | `BepInEx.Core` (API to compile against), `BepInEx.PluginInfoProps` (generates `MyPluginInfo` constants), `BepInEx.AssemblyPublicizer.MSBuild` (see below), `BepInEx.Analyzers` (warnings for common mistakes). |
| **tcli** | Thunderstore's CLI: builds the upload zip from `thunderstore.toml` and can publish it. |

> Decompiled game code is copyrighted. Read it, learn from it, never commit or redistribute it (`decompiled/` is git-ignored).

### The publicizer

Game code is full of `private` fields you'll want to read. Normally you'd use reflection (`AccessTools.Field`, `Traverse`). This repo references `Assembly-CSharp` with `Publicize="true"`: at compile time you get a copy where everything is `public`, so you can write `__instance.isInAbility` directly. Mono doesn't enforce member visibility at runtime, so it just works.

---

## 3. Anatomy of a BepInEx plugin

```csharp
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("BoplBattle.exe")]          // only load in this executable
public class Plugin : BaseUnityPlugin     // BaseUnityPlugin : MonoBehaviour
{
    internal static ConfigEntry<int> AirJumps;

    private void Awake()
    {
        Logger.LogInfo("Hello from my mod");                       // → LogOutput.log
        AirJumps = Config.Bind("General", "AirJumps", 2, "...");   // → BepInEx/config/<guid>.cfg
        new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll();         // apply every [HarmonyPatch] class
    }
}
```

- The plugin is a real `MonoBehaviour` on BepInEx's manager GameObject. **In Bopl Battle that GameObject is destroyed right after startup**, so `Update()`, `OnGUI()` and coroutines on the plugin stop running and `OnDestroy()` fires. Harmony patches are unaffected, as long as you don't remove them yourself. For per-frame code, patch a game method or create your own GameObject with `DontDestroyOnLoad`.
- **Don't call `harmony.UnpatchSelf()` in `OnDestroy()`.** It's a common tutorial snippet, and it was the actual bug in TripleJump 1.0.0: the game destroyed the plugin, the plugin unpatched itself, and the mod silently did nothing.
- **GUID** must be unique and stable forever (configs and dependencies key off it). Use reverse-domain style: `com.boplmods.triplejump`.
- `[BepInDependency("other.guid")]` declares a dependency on another plugin.
- `Config.Bind` creates the entry on first run; users edit it in the mod manager's *Config editor*.

---

## 4. Harmony in 10 minutes

```csharp
[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.Jump))]
static class MyPatch
{
    // Runs BEFORE the original. Return false to skip the original (and other prefixes' originals).
    static bool Prefix(PlayerPhysics __instance) { ... return true; }

    // Runs AFTER the original (even if a prefix skipped it).
    static void Postfix(PlayerPhysics __instance) { ... }
}
```

Special parameter names Harmony fills in:

| Parameter | Meaning |
|---|---|
| `__instance` | `this` of the patched method |
| `__result` | the return value (`ref` in a postfix to change it, `ref` in a prefix to set it when returning `false`) |
| `__state` | any type; `out` in the prefix, plain parameter in the postfix → pass data from prefix to postfix |
| same name as an original parameter | the argument value (`ref` to modify it) |
| `___fieldName` (3 underscores) | a private field of the instance (`ref` to write it). Less needed with the publicizer. |

Rules of thumb:
- **Prefer Postfix**, then Prefix. Prefix returning `false` replaces the whole method and breaks other mods that patch it.
- **Transpilers** rewrite the IL of a method (insert/replace instructions in the middle). Powerful, fragile; use only when a prefix/postfix can't express the change.
- Patch **the narrowest method** that does what you want.
- Private methods: `nameof(SlimeController.OldUpdate)` works thanks to the publicizer; otherwise use the string `"OldUpdate"`.
- Overloads: `[HarmonyPatch(typeof(T), "Name", new[] { typeof(int) })]`.
- If a patch target doesn't exist (game update renamed it), Harmony throws on `PatchAll` and the error is in the log.

---

## 5. Bopl Battle internals you must know

### Deterministic lockstep networking

Bopl Battle doesn't sync positions over the network. **Every client runs the full simulation**, and only **inputs** are exchanged. The simulation is deterministic, so all clients stay identical. Consequences:

- Gameplay code uses **fixed-point math**: `BoplFixedMath.Fix` and `Vec2`, not `float`/`Vector2`. Floats can give different results on different CPUs. Do the same: `Fix`, `(Fix)0.5f` for constants, `Fix.Min`, `Vec2.up`...
- The simulation steps in **`UpdateSim(Fix simDeltaTime)` / `LateUpdateSim`** of classes deriving from `MonoUpdatable`, driven by `Updater`, **not** in Unity's `Update()` with `Time.deltaTime`.
- Read input through the synced `Player` object: `PlayerHandler.Get().GetPlayer(id).jumpButton_PressedThisFrame()`, `InputVector()`, `AbilityButtonIsDown(i)`. **Never** read `UnityEngine.Input` / the keyboard directly in gameplay code: the other clients won't see it.
- No `UnityEngine.Random`, `System.Random`, `DateTime.Now` in gameplay logic.
- **Every player in the lobby needs the same gameplay mods with the same config**, otherwise their simulations diverge (desync). Cosmetic/UI/audio mods are client-side and safe.
- There's no rollback, so keeping extra state in your mod (like an air-jump counter) is fine as long as it's computed from simulation data.

### Key classes (search them in `decompiled/Assembly-CSharp`)

| Class | What it does |
|---|---|
| `SlimeController` | The player character's state machine (`idle`, `run`, `fall`, `ability`, `ragdoll`...). `OldUpdate` reads input, casts abilities, starts jumps. Fields: `playerNumber`, `body`, `playerPhysics`, `abilities`. |
| `PlayerPhysics` | Movement: `Jump()`, `IsGrounded()`, gravity, sticking to platforms. Tunables: `jumpStrength`, `maxSpeed`, `gravity_accel`... |
| `PlayerBody` | Fixed-point transform/rigidbody: `position`, `selfImposedVelocity`, `externalVelocity`. |
| `PlayerHandler` / `Player` | Per-player data: inputs, `Scale`, lives, colors. `PlayerHandler.Get().GetPlayer(id)`. |
| `GameTime`, `Updater` | Simulation clock, time stop. |
| `AudioManager` | `AudioManager.Get().Play("slimeJump1")`. |
| Ability classes | `Roll`, `Beam`, `Dash`, `MeteorSmash`, `Machoman`, `BounceBall`, … each implements an ability. |
| `Constants`, `CharacterStatsList` | Global tuning values. |

---

## 6. Worked example: the Triple Jump mod

### Step 1 — find the code to change

Search the decompiled code for `jump`. Two relevant places:

`PlayerPhysics.Jump()` applies the jump velocity:

```csharp
public void Jump()
{
    jumpedThisFrame = true;
    Vec2 vec = ((!isGrounded) ? Vec2.up : attachedGround.currentNormal(body));
    ...
    body.selfImposedVelocity = vec3 * jumpKeptMomentum + up * jumpStrength;
    UnGround();
}
```

Note it already handles `!isGrounded` (it jumps straight up). So the physics supports air jumps; something just never calls it in the air.

`SlimeController.OldUpdate(Fix simDeltaTime)` (called each sim tick from `UpdateSim`):

```csharp
bool flag5 = player.jumpButton_PressedThisFrame();
...
if (suppressInput) { flag5 = false; ... }
...                                   // abilities may `return` early here
if (playerPhysics.IsGrounded() & flag5)
{
    Jump();
    bodyAnimator.UpdateAnimations();
}
```

There's the rule: **you can only jump when grounded.**

### Step 2 — pick the patch strategy

Options:
1. Transpiler to remove `IsGrounded() &`: precise, but fragile and fiddly.
2. **Postfix on `OldUpdate`**: after vanilla logic ran, if the player was in the air for the whole frame and pressed jump, call `playerPhysics.Jump()` ourselves. Simple and robust. ✔

We need "was grounded at frame start": vanilla can ground or un-ground the player during `OldUpdate`, and we must not double-jump on the same press as a ground jump. A **Prefix** records it into `__state`; the **Postfix** reads it.

### Step 3 — per-player state

Each player needs their own "air jumps left" counter. A static `int` would be shared by all 4 players. Use a `ConditionalWeakTable<SlimeController, AirJumpState>`: per-instance storage that is garbage-collected with the player object.

### Step 4 — the code (`mods/TripleJump/AirJumpPatches.cs`)

```csharp
[HarmonyPatch(nameof(SlimeController.OldUpdate))]
[HarmonyPrefix]
private static void OldUpdatePrefix(SlimeController __instance, out bool __state)
{
    __state = __instance.playerPhysics.IsGrounded();
}

[HarmonyPatch(nameof(SlimeController.OldUpdate))]
[HarmonyPostfix]
private static void OldUpdatePostfix(SlimeController __instance, bool __state)
{
    var physics = __instance.playerPhysics;
    var player = PlayerHandler.Get().GetPlayer(__instance.playerNumber);
    var state = GetState(__instance);
    if (physics.IsGrounded()) { state.Remaining = Plugin.AirJumps.Value; return; }  // refill on landing
    if (__state || state.Remaining <= 0) return;                                     // vanilla handled it / none left
    if (__instance.suppressInput || __instance.isInAbility) return;                 // same guards as vanilla
    // skip states where vanilla returns early (ragdoll, biting, ability, nothing)
    if (!player.jumpButton_PressedThisFrame()) return;                              // synced input!

    state.Remaining--;
    __instance.wasUpsideDown = false;
    physics.Jump();
    __instance.bodyAnimator.UpdateAnimations();
}
```

Plus two small postfixes on `Spawn`/`ReSpawn` to refill on (re)spawn. `isInAbility`, `currentState`, `wasUpsideDown`, `bodyAnimator` and `OldUpdate` are all private in the game; the publicizer lets us use them directly.

Why it's multiplayer-safe: it only uses simulation state and the synced jump input, so every client computes exactly the same air jumps (as long as everyone has the mod with the same `AirJumps`).

### Step 5 — build, run, verify

```bash
dotnet build mods/TripleJump -c Release
```

The build copies `TripleJump.dll` into `<profile>\BepInEx\plugins\caligola-TripleJump-dev\`. Start modded from the mod manager. In `LogOutput.log`:

```
[Info   :   BepInEx] Loading [Triple Jump 1.0.0]
[Info   :Triple Jump] Triple Jump 1.0.0 loaded (2 air jumps).
```

Then play a local match and press jump three times. (Disable other air-jump mods such as *DoubleJump* in the profile first.)

---

## 7. Testing and debugging

- **Logs first.** `LogOutput.log` in the profile has BepInEx, your `Logger.Log*` calls, Unity's `Debug.Log`, and exceptions with stack traces. Harmony patch failures appear at startup.
- **Live console:** set `[Logging.Console] Enabled = true` in `<profile>\BepInEx\config\BepInEx.cfg` to get a console window next to the game.
- **Inspect live objects:** UnityExplorer (F7) lets you find `SlimeController` instances, watch fields like `currentState` change, and tweak values (`jumpStrength`!) to prototype a mod before writing it.
- **Breakpoints with dnSpyEx:**
  1. In `<profile>\doorstop_config.ini` set `debug_enabled = true` (optionally `debug_suspend = true` to pause at startup).
  2. Start modded.
  3. dnSpyEx → *Debug* → *Start Debugging* → Debug engine **Unity (Connect)**, address `127.0.0.1`, port `10000`.
  4. Open your `TripleJump.dll` (the `.pdb` is deployed next to it) or `Assembly-CSharp.dll` and set breakpoints.
  Rider and Visual Studio (with the Unity tools) can attach the same way. Set `debug_enabled = false` when done.
- **Iteration loop:** close the game → `dotnet build` → start modded. BepInEx can't unload plugins, so a restart is needed each time.
- **Local multiplayer** (several gamepads / keyboard) is the quickest way to test gameplay mods with more players.

---

## 8. Packaging and publishing on Thunderstore

A Thunderstore package is a zip with, at its root:

| File | Notes |
|---|---|
| `manifest.json` | `name` (letters/digits/underscore), `version_number` (semver), `description` (≤250 chars), `website_url`, `dependencies` (e.g. `BepInEx-BepInExPack-5.4.2305`) |
| `icon.png` | exactly 256×256 |
| `README.md` | becomes the mod page |
| `CHANGELOG.md` | optional, shown as a tab |
| your files | loose DLLs at the root (or under `plugins/`) are installed into `BepInEx/plugins/<Team>-<Mod>/` |

You don't write the manifest by hand: `thunderstore.toml` + tcli generate it.

```bash
dotnet build mods/TripleJump -c Release -t:PackThunderstore   # → dist/caligola-TripleJump-1.0.0.zip
```

1. **Test the zip:** Thunderstore Mod Manager → *Settings* → *Import local mod* → select the zip. Remove the `-dev` folder from `plugins` first so the mod doesn't load twice.
2. **Create a team** on thunderstore.io (your package namespace). Set `ThunderstoreTeam` in `Directory.Build.props` to it and rebuild.
3. **Upload:** <https://thunderstore.io/c/bopl-battle/create/>, or automate with a team service account token:
   `tcli publish --config-path mods/TripleJump/thunderstore.toml --file dist/<zip> --token <token>`
4. **Updates:** bump `<Version>` in the `.csproj`, add a CHANGELOG entry, pack, upload. Versions can't be re-uploaded or deleted, so test first.

---

## 9. Common pitfalls

| Symptom | Cause / fix |
|---|---|
| Mod loads, patches are listed, but nothing happens | Something unpatched it, typically `UnpatchSelf()` in `OnDestroy()` (see §3). Prove it by writing to a file from the patch with `File.AppendAllText`. |
| Mod not in the log at all | DLL not in `BepInEx/plugins`, you started *vanilla*, or `[BepInProcess]` name is wrong. |
| `Could not load file or assembly` | You referenced something not available in the game (e.g. a newer `System.*`). Target `netstandard2.1`, use game/BepInEx DLLs only, `Private=false`. |
| Harmony `Undefined target method` at startup | Method name/signature changed after a game update. Re-decompile and fix the patch. |
| Online match desyncs/freezes | Gameplay mod not installed by everyone, different configs, or you used floats/randomness/local input in sim code. |
| Behavior applies to all players at once | You stored per-player state in a static field. Use `ConditionalWeakTable` or a dictionary keyed by the instance. |
| Works in a build, broken after an update | Normal for mods. Keep patches small and targeted so updates break less. |

---

## 10. Ideas for your next mods

Ordered roughly by difficulty:

- **Config tweaks:** change `PlayerPhysics.jumpStrength`, `maxSpeed`, `gravity_accel` in a postfix on `PlayerPhysics.Awake`/`Init`.
- **Cosmetic/UI:** an `OnGUI` overlay with each player's air-jumps left, custom colors, sound replacements (client-side, no desync risk).
- **Ability tweaks:** change cooldowns or effects of a specific ability class (`Roll`, `Beam`, `Dash`...).
- **New rules:** e.g. low gravity rounds, wall jumps (look at `attachedGround.currentNormal`), size changes on kill.
- **New ability:** see `mods/SpawnBot` for a working example without any asset files. It subclasses `AbilityMonoBehaviour`, puts it on a runtime-created inactive template GameObject, draws the icon into a `Texture2D`, and appends a `NamedSprite` to the game's `abilityIcons` list before the selection grid is built. For abilities with real visuals (animations, projectiles), you'd build an AssetBundle with Unity 2022.3.60 and load the prefab from it instead.
