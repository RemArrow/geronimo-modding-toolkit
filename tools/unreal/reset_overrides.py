"""
Return the cooking project to a clean state: delete every override folder, clear the
ObjectRedirectors a rename leaves behind, and list what survives.

Run this whenever an experiment has left assets sitting on game package paths. Stale
overrides are easy to forget and they silently ship on the next cook -- an empty
material on a game path is enough to make the spawner UI vanish.
"""
import unreal

# Folders that mirror game package paths, i.e. anything that overrides shipped content.
OVERRIDE_DIRS = [
    "/Game/Weapons/HK416a5",
    "/Game/Weapons/G28",
]

eal = unreal.EditorAssetLibrary

for d in OVERRIDE_DIRS:
    if eal.does_directory_exist(d):
        eal.delete_directory(d)
        unreal.log_warning("RESULT deleted %s" % d)

eal.save_directory("/Game", False, True)

for p in eal.list_assets("/Game", True, False):
    data = eal.find_asset_data(p)
    if data.asset_class_path.asset_name == "ObjectRedirector":
        eal.delete_asset(p)
        unreal.log_warning("RESULT killed redirector %s" % p)

unreal.log_warning("RESULT === remaining ===")
for p in sorted(eal.list_assets("/Game", True, False)):
    unreal.log_warning("RESULT keep %s" % p)

unreal.SystemLibrary.quit_editor()
