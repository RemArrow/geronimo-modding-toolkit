# Build pipeline

End-to-end: your model → geometry in the game. Each stage names the check that proves
it worked — do not skip those, several of them are the only thing standing between you
and a crash on load.

```
Blender            UE 5.7 editor        UE 5.7 cook        retoc            game
  assemble   ->      import       ->      cook       ->    to-zen    ->   Content\Paks
  + align            + place on                             .utoc
                     game path                              .ucas
```

Before anything else, read [findings.md](findings.md). It tells you what is and is not
possible in this build, and will save you from the three approaches that crash the game.

---

## Stage 0 — know what you are overriding

New package IDs **do not register** in this build, so custom geometry has to replace an
existing package. Establish two things first:

**What class the target actually is.** Folder names lie —
`/Game/Weapons/HK416a5/Mesh/` mostly contains *materials*, not meshes:

```bash
retoc to-legacy <game>/Content/Paks <out> --version UE5_7
grep -aoE "StaticMesh|MaterialInstanceConstant|SkeletalMesh" <asset>.uasset | sort -u
```

**What extent it covers.** Use `tools/UAssetParser` in `bounds` mode. It prints
`ExtendedBounds` and the blueprint's component `RelativeLocation`s. If the muzzle
anchor falls inside the mesh's own bounds, that package is the *whole rifle*, not a
receiver — your replacement must cover the same extent.

---

## Stage 1 — assemble and align the mesh (Blender)

`tools/blender/measure_parts.py` then `tools/blender/assemble_rifle.py`.

```bash
blender.exe --background --python tools/blender/measure_parts.py
blender.exe --background --python tools/blender/assemble_rifle.py
```

Measure first, then set `DELTA` in `assemble_rifle.py` from the comparison. Derive it by
matching bounding boxes and checking **both edges of every axis**:

| axis | target | yours | Δ min-edge | Δ max-edge |
|---|---|---|---|---|
| X | −35.920 … 40.738 | −28.500 … 48.400 | −7.420 | −7.662 |
| Y | −2.413 … 4.396 | −2.800 … 3.900 | +0.387 | +0.496 |
| Z | −12.275 … 6.547 | −9.900 … 8.600 | −2.375 | −2.053 |

Agreement between the two deltas (here within ~0.2 cm) is what proves the models share
an envelope and that a **translation** is the right fix. If they disagree badly, you have
the wrong part set — go back to stage 0 rather than reaching for scale.

Two traps:

- Bake transforms into **mesh data**, not object transforms. FBX objects have parents,
  which makes `matrix_world` assignment get reinterpreted.
- **Unreal's FBX import negates Y.** Apply Y corrections with the sign flipped, and
  verify after import — getting this wrong doubles the error instead of fixing it.

**Check:** the script prints `FINAL cm` against `TARGET cm`.

---

## Stage 2 — import and place on the game's package path

`tools/unreal/print_bounds.py` verifies; your own import script does the work.

Import with **`import_materials=False`** and **`import_textures=False`**. This is not
optional — importing materials creates material packages that cook onto game paths and
override the real ones, which makes the spawner UI disappear.

Then duplicate the mesh onto the exact game package path you are overriding.

**Check:** imported bounds vs target, remembering the Y negation.

---

## Stage 3 — cook

```bash
UnrealEditor-Cmd.exe <project>.uproject \
  -run=Cook -TargetPlatform=Windows -unversioned -stdout -unattended -nosplash
```

Needs `+DirectoriesToAlwaysCook=(Path="/Game/...")` in `DefaultGame.ini` or it silently
cooks nothing. Also set:

```ini
bGenerateChunks=False
bShareMaterialShaderCode=False
bSharedMaterialNativeLibraries=False
```

**Check — the gate that catches most disasters:**

```bash
find Saved/Cooked/Windows/<Project>/Content -name "*.uasset" | sed "s|.*/Content/||"
```

Must list **exactly** the packages you intend. Any extra material, texture or engine
asset here will override game content. Delete it from the project and re-cook — stubs
regenerate every cook until removed.

---

## Stage 4 — stage and pack to a Zen container

Staging layout mirrors the game's tree, rooted at the project name:

```
stage/
└── <Project>/Content/Weapons/<Target>/Mesh/
    ├── <Asset>.uasset
    └── <Asset>.uexp
```

**Do not** put `scriptobjects.bin` at the stage root — retoc will emit a `ScriptObjects`
chunk that overrides the game's global script-object table.

```bash
retoc to-zen --version UE5_7 stage/ out/zzz_MyMod_P.utoc
```

**Check:**

```bash
retoc list out/zzz_MyMod_P.utoc
```

A clean single-package mod container is exactly two chunks and nothing more:

```
zzz_MyMod_P   <id>00000001   ExportBundleData
zzz_MyMod_P   <id>00000006   ContainerHeader
```

The `ExportBundleData` id must equal the game's id for that package — verify with
`retoc manifest` on the game's `.utoc`. That collision is what makes the override land.

---

## Stage 5 — deploy

**Close the game first.** Containers are file-locked while it runs, and the copy will
silently target a locked file.

Ship three files into `<game>\Content\Paks\`:

| File | Why |
|---|---|
| `.utoc` / `.ucas` | the actual content |
| `.pak` (minimal, ~394 B) | a pak with a real `../../../` mount point |

The pak matters more than it looks. Do **not** use a UAT-staged pak (it carries
`Engine/Config/Base*.ini` → global shadermap crash), and note the engine's own chunk pak
lists zero files with mount point `/`. A minimal pak containing one dummy file with
mount point `../../../` is what you want.

The `zzz_` prefix makes it mount last.

---

## Stage 6 — the UE4SS mod (optional)

To add a spawner entry, copy `tools/ue4ss/AddGunEntry/` to:

```
<game>\Binaries\Win64\ue4ss\Mods\AddGunEntry\
```

The folder needs its `enabled.txt`. Edit the config block at the top of `main.lua`.

**The game must be fully closed and relaunched** for a script change to take effect —
UE4SS reads `main.lua` only at process start.

---

## Stage 7 — verify

The shipping build has logging compiled out, so verification runs through `UE4SS.log`:

```bash
L="<game>/Binaries/Win64/ue4ss/UE4SS.log"
: > "$L"                                  # clear before launching
grep -aE "\[AddGunEntry\]" "$L" | sed 's/^\[[^]]*\] \[Lua\] //'
```

Launching needs a VR headset (the game exits at OpenXR init without one, and `-nohmd`
does not bypass it), so make each run answer every open question at once.

Proving a container is actually mounted, while the game runs:

```powershell
try { $fs=[System.IO.File]::Open($path,'Open','Write','None'); $fs.Close(); "UNLOCKED" }
catch { "LOCKED (mounted)" }
```

Expect `.utoc` and `.ucas` locked, `.pak` unlocked — the game never mounts extra legacy
paks.
