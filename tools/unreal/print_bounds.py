"""
Print StaticMesh bounds for the given asset paths.

Use it to confirm an imported mesh landed where you intended -- in particular to catch
Unreal's FBX import NEGATING Y. Blender Y[-2.311..4.380] arrives here as Y[-4.380..2.311].
X and Z pass through unchanged.

Run:
  UnrealEditor.exe <project>.uproject -ExecutePythonScript="print_bounds.py" \
      -nosplash -stdout -unattended

Then grep the output for "RESULT".
"""
import unreal

ASSETS = [
    "/Game/Weapons/YourMod/Mesh/YourMesh",
]

eal = unreal.EditorAssetLibrary
for path in ASSETS:
    if not eal.does_asset_exist(path):
        unreal.log_warning("RESULT %s -> MISSING" % path)
        continue
    mesh = eal.load_asset(path)
    try:
        b = mesh.get_bounds()
        o, e = b.origin, b.box_extent
        unreal.log_warning(
            "RESULT %-50s origin=(%.4f, %.4f, %.4f) extent=(%.4f, %.4f, %.4f)"
            % (path, o.x, o.y, o.z, e.x, e.y, e.z))
        unreal.log_warning(
            "RESULT %-50s X[%.3f..%.3f] Y[%.3f..%.3f] Z[%.3f..%.3f]"
            % ("  range", o.x - e.x, o.x + e.x, o.y - e.y, o.y + e.y, o.z - e.z, o.z + e.z))
    except Exception as ex:
        unreal.log_warning("RESULT %s -> %s" % (path, ex))

unreal.SystemLibrary.quit_editor()
