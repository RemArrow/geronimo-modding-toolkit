# Geronimo — game architecture

Everything here was verified against the shipped build (Dumper-7 SDK, Kismet
bytecode, class dumps of the real packages). Nothing is inferred from folder names.

---

## 1. Weapon class hierarchy

From the live-process Dumper-7 SDK:

```
AGrippableStaticMeshActor            (VRExpansionPlugin)
└── AGunReceiver                     (native, DarkWeapons plugin)
    ├── ABoltActionRifle → GunSniperBase_C  → R700 only
    ├── ARifle          → GunRifleBase_C    → AK12, G28, HK416A5, KAC SR25/M110,
    │                                          M4A1, MCX, Tavor
    ├── AShotgun        → GunShotgunBase_C
    └── AHandgun        → GunPistolBase_C
```

Naming correction: **"GS_" = Grip Script**, from the open-source VRExpansionPlugin
(`UGS_Default : UVRGripScriptBase`) — *not* "Gun System". `GSLerpFireChamber` does
not exist; it was `GSLerp` (hand-motion smoothing) and `FireChamber` (a
`UGunChamberComponent`) mistakenly concatenated from a string scrape.

---

## 2. What makes a gun selectable

A weapon needs a `GunAsset_C` DataAsset, conventionally at
`Geronimo/Content/Weapons/Data/DA_<name>.uasset`.

`GunAsset` exposes exactly **five** properties (dumped at runtime from the live CDO):

| Property | Type |
|---|---|
| `ReceiverClass` | ClassProperty |
| `Category` | ByteProperty (`0` = Assault Rifle) |
| `DisplayName` | TextProperty |
| `bEnabled` | BoolProperty |
| `Icon` | ObjectProperty |

**There is no mesh field.** A gun's geometry comes *entirely* from the blueprint in
`ReceiverClass`. This is the single most consequential fact in the whole project —
it means you cannot give a new spawner entry its own geometry by data alone.

---

## 3. How the spawner list is built

`BP_GameInstance::GetItems()` queries the **AssetRegistry by class** (imports:
`AssetRegistryHelpers`, `GetAssetRegistry`, `GetAsset`, `MakeTopLevelAssetPath`) and
fills `TMap<EGunType, FGunAssetList> AvailableGuns`.

That map is **empty in the CDO** — it is built at runtime, so there is no static
list to patch. By contrast `Patches` (39 entries) and `Camos` (23) *are* baked CDO
arrays.

The AssetRegistry itself is a frozen `Geronimo/AssetRegistry.bin` inside
`Geronimo-Windows.pak`. Consequences, all empirically confirmed:

- The game **auto-mounts arbitrary IoStore containers** (`.utoc`/`.ucas`) placed in
  `Content\Paks\`, but **never mounts extra legacy `.pak` files**. Verified by
  file-lock test against the running process: our `.utoc` and `.ucas` are locked,
  our `.pak` is not. So config or AssetRegistry overrides cannot be delivered by pak.
- `BP_GameInstance::LoadMods()` **exists but is an empty stub** (`Return; EndOfScript`).
  The developers scaffolded mod support and never implemented the registry merge.

`GetItems()` is **additive** — calling it a second time duplicates the entire UI gun
list. Never call it manually.

---

## 4. Package layout — the part that cost the most time

Folder names lie. `/Game/Weapons/HK416a5/Mesh/` looks like modular part meshes. It
is not. Actual classes, from a class dump of the legacy-converted packages:

| Package | Real class |
|---|---|
| `HK_416_279_v2` | **StaticMesh** — the entire rifle |
| `HK_416_279_movables` | SkeletalMesh — moving parts only |
| `stock_ar15_HK_slim` | MaterialInstanceConstant |
| `pistol_grip_HK_BGB` | MaterialInstanceConstant |
| `flash_hider_USGI_001` | MaterialInstanceConstant |
| `HK_416_handguard_midwest_industries_short` | MaterialInstanceConstant |
| `HK_416_barrel_and_gas_system_001` | MaterialInstanceConstant |
| `HK_416_receiver_RAL8000` | MaterialInstanceConstant |

The whole rifle — receiver, barrel, handguard, stock, grip — is **one static mesh
with 8 material slots**. The "part"-named siblings are its *materials*, which
`HK_416_279_v2` imports by name.

Check a package's real class before touching it:

```bash
grep -aoE "StaticMesh|MaterialInstanceConstant|SkeletalMesh" <asset>.uasset | sort -u
```

---

## 5. Mesh / skeleton convention

A weapon = **static mesh** (the rifle) + **skeletal mesh** ("movables", moving parts
only). Static meshes carry **no sockets** — every attach point is a Blueprint scene
component.

Movables bones are driven **by name**. The HK416 skeleton has 13 `Root*` bones:

```
RootBolt              RootBoltCarrierGroup   RootBoltPin
RootBoltRelease       RootChargingHandle     RootChargingHandleLatch
RootDustCover         RootMagRelease         RootMagReleaseAmbi
RootPiston            RootPistonSpring       RootSelector
RootTrigger
```

Axes: **X = forward/muzzle, Z = up**, units **cm**.
Textures: BaseColor + Normal (DirectX) + packed ORM.

See [model-spec.md](model-spec.md) for the full
modeler-facing spec.

---

## 6. Blueprint attach anchors (HK416A5 279mm)

Dumped from `Firearm_Rifle_HK416A5_279mm.uasset`. These are **actor-local
positions in cm** and are the ground truth for aligning any replacement mesh:

| Component | X | Y | Z |
|---|---|---|---|
| Fire Location (muzzle) | 35.899 | 0.000 | 2.929 |
| SecondaryHandSocket | 25.243 | 0.000 | 2.917 |
| MagwellHandSocket | 7.934 | 0.000 | −1.024 |
| Round Chamber Location | 5.150 | 0.000 | −0.080 |
| Magazine Well Component | 4.827 | 0.000 | −5.440 |
| FireChamber | 4.366 | 0.000 | 3.005 |
| Bolt Release Interaction | 1.214 | −1.410 | 2.357 |
| Chambering Round Proxy | −2.003 | 0.000 | −0.764 |
| Round Eject Location | −2.499 | 0.000 | −0.080 |
| PrimaryHandSocket | −7.279 | 0.000 | −5.602 |
| ChargingHandle | −8.964 | 0.000 | 5.490 |
| Recoil Pivot | −10.271 | 0.000 | 3.020 |
| DefaultStockButtPosition | −39.455 | 0.000 | −4.629 |

Mesh bounds of `HK_416_279_v2` (origin `(2.4088, 0.9917, −2.8641)`, extent
`(38.3290, 3.4044, 9.4110)`):

```
X [-35.920 ..  40.738]      length 76.66 cm
Y [ -2.413 ..   4.396]      width   6.81 cm
Z [-12.275 ..   6.547]      height 18.82 cm
```

The muzzle anchor at X 35.9 sitting inside that range is what proves the mesh is the
whole rifle, not just a receiver.
