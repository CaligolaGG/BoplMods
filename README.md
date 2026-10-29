# BoplMods

Mods for **Bopl Battle**, built with **BepInEx 5 + Harmony**, packaged for **Thunderstore** (Thunderstore Mod Manager / r2modman).

New to modding? Read [TUTORIAL.md](TUTORIAL.md).

## Requirements

| What | Why | Status on this machine |
|---|---|---|
| .NET SDK 8+ (you have 9 and 10) | builds the mods (target is `netstandard2.1`) | installed |
| Bopl Battle (Steam) | game DLLs are referenced from the install folder | `C:\Program Files (x86)\Steam\steamapps\common\Bopl Battle` |
| Thunderstore Mod Manager with a Bopl Battle profile + **BepInExPack** | loads the mods | `Default` profile, BepInExPack 5.4.2305 |
| `ilspycmd` (dotnet global tool) | decompile the game from the CLI | installed |
| `tcli` (dotnet global tool) | builds Thunderstore zips / publishes | installed |
| BepInEx templates (`dotnet new bepinex5plugin`) | optional, scaffold mods outside this repo | installed |
| `tools/dnSpyEx` | GUI decompiler + debugger | downloaded |
| `tools/UnityExplorer` | in-game inspector of scenes/objects | downloaded (not installed in profile) |

Paths differ on another PC? Copy `Directory.Build.user.props.example` to `Directory.Build.user.props` and edit it.

## Everyday workflow

```bash
# 1. build: compiles and copies the DLL into the mod manager profile
dotnet build mods/TripleJump -c Release

# 2. Thunderstore Mod Manager -> Bopl Battle -> profile "Default" -> "Start modded"

# 3. check the log
#    %APPDATA%\Thunderstore Mod Manager\DataFolder\BoplBattle\profiles\Default\BepInEx\LogOutput.log
```

The dev build is deployed to `BepInEx\plugins\<Team>-<Mod>-dev\`. The mod manager doesn't list it as a mod (it wasn't installed through it), but BepInEx loads it. Delete that folder to remove it.

## Making a release

```bash
dotnet build mods/TripleJump -c Release -t:PackThunderstore
# -> dist/caligola-TripleJump-1.0.1.zip
```

- **Try it locally like a user would:** Thunderstore Mod Manager → *Settings* → *Import local mod* → pick the zip. (Delete the `-dev` folder first, or the mod loads twice.)
- **Publish:** create a team on thunderstore.io, set `ThunderstoreTeam` in `Directory.Build.props` to that name, then upload the zip on <https://thunderstore.io/c/bopl-battle/create/> or run `tcli publish --file dist/<zip> --token <service-account-token>`.
- Bump `<Version>` in the `.csproj` and update `CHANGELOG.md` before every release.

## Mods

| Mod | Description |
|---|---|
| [TripleJump](mods/TripleJump/README.md) | Up to 2 extra jumps in mid-air (configurable). |
| [CaliSkills](mods/CaliSkills/README.md) | Pack of new abilities. Pinball: a bow-shot ball that bounces for 10 s and knocks players back. Tuning in `mods/CaliSkills/Abilities/Pinball/PinballSettings.cs`. |
| [SpawnBot](mods/SpawnBot/README.md) | Dev tool: a SpawnBot ability that spawns idle bot players to test offensive abilities on (local games only). |

Reference: [docs/ABILITIES.md](docs/ABILITIES.md) lists the game's abilities and their IDs.

## Adding a new mod

1. Copy `mods/TripleJump` to `mods/MyMod`, rename `TripleJump.csproj` → `MyMod.csproj`.
2. In the `.csproj` change `AssemblyName`, `RootNamespace`, `BepInExPluginGuid`, `BepInExPluginName`, `Description`, reset `Version`.
3. In `thunderstore.toml` change `name`, `description`, and the `build.copy` DLL path.
4. Replace the patches, README, CHANGELOG and icon (256×256 PNG).
5. `dotnet sln add mods/MyMod/MyMod.csproj`

## Multiplayer warning

Bopl Battle uses deterministic lockstep networking. A mod that changes gameplay must be installed, with the same config, by **every** player in the lobby, or the match desyncs.
