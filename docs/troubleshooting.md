# Troubleshooting

Crash signature → root cause → fix. Every entry here was hit for real.

---

## `EXCEPTION_ACCESS_VIOLATION reading address 0xffffffffffffffff`

The generic "a package override broke an import" crash. Happens during load, before
the main menu. Two distinct causes:

### Cause A — overwrote a material package with a mesh

`/Game/Weapons/HK416a5/Mesh/` contains **materials**, not part meshes.
`stock_ar15_HK_slim`, `pistol_grip_HK_BGB`, `flash_hider_USGI_001`,
`HK_416_handguard_…`, `HK_416_barrel_…` and `HK_416_receiver_RAL8000` are all
`MaterialInstanceConstant`. `HK_416_279_v2` imports them by name; replacing them
with meshes nulls those imports.

**Fix:** override only `HK_416_279_v2`. Verify class first:

```bash
grep -aoE "StaticMesh|MaterialInstanceConstant|SkeletalMesh" <asset>.uasset | sort -u
```

### Cause B — overrode a skeleton

Cooked animation stores **bone indices**, not names. The HK416 skeleton has 13
`Root*` bones; our rig builds 7. Matching names is not enough — order matters.

**Fix:** never override `*_movables_Skeleton`. Ship static meshes only.

---

## `Could not load section N (of 334) of the global shadermap`

Caused by deploying a **UAT-staged `.pak`**, which carries `Engine/Config/Base*.ini`.

**Fix:** ship only `.utoc` + `.ucas` plus a minimal 394-byte mount pak.
Sanity check:

```bash
grep -c "Engine/Config" <your>.pak    # must be 0
```

---

## The spawner UI disappears

Game loads, but the gun list is gone. Two causes, both "we overrode content we
shouldn't have":

1. **Empty material stubs.** Auto-created `MaterialInstanceConstant`s on game
   package paths override the real materials with blanks. They **regenerate on every
   cook** until deleted from the UE project.
2. **Engine/plugin content.** A full UAT stage pulls in ~301 `/Engine/` packages plus
   ControlRig, Interchange, Paper2D etc., which override the game's own.

**Fix:** the cook must produce exactly the packages you intend — check before packing:

```bash
find Saved/Cooked/Windows/Geronimo/Content -name "*.uasset" | sed "s|.*/Content/||"
```

Isolate with a `PrimaryAssetLabel` chunk if a full stage is genuinely needed.

---

## Geometry renders but is misaligned

The replacement mesh doesn't match what the blueprint's attachment points expect.

**Most likely:** you shipped only part of the rifle. `HK_416_279_v2` is the **whole
gun** (76.66 cm, muzzle anchor at X 35.9), not a receiver.

**Second most likely:** the Y axis. UE's FBX import negates Y; applying a Y
correction without flipping its sign doubles the error.

See [pipeline.md](pipeline.md#stage-1--assemble-and-align-the-mesh-blender).

---

## Game exits immediately, no window

```
Error [GENERAL | xrCreateInstance | OpenXR-Loader] :
  xrCreateInstance called with invalid API version 1.1. Max supported version is 1.0
```

The game requires a live OpenXR runtime. `-nohmd`, `-emulatestereo` and `-nullrhi`
do **not** bypass it. A headset has to be connected and its runtime running.

UE4SS still initialises first, so a populated `UE4SS.log` with no game window means
exactly this.

---

## A multi-GB `Geronimo-Win64-Shipping.exe` lingers

`timeout` sends SIGTERM, which a Windows GUI process ignores — it survives as a
zombie holding file locks on the containers.

```powershell
Stop-Process -Name Geronimo-Win64-Shipping -Force
```

---

## Mod changes don't take effect

- **Lua:** UE4SS reads `main.lua` only at process start. Fully close and relaunch.
- **Containers:** file-locked while the game runs; the copy silently targets a locked
  file. Close the game before deploying.

Confirm which container the running game actually has open:

```powershell
try { $fs=[System.IO.File]::Open($path,'Open','Write','None'); $fs.Close(); "UNLOCKED" }
catch { "LOCKED (mounted)" }
```

Expect `.utoc` and `.ucas` locked, `.pak` unlocked — the game never mounts extra
legacy paks.

---

## `retoc to-zen` → `Error: failed to fill whole buffer`

You're running `Utils/retoc-v015`. It fails on **all** input, including previously
working stages. Use `Utils/retoc-src/target/release/retoc.exe`.

---

## UAssetAPI throws `OutOfMemoryException`

Wrong engine version constant. Use `EngineVersion.VER_UE5_6` — **not** `VER_UE5_7`,
despite the game being 5.7.4. The usmap is also mandatory.

---

## `UAssetParser` ignores a mode and dumps the pak scan instead

You ran the stale `bin/Debug/net9.0/` build. Use the newest:

```bash
U=$(ls -t Utils/UAssetParser/bin/Debug/*/UAssetParser.exe | head -1)
```

---

## The cook produces nothing

Missing `+DirectoriesToAlwaysCook=(Path="...")` in `DefaultGame.ini`.

---

## UAT: `Missing receipt Geronimo.target`

The `.uproject` contains an empty `"Modules": []`. Remove the key.

---

## UAT: `'C:\Program' is not recognized`

Quoting mangled by invoking `RunUAT.bat` from Bash. Run it through PowerShell:

```powershell
& "C:\Program Files\Epic Games\UE_5.7\Engine\Build\BatchFiles\RunUAT.bat" BuildCookRun -project="$proj" ...
```

---

## Editor Python: changes silently lost

Save explicitly before quitting:

```python
unreal.EditorAssetLibrary.save_directory("/Game/Weapons", False, True)
unreal.SystemLibrary.quit_editor()
```

---

## Headless FBX import crashes the editor

Interchange. Disable it in `DefaultEngine.ini`:

```ini
[ConsoleVariables]
Interchange.FeatureFlags.Import.FBX=False
```

---

## The gun list is duplicated

`BP_GameInstance::GetItems()` is **additive**. Calling it manually a second time
duplicates the entire UI list. Never call it.

---

## CUE4Parse / FModel can't open the containers

```
ParserException: Invalid bool value (...)
  at FIoContainerHeaderSoftPackageReferences..ctor
```

A CUE4Parse-vs-5.7 gap. It fails identically on the game's own container, so it says
nothing about your mod. Use `retoc` for container work.
