# Findings — what's true, what's proven impossible

This is the most valuable file in the repo. Every entry below was established
empirically, and several of them cost multiple game crashes to learn. **Read this
before attempting anything new.**

---

## 1. New package IDs cannot register in this build

**Status: proven, not assumed. Stop trying.**

The controlled in-game probe ran game packages and our new packages through the
*identical* code path in the same session:

```
==== CONTROLS (game packages) ====
  GAME mesh (HK416 body)   find=yes  load=yes  after=YES
  GAME mesh (HK stock)     find=yes  load=yes  after=YES
  GAME data (donor)        find=yes  load=yes  after=YES
==== OURS (new packages) ====
  OURS Body                find=no   load=no   after=no
  OURS Grip                find=no   load=no   after=no
==== VERDICT: controls 3/3 loaded, ours 0/2 loaded ====
```

0 of 424 resident static meshes matched our mod's name. Both the `Package.Object` and
bare package-path forms fail.

### What was ruled out, and how

| Hypothesis | How it was eliminated |
|---|---|
| Container malformed | Header dumped byte-by-byte: signature `0x496F436E`, version 5, container ID, 45 packages, entry array 728 B = 45 × 16 + 8. All correct. |
| Wrong header version | `retoc info` — ours `SoftPackageReferencesOffset`, game's identical |
| Wrong TOC version | Ours `ReplaceIoChunkHashWithIoHash`, game's identical |
| Wrong package names | `retoc manifest` — exactly the exact paths we shipped  |
| Wrong package IDs | retoc's hashing is provably correct: an override only collided with the game's real chunk because the computed IDs matched |
| Wrong export object name | Name table in the cooked package really does contain the expected export name |
| Missing ScriptObjects | The game's `global.utoc` already provides chunk `…0005` |
| Missing NavigationSystem | `print-script-objects` shows the game has `/Script/NavigationSystem` and `NavCollision` |
| Container not mounted | File-lock test against the running process: our `.utoc` and `.ucas` are **locked**, same as the game's |
| retoc-specific bug | An **engine-built** container (UAT `-pak -iostore`, isolated via `PrimaryAssetLabel` chunk) fails identically |
| Bad/empty mount pak | Swapped the engine's empty 339-byte pak for a 394-byte one with a real `../../../` mount point — no change |
| Probe method broken | The controls above — 3/3 game packages load through the same call |

### The one asymmetry that explains it

Our `.utoc`/`.ucas` are opened by the IoDispatcher (hence file-locked, hence chunk
*overrides* work), but our `.pak` is **never mounted** — the game only mounts its own
`Geronimo-Windows.pak`. Package-store registration happens on the pak mount path
(`IPlatformFilePak.cpp` ~6033–6085), so the header that declares our *new* packages
never gets ingested.

That is exactly consistent with everything observed: overrides of existing package
IDs work (the store entry already exists, served from the base container), while new
IDs never appear.

### Consequence

Geometry can only be delivered by **overriding an existing package**. Combined with
`GunAsset` having no mesh field, a new spawner entry **cannot** have unique geometry
in this build.

---

## 2. GunAsset has no mesh field

Dumped live from the donor DataAsset — the complete property list is:

```
ReceiverClass   ClassProperty
Category        ByteProperty
DisplayName     TextProperty
bEnabled        BoolProperty
Icon            ObjectProperty
```

A spawner entry's geometry comes **entirely** from its `ReceiverClass` blueprint.
There is no per-entry mesh override to set.

---

## 3. Adding a spawner entry works, and is genuinely additive

Runtime construction sidesteps the package blocker completely — a runtime `UObject`
needs no package:

```lua
local cls    = StaticFindObject("/Game/Gameplay/Customization/GunAsset.GunAsset_C")
local newGun = StaticConstructObject(cls, gi, FName("DA_MyCustomGun"))
for _, p in ipairs({"ReceiverClass","Icon","bEnabled"}) do newGun[p] = donor[p] end
newGun.Category    = 0                 -- Assault Rifle
newGun.DisplayName = FText("My Custom Gun")
arr[arr:GetArrayNum() + 1] = newGun    -- append by index assignment
```

Result, reproduced on every run: `*** ADDED 'My Custom Gun' (10 -> 11) ***`.
Nothing is removed or replaced.

---

## 4. `HK_416_279_v2` is the whole rifle, and its siblings are materials

This misreading caused three crashes and the vanishing-UI bug.

`/Game/Weapons/HK416a5/Mesh/` **looks** like modular part meshes. In reality only
`HK_416_279_v2` (StaticMesh, the entire rifle, 8 material slots) and
`HK_416_279_movables` (SkeletalMesh) are geometry. `stock_ar15_HK_slim`,
`pistol_grip_HK_BGB`, `flash_hider_USGI_001`, `HK_416_handguard_…`,
`HK_416_barrel_…` and `HK_416_receiver_RAL8000` are **MaterialInstanceConstants**,
imported by name from the mesh.

Overwriting them with static meshes nulls those imports and produces
`EXCEPTION_ACCESS_VIOLATION reading 0xffffffffffffffff` at load.

**Always verify a package's real class before overriding it:**

```bash
grep -aoE "StaticMesh|MaterialInstanceConstant|SkeletalMesh" <asset>.uasset | sort -u
```

---

## 5. Never override a skeleton

Cooked animation data stores **bone indices, not names**. Matching bone *names* is
necessary but nowhere near sufficient — the hierarchy must have the same bones in
the same order.

The HK416 skeleton has 13 `Root*` bones; a 7-bone rig will not do.
Overriding it crashes with the same access violation.

**Safe pattern:** override static meshes only, leave `*_movables` and its skeleton
stock. The moving parts sit mostly inside the receiver, so the silhouette still
reads as the new gun.

---

## 6. Container hygiene — three things that will break the game

| Do not ship | Symptom | Why |
|---|---|---|
| A `ScriptObjects` chunk (`000000000000000000000005`) | broad breakage | Overrides the game's **global** script-object table. `retoc to-zen` emits one whenever `scriptobjects.bin` sits at the stage root — leave it out |
| A UAT-staged `.pak` | *"Could not load section N of the global shadermap"* | It carries `Engine/Config/Base*.ini`. Ship only `.utoc`/`.ucas` plus a minimal pak |
| Engine/plugin content | spawner UI disappears | A full UAT stage pulls in ~301 `/Engine/` + plugin packages that override the game's own. Isolate with a `PrimaryAssetLabel` chunk |

**Always `retoc list` before deploying.** A clean single-package mod container looks
exactly like this and nothing more:

```
zzz_MyMod_P   c210e79fb620ebd700000001   ExportBundleData
zzz_MyMod_P   2e9221da161cec8e00000006   ContainerHeader
```

Empty material assets in the cook are the other UI-killer: auto-created
`MaterialInstanceConstant`s on game package paths override the real materials with
blanks, and they **regenerate on every cook** until deleted from the project.

---

## 7. Overriding an existing package works

Confirmed end-to-end twice — first by repointing the G28's mesh to Tavor geometry,
then by a full custom-rifle build. Edit → pack → drop in `Content\Paks\` → it
renders. No injection or runtime hook needed for the geometry itself.

The override lands because the chunk ID is derived from the package name, so ours
collides with the game's by construction:

```
GAME chunk id: c210e79fb620ebd700000001
OUR  chunk id: c210e79fb620ebd700000001
```

---

## 8. Delivery constraints

- Manual DLL injection is **not** an acceptable delivery mechanism — mods must load
  automatically at game start. (UE4SS satisfies this; it auto-loads via a `dwmapi.dll`
  proxy.)
- Plain legacy `.pak` alone does not load — it must be a Zen/IoStore container.
- Paks are unencrypted and unsigned; no AES key needed.
- Nexus Mods is excluded as a source by preference.

---

## 9. Dead ends, recorded so they aren't retried

| Attempt | Outcome |
|---|---|
| retoc PR #70 (ScriptObjects chunk) to fix new packages | Build fails every `to-zen`; and the blocker isn't ScriptObjects anyway |
| Engine-built container via UAT + chunking | New packages still 0/2 — not a retoc bug |
| Overriding the 6 "part" packages | Crash — they're materials |
| Overriding the movables skeleton | Crash — bone index mismatch |
| Empty material stubs on game paths | Spawner UI disappears |
| Deploying the staged `.pak` | Global shadermap crash (hit 3 times) |
| `-nohmd` / `-emulatestereo` / `-nullrhi` for headless testing | All exit 1; OpenXR is mandatory |
| `germodimo` | Ruled out (broken downloader) |
