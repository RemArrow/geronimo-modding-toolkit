# Geronimo — Weapon Model Spec (Assault Rifle)

Everything here was extracted from the shipped game (HK416A5 and M4A1 URGI), not guessed.
If the model follows this, it drops into the game's existing rifle logic with no rework.

Engine: **Unreal Engine 5.7**. Units: **centimetres** (UE default). Axes: **X = forward (muzzle),
Z = up**. Bolt travel and dust cover run along X; trigger and selector rotate about Z.

---

## 1. Mesh split — this is the important bit

A Geronimo weapon is **not one mesh**. It is split:

| Piece | Type | Example (HK416A5) |
|---|---|---|
| Receiver / main body | **Static Mesh** | `HK_416_279_v2` |
| All moving parts | **Skeletal Mesh** ("movables") | `HK_416_279_movables` |
| Barrel + gas system | Static Mesh | `HK_416_barrel_and_gas_system_001` |
| Handguard | Static Mesh | `HK_416_handguard_quad_rail` |
| Stock | Static Mesh | `stock_ar15_HK_slim` |
| Pistol grip | Static Mesh | `pistol_grip_HK_BGB` |
| Iron sights | Static Mesh | `HK_Rearsight`, `iron_sight_kriss_defiance_001` |
| Muzzle device | Static Mesh | `flash_hider_USGI_001` |
| Magazine | Static Mesh | (own asset) |
| Round / cartridge | Static Mesh | `ammo_556_m855a1` |

The separate parts exist because the game has a **modular attachment system** (handguards, stocks,
grips and muzzle devices are swappable in-game).

> **Simplest first version:** model the receiver as one static mesh, the moving parts as one
> skeletal mesh, and bake barrel/handguard/stock/grip into the receiver. Modularity can come later.
> The moving-parts skeletal mesh is the part that cannot be skipped.

---

## 2. Required bones on the "movables" skeletal mesh

The game drives these **by name**. Names must match exactly (case-sensitive).

**Required** — referenced directly by the rifle code:

| Bone | Purpose | Motion | Axis |
|---|---|---|---|
| `RootBoltCarrierGroup` | Bolt carrier | translation | **X**, travel ≈ **-9.75 cm** |
| `RootChargingHandle` | Charging handle | translation | X (reciprocates with bolt) |
| `RootBoltRelease` | Bolt catch / release | rotation | **X** |
| `RootDustCover` | Ejection port cover | rotation | **X** |
| `RootTrigger` | Trigger | rotation | **Z** (max ≈ **7°**) |
| `RootMagRelease` | Magazine release | translation | **Y** (≈ **-0.3 cm**) |
| `RootSelector` | Fire selector | rotation | **Z** |

**Optional** — present on real guns, safe to include, ignored if absent:

`RootBolt`, `RootBoltPin`, `RootChargingHandleLatch`, `RootChargingHandleL` / `RootChargingHandleR`
(ambidextrous), `RootMagReleaseAmbi`, `RootHammer`, `RootSear`, `RootDisconnector`,
`RootPiston`, `RootPistonSpring`

Reference skeletons:
- HK416A5: `RootBolt, RootBoltCarrierGroup, RootBoltPin, RootBoltRelease, RootChargingHandle,
  RootChargingHandleLatch, RootDustCover, RootMagRelease, RootMagReleaseAmbi, RootPiston,
  RootPistonSpring, RootSelector, RootTrigger`
- M4A1 URGI: `RootBolt, RootBoltCarrierGroup, RootBoltRelease, RootChargingHandle,
  RootChargingHandleL, RootChargingHandleLatch, RootChargingHandleR, RootDisconnector,
  RootDustCover, RootHammer, RootMagRelease, RootSear, RootSelector, RootTrigger`

Each moving part should be its own bone, with geometry skinned rigidly (weight 1.0) to that bone.
No blending needed — these are mechanical parts, not organic deformation.

---

## 3. No mesh sockets needed

Checked: the shipped static meshes carry **no sockets**. All attachment points (muzzle/fire
location, recoil pivot, chamber, ejection port, magazine well, hand grips, rail spline points)
are **Blueprint scene components** positioned on our side, not authored into the mesh.

So the modeler does **not** need to add sockets. Just make sure the geometry is positioned
sensibly around the origin so those points can be placed.

---

## 4. Origin / orientation

- Muzzle points along **+X**.
- **+Z** is up.
- Keep the origin near the receiver/grip area (roughly where the weapon is held), consistent
  between the static body mesh and the movables skeletal mesh — they are assembled at the same
  transform in-game, so their origins must agree.

---

## 5. Materials & textures

Existing guns use a straightforward PBR set per part:
`BaseColor`, `Normal` (DirectX), and `OcclusionRoughnessMetallic` (ORM, packed).

Example from the game: `V6_T_USP45_Slide_BaseColor`, `..._OcclusionRoughnessMetallic`,
`Slide_Normal_DirectX`.

Deliver textures as standard PNG/TGA; ORM packed as R=Occlusion, G=Roughness, B=Metallic.

---

## 6. Deliverable checklist

- [ ] Receiver / body — static mesh (FBX)
- [ ] Movables — skeletal mesh (FBX) with the bones in §2, rigid-skinned
- [ ] Magazine — static mesh (FBX)
- [ ] (Optional) barrel, handguard, stock, grip, sights, muzzle device as separate static meshes
- [ ] Textures: BaseColor + Normal (DirectX) + ORM per material
- [ ] Modelled in cm, muzzle +X, up +Z, consistent origin across body and movables
