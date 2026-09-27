using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.PropertyTypes.Objects;
using UAssetAPI.PropertyTypes.Structs;
using UAssetAPI.ExportTypes;

public static class Bounds
{
    static string UsmapPath => Paths.Usmap;

    static string Vec(PropertyData p)
    {
        if (p is VectorPropertyData v) return $"({v.Value.X,9:F3},{v.Value.Y,8:F3},{v.Value.Z,8:F3})";
        if (p is StructPropertyData sp && sp.Value.Count == 1) return Vec(sp.Value[0]);
        return null;
    }

    public static void Run(string path)
    {
        var mappings = new UAssetAPI.Unversioned.Usmap(UsmapPath);
        var asset = new UAsset(path, EngineVersion.VER_UE5_6, mappings);
        Console.WriteLine($"########## {Path.GetFileName(path)}   ({asset.Exports.Count} exports)");
        foreach (var exp in asset.Exports)
        {
            if (exp is not NormalExport ne) continue;
            string cls = exp.GetExportClassType()?.ToString() ?? "?";
            string loc = null, scale = null, mesh = null, rot = null;
            foreach (var p in ne.Data)
            {
                string n = p.Name.ToString();
                if (n == "RelativeLocation") loc = Vec(p);
                else if (n == "RelativeScale3D") scale = Vec(p);
                else if (n == "RelativeRotation") rot = Vec(p);
                else if (n == "StaticMesh" || n == "SkeletalMesh") mesh = p.RawValue?.ToString();
            }
            if (loc != null || mesh != null)
                Console.WriteLine($"  {exp.ObjectName,-42} {cls,-26} loc={loc ?? "-",-32} rot={rot ?? "-",-30} scale={scale ?? "-",-24} mesh={mesh}");
        }
    }
}
