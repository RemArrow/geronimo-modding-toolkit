# Geronimo Modding Toolkit

Reverse-engineering notes and tooling for modding **Geronimo** (Dark Matter Studios) —
a VR tactical shooter on Unreal Engine **5.7.4** (CL 51494982). There is **no official
modkit**; everything here was derived from the shipped build.

This repo contains **no game assets and no game code** — only findings, and tools that
operate on a copy of the game you own.

---

## Start here

If you are about to try something, read [**findings.md**](docs/findings.md) first. It
documents what is possible in this build and — more usefully — what is *proven
impossible*, along with the approaches that crash the game. It will save you several
evenings.

The three-sentence version:

1. **New package IDs never register.** Custom content must override an *existing*
   package. This was proven with a controlled in-game probe, not assumed.
2. **`GunAsset` has no mesh field.** A gun's geometry comes entirely from its
   `ReceiverClass` blueprint, so a new spawner entry cannot have unique geometry.
3. **Folder names lie.** `/Game/Weapons/<gun>/Mesh/` is mostly `MaterialInstanceConstant`s.
   Overwriting them with meshes crashes the game on load.

---

## Documentation

| Doc | What's in it |
|---|---|
| [findings.md](docs/findings.md) | Proven facts and proven dead ends. **Read first.** |
| [architecture.md](docs/architecture.md) | Weapon class hierarchy, how the spawner list is built, real package classes, blueprint attach anchors |
| [toolchain.md](docs/toolchain.md) | Every tool, the versions that work, and the ones that look right and aren't |
| [pipeline.md](docs/pipeline.md) | Blender → UE → cook → container → deploy, with a verification gate at each stage |
| [troubleshooting.md](docs/troubleshooting.md) | Crash signature → root cause → fix |
| [scripts.md](docs/scripts.md) | What each tool does and how to configure it |
| [model-spec.md](docs/model-spec.md) | Modeler-facing spec: mesh split, required bones, axes, units |

---

## Tools

| Path | Language | Purpose |
|---|---|---|
| [`tools/UAssetParser/`](tools/UAssetParser) | C# | Read legacy `.uasset` — bounds, imports, Kismet bytecode |
| [`tools/ue4ss/AddGunEntry/`](tools/ue4ss/AddGunEntry) | Lua | Add a spawner entry at runtime; probe whether packages load |
| [`tools/blender/`](tools/blender) | Python | Measure part bounds, assemble and align a weapon mesh |
| [`tools/unreal/`](tools/unreal) | Python | Print bounds, verify skeletons, reset stale overrides |

Each has a config block at the top. None is turnkey — fill in your own paths.

---

## Third-party tools you'll need

| Tool | For | Note |
|---|---|---|
| [Dumper-7](https://github.com/Encryqed/Dumper-7) | SDK + `.usmap` | Use this, **not** UnrealMappingsDumper — that one crashes the game on 5.7 (upstream issue #75) |
| [retoc](https://github.com/trumank/retoc) | Zen ↔ Legacy containers | Build from source; the v0.1.5 + PR #70 build fails every `to-zen` |
| [repak](https://github.com/trumank/repak) | legacy `.pak` | |
| [UAssetAPI](https://github.com/atenfyr/UAssetAPI) | `.uasset` parsing | Use `VER_UE5_6`, never `VER_UE5_7` |
| [UE4SS](https://github.com/UE4SS-RE/RE-UE4SS) | runtime Lua hooks | Auto-loads on 5.7 with no manual offsets |
| Unreal Engine **5.7** | cooking | Cooked assets are not version-portable |
| Blender | mesh work | |

CUE4Parse and FModel **cannot open this game's containers** — they fail on
`FIoContainerHeaderSoftPackageReferences` identically on the game's own container, so
it's a 5.7 support gap rather than anything you did. Use `retoc` for container work.

---

## The shortest useful summary of the architecture

```
AGrippableStaticMeshActor            (VRExpansionPlugin)
└── AGunReceiver                     (native, DarkWeapons plugin)
    ├── ABoltActionRifle → GunSniperBase_C
    ├── ARifle          → GunRifleBase_C
    ├── AShotgun        → GunShotgunBase_C
    └── AHandgun        → GunPistolBase_C
```

`BP_GameInstance::GetItems()` builds the gun list at runtime by querying the
**AssetRegistry by class**. The registry is frozen inside the shipped pak, and
`BP_GameInstance::LoadMods()` **exists but is an empty stub** — the developers
scaffolded mod support and never implemented the registry merge.

That is why adding a gun requires a runtime hook, and why the working approach is to
construct a `GunAsset` with `StaticConstructObject` and append it to `AvailableGuns` —
a runtime `UObject` needs no package.

---

## Scope and legality

For people who **own the game**, modding their own installation.

- No game assets, code, or extracted content are redistributed here.
- Nothing bypasses DRM, licensing or anti-cheat. Geronimo's paks are unencrypted and
  unsigned; no key is involved.
- Findings are structural observations (class names, package layouts, coordinates)
  needed to author compatible content.

If you build on this, keep your own art licensed appropriately — a purchased asset's
terms usually do **not** permit redistribution inside a mod.

Not affiliated with or endorsed by Dark Matter Studios.

---

## License

Tools and documentation: [MIT](LICENSE).
