# Toolchain

Every tool, the version that works, and — just as important — the ones that look
right and are not.

---

## Summary

| Tool | Use | Status |
|---|---|---|
| **Dumper-7** | SDK + `.usmap` from the live process | Works |
| **retoc** (`Utils/retoc-src`) | Zen ↔ Legacy container conversion | **Use this build** |
| retoc (`Utils/retoc-v015`, PR #70) | was meant to fix new-package registration | **Broken — see below** |
| **repak** | legacy `.pak` inspection / creation | Works |
| **UAssetAPI** (`Utils/UAssetParser`) | read/edit legacy `.uasset` | Works, with a version gotcha |
| **Unreal Editor 5.7** | cooking, FBX import, asset ops | Works |
| **Blender 5.2.1** (via Steam) | mesh assembly, rigging, alignment | Works headless |
| **UE4SS** | runtime Lua hooks in the shipped game | Works, auto-loads |
| CUE4Parse | container parsing | **Fails on this game** |
| UnrealMappingsDumper | `.usmap` extraction | **Crashes the game** |

---

## Dumper-7

Inject into `Geronimo-Win64-Shipping.exe` — produces a full C++ SDK and a `.usmap`
with 12,463 schemas at:

```
C:\Dumper-7\5.7.4-51494982+++UE5+Release-5.7-Geronimo\
  Mappings\5.7.4-51494982+++UE5+Release-5.7-Geronimo.usmap
```

**Do not use UnrealMappingsDumper** — it crashed the game twice on UE 5.7 due to
stale hardcoded offsets (upstream issue #75, unfixed). Dumper-7 auto-detects and
worked first try.

---

## retoc — which build, and why it matters

Use **`Utils/retoc-src/target/release/retoc.exe`**.

`Utils/retoc-v015` (tag v0.1.5 + PR #70) fails **every** `to-zen` conversion with
`Error: failed to fill whole buffer` on *any* input, including staging directories
it had previously converted successfully. It is not an asset problem — bisecting
per-asset is wasted effort, the failure is global. It was built to chase the
new-package blocker, which turned out to have a different cause entirely (see
[findings](findings.md)).

Useful subcommands:

```bash
retoc info     <utoc>           # container id, flags, versions, package count
retoc list     <utoc>           # chunk ids + types - ALWAYS check before deploying
retoc manifest <utoc>           # package names -> pakstore.json
retoc to-legacy <paks> <out> --version UE5_7
retoc to-zen    <stage> <out.utoc> --version UE5_7
retoc print-script-objects <global.utoc>
retoc unpack-raw <utoc> <dir>   # raw chunks, for header inspection
```

---

## UAssetAPI — the version gotcha

```csharp
var mappings = new UAssetAPI.Unversioned.Usmap(usmapPath);
var asset = new UAsset(path, EngineVersion.VER_UE5_6, mappings);   // 5_6, NOT 5_7
```

`VER_UE5_7` throws `OutOfMemoryException` despite the game being 5.7.4. The usmap is
mandatory (the packages use unversioned properties). This is the single most
important tooling gotcha in the project.

`Utils/UAssetParser` modes: default (CUE4Parse provider — largely useless here),
`legacy`, `probe`, `kismet`, `imports`, `bounds`.

The built exe lands in `bin/Debug/net10.0/`, while a stale `net9.0` build also
exists — running the old one silently ignores new modes and falls through to the
default provider mode. Always pick the newest:

```bash
U=$(ls -t Utils/UAssetParser/bin/Debug/*/UAssetParser.exe | head -1)
```

---

## CUE4Parse

Fails to mount this game's containers:

```
ParserException: Invalid bool value (...)
  at FIoContainerHeaderSoftPackageReferences..ctor
```

It fails **identically on the game's own container and on ours**, at the same struct
and offset — which is itself useful evidence that our containers are well-formed.
A CUE4Parse-vs-5.7 gap, not a mod problem. FModel is affected for the same reason.

---

## Unreal Editor 5.7

Cooked assets are **not version-portable** — 5.7 is required because the game is
5.7.4. In the Epic Launcher, 5.8 is the default; pick 5.7.x from the version dropdown.

Project config that matters (`UEProject/Geronimo/Config/DefaultGame.ini`):

```ini
[/Script/UnrealEd.ProjectPackagingSettings]
+DirectoriesToAlwaysCook=(Path="/Game/Weapons/HK416a5")
bGenerateChunks=False
bShareMaterialShaderCode=False
bSharedMaterialNativeLibraries=False
```

Without `DirectoriesToAlwaysCook` the cook silently produces nothing.

`DefaultEngine.ini` needs this for headless FBX import — Interchange crashes the
commandlet:

```ini
[ConsoleVariables]
Interchange.FeatureFlags.Import.FBX=False
```

Python API gotchas found the hard way:

- `unreal.PrimaryAssetLabelFactory` **does not exist** — use `unreal.DataAssetFactory`
  with `data_asset_class = unreal.PrimaryAssetLabel`.
- `SkeletalMesh.skeleton` is **read-only** — to move a skeleton onto another package
  path use `EditorAssetLibrary.rename_asset` (it auto-fixes references), then delete
  the leftover `ObjectRedirector`s.
- The property is `label_assets_in_my_directory`, not `b_label_assets_in_my_directory`.
- A script must end with `unreal.SystemLibrary.quit_editor()`, and save explicitly
  (`save_directory`) before it — otherwise changes are lost.
- UAT `BuildCookRun` fails with *"Missing receipt Geronimo.target"* if the `.uproject`
  contains an empty `"Modules": []`. Remove the key.
- Run UAT through PowerShell with `& $uat`; invoking it from Bash mangles the quoting
  and dies with `'C:\Program' is not recognized`.

---

## Blender 5.2.1

Installed **via Steam**:
`C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe`.
Headless Python works: `blender.exe --background --python script.py`

Three gotchas worth remembering for any model:

- Source models may point **+Y**; Geronimo needs **+X** forward — bake a −90°
  rotation about Z.
- Object-level rotation is unreliable when FBX objects have parents. Bake into
  **mesh data** instead, then identity the object:

  ```python
  o.data.transform(ROT @ o.matrix_world)
  o.matrix_world.identity()
  ```

  Verify in-session; an FBX round-trip adds axis-conversion noise that looks like a bug.

- **UE's FBX import negates Y.** Blender `Y[-2.311..4.380]` arrives in UE as
  `Y[-4.380..2.311]`. X and Z pass through unchanged. Always confirm imported bounds
  against Blender's before trusting an offset — applying a Y correction without
  accounting for this doubles the error instead of fixing it.

---

## UE4SS

Installed as a `dwmapi.dll` proxy in `<game>\Binaries\Win64\`; auto-detects
everything on 5.7 with no manual offsets. Mods live in `ue4ss\Mods\<Name>\` and are
enabled by an `enabled.txt` in the mod folder (the modern mechanism — the legacy
`mods.txt` does not need editing).

**`ue4ss\UE4SS.log` is the only diagnostic channel that works.** The shipping build
has logging compiled out: `-log` does nothing and `Saved\Logs\Geronimo.log` is stale
(the newest entries in `Saved\Crashes\` are months old). Anything `print()`ed from
Lua lands in `UE4SS.log` and can be read from outside the game — this is how every
in-game measurement in this project was taken.

Lua gotchas:

- `LoadAsset` must run inside `ExecuteInGameThread` ("can only be called from within
  the game thread").
- Never pass Lua tables for native struct / out-array parameters — it crashes the game.
- `map:Find()` returns an unusable value; use `map:ForEach()` and `:get()` the key
  and value.
- The gun-array struct field is GUID-suffixed:
  `GunAssets_9_E68A935B442B9DB0A278E7B5893C9566`.
- Appending to a `TArray` works via index assignment: `arr[n+1] = obj`.
- The mod only re-reads `main.lua` at process start — the game must be fully closed
  and relaunched for a script change to take effect.

---

## Running the game for testing

The game **hard-requires a live OpenXR runtime**. Without a headset it exits during
VR init:

```
Error [GENERAL | xrCreateInstance | OpenXR-Loader] :
  xrCreateInstance called with invalid API version 1.1. Max supported version is 1.0
```

`-nohmd`, `-emulatestereo` and `-nullrhi` **do not bypass this** — all three still
exit with code 1, though UE4SS does initialise first (its log fills in). Automated
headless testing is therefore impossible; a human with a headset has to launch it.
`timeout` also will not kill the GUI process — it leaves a multi-GB zombie behind;
use `Stop-Process -Force`.

Because each test run costs a person putting on a headset, design every run to
answer **all** open questions at once, and read the answers out of `UE4SS.log`
afterwards rather than asking for observations.
