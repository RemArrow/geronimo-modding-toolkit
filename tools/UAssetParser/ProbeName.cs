using System.Reflection;
using UAssetAPI;
using UAssetAPI.UnrealTypes;

// Locate whichever property actually carries the package's declared name, since retoc reads
// that (not the name map, not the file path) when emitting the Zen package store entry.
public static class ProbeName
{
    static string UsmapPath => Paths.Usmap;

    public static void Run(string path)
    {
        var mappings = new UAssetAPI.Unversioned.Usmap(UsmapPath);
        var asset = new UAsset(path, EngineVersion.VER_UE5_6, mappings);

        Console.WriteLine($"=== {Path.GetFileName(path)} ===");
        foreach (var prop in asset.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                || prop.Name.Contains("Folder", StringComparison.OrdinalIgnoreCase)
                || prop.Name.Contains("Name", StringComparison.OrdinalIgnoreCase))
            {
                object? val = null;
                try { val = prop.GetValue(asset); } catch { }
                if (val is System.Collections.IEnumerable && val is not string) continue;
                Console.WriteLine($"  {prop.PropertyType.Name,-20} {prop.Name,-28} = {val}");
            }
        }
        foreach (var f in asset.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (f.Name.Contains("Package", StringComparison.OrdinalIgnoreCase)
                || f.Name.Contains("Folder", StringComparison.OrdinalIgnoreCase))
            {
                object? val = null;
                try { val = f.GetValue(asset); } catch { }
                if (val is System.Collections.IEnumerable && val is not string) continue;
                Console.WriteLine($"  [field] {f.FieldType.Name,-14} {f.Name,-28} = {val}");
            }
        }
    }
}
