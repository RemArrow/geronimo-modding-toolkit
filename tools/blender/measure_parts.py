"""
Print per-part and combined bounds for a folder of weapon part FBXs.

Run this BEFORE assemble_rifle.py: the combined bounds are what you compare against the
target package's bounds to derive the alignment offset.

Run:  blender.exe --background --python measure_parts.py

Method: compare min-edge and max-edge deltas separately on each axis. If they agree to
within a few millimetres, the two models share an envelope and a pure translation is the
right correction. If they disagree badly, the models differ in size and you have the
wrong part set -- do not reach for scaling, check what the target package actually
contains first.
"""
import os

import bpy
from mathutils import Vector

# ------------------------------------------------------------------ config
EXPORT_DIR = r"E:\path\to\your\part_fbx_folder"

# Parts that make up the rifle proper. Anything not listed is still measured and
# printed, just excluded from the COMBINED figure.
RIFLE = ["Body", "Handguard", "Stock", "Grip", "Muzzle", "Feer"]
# --------------------------------------------------------------------------

bpy.ops.wm.read_factory_settings(use_empty=True)

parts = {}
for fname in sorted(os.listdir(EXPORT_DIR)):
    if not fname.lower().endswith(".fbx"):
        continue
    name = os.path.splitext(fname)[0]
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(EXPORT_DIR, fname))
    objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]

    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for corner in o.bound_box:
            w = o.matrix_world @ Vector(corner)
            lo = Vector((min(lo[i], w[i]) for i in range(3)))
            hi = Vector((max(hi[i], w[i]) for i in range(3)))
    parts[name] = (lo, hi)
    print("PART %-20s X[%8.3f..%8.3f] Y[%8.3f..%8.3f] Z[%8.3f..%8.3f]"
          % (name, lo.x, hi.x, lo.y, hi.y, lo.z, hi.z))

lo = Vector((1e9, 1e9, 1e9))
hi = Vector((-1e9, -1e9, -1e9))
for name in RIFLE:
    if name not in parts:
        continue
    l, h = parts[name]
    lo = Vector((min(lo[i], l[i]) for i in range(3)))
    hi = Vector((max(hi[i], h[i]) for i in range(3)))

print("COMBINED   X[%8.3f..%8.3f] Y[%8.3f..%8.3f] Z[%8.3f..%8.3f]"
      % (lo.x, hi.x, lo.y, hi.y, lo.z, hi.z))
print("COMBINED cm X[%8.3f..%8.3f] Y[%8.3f..%8.3f] Z[%8.3f..%8.3f]"
      % (lo.x * 100, hi.x * 100, lo.y * 100, hi.y * 100, lo.z * 100, hi.z * 100))
