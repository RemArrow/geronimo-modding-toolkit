# Tools

Everything in `tools/` is a starting point with a config block at the top, not a
turnkey build. Paths and asset names need filling in for your own mod.

---

## `tools/UAssetParser/` — C#

Reads legacy (non-Zen) `.uasset` files via UAssetAPI. Convert the game's containers
first with `retoc to-legacy`.

```bash
dotnet build -c Debug
```

| Mode | Args | Purpose |
|---|---|---|
| `bounds` | `<uasset>` | `ExtendedBounds`, material slots, and component `RelativeLocation`s. **The most useful mode** — this is how you get a blueprint's attach anchors |
| `imports` | `<uasset> [usmap]` | Import/export table dump |
| `kismet` | `<uasset> [fn]` | Blueprint bytecode disassembly |
| `probe` | `<uasset>` | Finds which property carries a package's declared name (retoc reads that, not the file path) |

Two things to set before it runs:

- **The usmap path is hardcoded** near the top of each mode's source. Point it at your
  own Dumper-7 output.
- **Use `EngineVersion.VER_UE5_6`**, never `VER_UE5_7`. `VER_UE5_7` throws
  `OutOfMemoryException` despite the game being 5.7.4. The usmap is mandatory —
  the packages use unversioned properties.

```csharp
var mappings = new UAssetAPI.Unversioned.Usmap(usmapPath);
var asset = new UAsset(path, EngineVersion.VER_UE5_6, mappings);
```

The build lands in `bin/Debug/net10.0/`. If an older `net9.0` build is also present,
running it silently ignores newer modes and falls through to the default pak scan —
pick the newest binary.

---

## `tools/ue4ss/AddGunEntry/` — Lua

Adds a new gun entry to the spawner UI, and probes whether custom packages load.

Copy the whole folder to `<game>\Binaries\Win64\ue4ss\Mods\AddGunEntry\`. The
`enabled.txt` is what turns it on (the modern UE4SS mechanism — the legacy `mods.txt`
does not need editing).

Config block at the top sets the category, display name, donor asset, and which
packages to probe.

Two design points worth preserving if you modify it:

- **It builds the GunAsset at runtime** with `StaticConstructObject` rather than shipping
  a cooked DataAsset. A runtime `UObject` needs no package, which sidesteps the
  new-package blocker entirely. This is the only known way to add a spawner entry.
- **The probe keeps real game packages as controls.** Without them, a failure is
  ambiguous between "my package didn't load" and "the probe doesn't work". Keeping
  controls in is what turned a week of guesswork into a definitive answer — don't
  remove them.

Everything it prints lands in `ue4ss\UE4SS.log`, readable from outside the game. In a
shipping build that is the only diagnostic channel that works.

---

## `tools/blender/` — Python

Run headless: `blender.exe --background --python <script>`

### `measure_parts.py`

Prints per-part and combined bounds for a folder of part FBXs. **Run this first** — the
combined bounds are what you compare against the target package to derive the alignment.

### `assemble_rifle.py`

Joins the parts into one static mesh, bakes an alignment offset into mesh data, exports
a single FBX.

`DELTA` at the top is the alignment offset in **metres** — the one knob to turn when the
gun sits wrong in-game.

Bakes into **mesh data** rather than object transforms deliberately: FBX objects have
parents, which makes `matrix_world` assignment unreliable.

---

## `tools/unreal/` — Python

Run with:

```bash
UnrealEditor.exe <project>.uproject -ExecutePythonScript="<script>" \
  -nosplash -stdout -unattended
```

Then grep the output for `RESULT`.

### `print_bounds.py`

Prints origin, extent and per-axis range for a list of assets. Use it after import to
confirm the mesh landed where you intended — in particular to catch **Unreal's FBX
import negating Y**.

### `verify_skeleton.py`

Prints a skeletal mesh's skeleton and bone count. Cooked animation stores bone
*indices*, so a mismatched skeleton crashes the game at load. Geronimo's HK416 skeleton
has 13 `Root*` bones; if your rig doesn't reproduce that exactly, in order, don't ship
a skeleton at all.

### `reset_overrides.py`

Deletes override folders, clears leftover `ObjectRedirector`s, lists what survives. Run
it whenever an experiment has left assets sitting on game package paths — stale
overrides are easy to forget and silently ship on the next cook.

---

## Editor Python gotchas

Collected so you don't rediscover them:

- `unreal.PrimaryAssetLabelFactory` **does not exist** — use `unreal.DataAssetFactory`
  with `data_asset_class = unreal.PrimaryAssetLabel`.
- The property is `label_assets_in_my_directory`, not `b_label_assets_in_my_directory`.
- `SkeletalMesh.skeleton` is **read-only**. To move a skeleton onto another package path
  use `EditorAssetLibrary.rename_asset` (it auto-fixes references), then delete the
  redirectors.
- Save explicitly (`save_directory`) **before** `quit_editor()`, or changes are lost.
- Headless FBX import crashes unless Interchange is off:
  `Interchange.FeatureFlags.Import.FBX=False` under `[ConsoleVariables]`.
- Run UAT through PowerShell with `& $uat`; from Bash the quoting mangles into
  `'C:\Program' is not recognized`.
- UAT fails with *"Missing receipt"* if the `.uproject` has an empty `"Modules": []`.
  Remove the key.
