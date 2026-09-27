using UAssetAPI;
using UAssetAPI.UnrealTypes;

public static class Imports
{
    static string UsmapPath => Paths.Usmap;

    public static void Run(string path, string? filter)
    {
        var mappings = new UAssetAPI.Unversioned.Usmap(UsmapPath);
        var asset = new UAsset(path, EngineVersion.VER_UE5_6, mappings);
        Console.WriteLine($"=== {Path.GetFileName(path)} : {asset.Imports.Count} imports ===");
        for (int i = 0; i < asset.Imports.Count; i++)
        {
            var imp = asset.Imports[i];
            var line = $"[{-(i + 1)}] {imp.ClassName,-28} {imp.ObjectName}";
            if (filter == null || line.Contains(filter, StringComparison.OrdinalIgnoreCase))
                Console.WriteLine("  " + line);
        }
    }
}
