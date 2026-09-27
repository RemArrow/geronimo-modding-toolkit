"""
Assemble a multi-part weapon model into ONE static mesh and align it to the package
it will override.

Geronimo stores a whole rifle -- receiver, barrel, handguard, stock, grip -- as a single
StaticMesh with several material slots (see docs/architecture.md). So a replacement must
also be one mesh covering the same extent; shipping only a receiver leaves it floating
among attachment points positioned for a full-length gun.

Run:  blender.exe --background --python assemble_rifle.py

Two things that will bite you:
  * Transforms must be baked into MESH DATA. FBX objects have parents, which makes
    matrix_world assignment get reinterpreted.
  * Unreal's FBX import NEGATES Y. X and Z pass through unchanged. Apply any Y
    correction with the sign flipped here, then verify with tools/unreal/print_bounds.py.
"""
import os

import bpy
from mathutils import Vector

# ------------------------------------------------------------------ config
EXPORT_DIR = r"E:\path\to\your\part_fbx_folder"
OUT_FBX = os.path.join(EXPORT_DIR, "Assembled_Rifle.fbx")

# Parts to join, by FBX basename. Exclude the "movables" skeletal mesh (overriding a
# skeleton crashes the game) and the magazine (it is a separate attachment).
PARTS = ["Body", "Handguard", "Stock", "Grip", "Muzzle", "Feer"]

# Alignment offset in METRES, baked into mesh data. Derive it by comparing this
# model's combined bounds against the target package's bounds -- see
# docs/pipeline.md. Remember Y is negated on import into Unreal.
DELTA = Vector((0.0, 0.0, 0.0))

# Bounds of the package being overridden, in cm, for the printed comparison.
# Get these with tools/UAssetParser bounds mode. Example below is HK_416_279_v2.
TARGET_CM = "X[ -35.920..  40.738] Y[  -2.413..   4.396] Z[ -12.275..   6.547]"
# --------------------------------------------------------------------------


def world_bounds(objs):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for corner in o.bound_box:
            w = o.matrix_world @ Vector(corner)
            lo = Vector((min(lo[i], w[i]) for i in range(3)))
            hi = Vector((max(hi[i], w[i]) for i in range(3)))
    return lo, hi


bpy.ops.wm.read_factory_settings(use_empty=True)

meshes = []
for name in PARTS:
    path = os.path.join(EXPORT_DIR, name + ".fbx")
    if not os.path.exists(path):
        print("MISSING %s" % path)
        continue
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    meshes += [o for o in set(bpy.data.objects) - before if o.type == "MESH"]

if not meshes:
    raise SystemExit("no meshes imported - check EXPORT_DIR and PARTS")

# Bake the alignment into mesh data, once per unique datablock.
done = set()
for o in meshes:
    if o.data.name in done:
        continue
    m = o.matrix_world.copy()
    m.translation += DELTA
    o.data.transform(m)
    done.add(o.data.name)
    o.matrix_world.identity()

for o in list(bpy.data.objects):
    if o.type != "MESH":
        bpy.data.objects.remove(o, do_unlink=True)

bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.join()

joined = bpy.context.view_layer.objects.active
joined.name = "Assembled_Rifle"

lo, hi = world_bounds([joined])
print("FINAL cm  X[%8.3f..%8.3f] Y[%8.3f..%8.3f] Z[%8.3f..%8.3f]"
      % (lo.x * 100, hi.x * 100, lo.y * 100, hi.y * 100, lo.z * 100, hi.z * 100))
print("TARGET cm " + TARGET_CM)

bpy.ops.object.select_all(action="DESELECT")
joined.select_set(True)
bpy.ops.export_scene.fbx(filepath=OUT_FBX, use_selection=True, apply_unit_scale=True,
                         add_leaf_bones=False, bake_space_transform=False)
print("WROTE " + OUT_FBX)
