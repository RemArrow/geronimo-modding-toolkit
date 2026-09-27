"""
Print a SkeletalMesh's skeleton and bone list.

Why you want this: cooked animation data stores BONE INDICES, not names. Overriding a
game skeleton with one that has the same bone names but a different count or order
crashes the game with EXCEPTION_ACCESS_VIOLATION at load.

Geronimo's HK416 skeleton has 13 Root* bones:
  RootBolt, RootBoltCarrierGroup, RootBoltPin, RootBoltRelease, RootChargingHandle,
  RootChargingHandleLatch, RootDustCover, RootMagRelease, RootMagReleaseAmbi,
  RootPiston, RootPistonSpring, RootSelector, RootTrigger

If your rig does not reproduce that list exactly, in order, do not ship a skeleton.
Override static meshes only and leave *_movables stock.
"""
import unreal

MESH = "/Game/Weapons/YourMod/Mesh/YourMesh_movables"

eal = unreal.EditorAssetLibrary
if not eal.does_asset_exist(MESH):
    unreal.log_warning("RESULT MISSING %s" % MESH)
else:
    a = eal.load_asset(MESH)
    unreal.log_warning("RESULT class = %s" % a.get_class().get_name())
    sk = a.get_editor_property("skeleton")
    unreal.log_warning("RESULT skeleton = %s" % (sk.get_path_name() if sk else "None"))
    if sk:
        try:
            tree = sk.get_editor_property("bone_tree")
            unreal.log_warning("RESULT bone count = %d" % len(tree))
        except Exception as ex:
            unreal.log_warning("RESULT bone_tree -> %s" % ex)

unreal.SystemLibrary.quit_editor()
