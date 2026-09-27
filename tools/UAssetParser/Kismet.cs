using UAssetAPI;
using UAssetAPI.UnrealTypes;
using UAssetAPI.ExportTypes;

// Disassemble Blueprint bytecode + dump default property values, to find how the gun list is
// actually populated (AvailableGuns / GetItems / LoadMods on BP_GameInstance).
public static class Kismet
{
    static string UsmapPath => Paths.Usmap;

    public static void Run(string path, string? funcFilter)
    {
        var mappings = new UAssetAPI.Unversioned.Usmap(UsmapPath);
        var asset = new UAsset(path, EngineVersion.VER_UE5_6, mappings);

        Console.WriteLine($"=== {Path.GetFileName(path)} : {asset.Exports.Count} exports ===\n");

        foreach (var exp in asset.Exports)
        {
            string name = exp.ObjectName.ToString();
            string cls = exp.GetExportClassType().ToString();

            if (funcFilter != null && !name.Contains(funcFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            Console.WriteLine($"--- export: {name} ({cls}) ---");

            // Dump default property values (this is where a baked AvailableGuns list would live).
            if (exp is NormalExport ne && ne.Data.Count > 0)
            {
                foreach (var prop in ne.Data)
                    Console.WriteLine($"    prop {prop.Name} [{prop.PropertyType}] = {Summarize(prop)}");
            }

            // Dump Kismet bytecode for functions.
            if (exp is StructExport se && se.ScriptBytecode != null)
            {
                Console.WriteLine($"    bytecode: {se.ScriptBytecode.Length} instructions");
                foreach (var inst in se.ScriptBytecode)
                    Console.WriteLine($"      {inst.Inst}  {Describe(inst)}");
            }
            else if (exp is StructExport se2 && se2.ScriptBytecode == null)
            {
                Console.WriteLine($"    (bytecode present but not parsed; raw size {se2.ScriptBytecodeSize})");
            }
            Console.WriteLine();
        }
    }

    static string Summarize(UAssetAPI.PropertyTypes.Objects.PropertyData prop)
    {
        try
        {
            switch (prop)
            {
                case UAssetAPI.PropertyTypes.Objects.ArrayPropertyData arr:
                    return $"Array[{arr.Value?.Length ?? 0}] {{ " +
                           string.Join(", ", (arr.Value ?? []).Take(12).Select(SafeStr)) + " }";
                case UAssetAPI.PropertyTypes.Objects.MapPropertyData map:
                    var pairs = map.Value?.Select(kv => $"{SafeStr(kv.Key)} => {SafeStr(kv.Value)}") ?? [];
                    return $"Map[{map.Value?.Count ?? 0}] {{ " + string.Join("; ", pairs.Take(12)) + " }";
                default:
                    var s = prop.ToString() ?? "";
                    return s.Length > 300 ? s[..300] + "..." : s;
            }
        }
        catch (Exception ex) { return $"<unprintable: {ex.GetType().Name}>"; }
    }

    static string SafeStr(object? o)
    {
        try
        {
            if (o is UAssetAPI.PropertyTypes.Objects.ObjectPropertyData op) return op.Value.ToString();
            if (o is UAssetAPI.PropertyTypes.Structs.StructPropertyData sp)
                return "{" + string.Join(",", sp.Value.Select(v => $"{v.Name}={SafeStr(v)}")) + "}";
            var s = o?.ToString() ?? "null";
            return s.Length > 120 ? s[..120] + "..." : s;
        }
        catch { return "<err>"; }
    }

    // Pull out the interesting bits of an expression (call targets, property/variable names)
    // rather than the useless default ToString().
    static string Describe(UAssetAPI.Kismet.Bytecode.KismetExpression e)
    {
        var parts = new List<string>();
        var t = e.GetType();
        foreach (var f in t.GetFields())
        {
            if (f.Name is "StackNode" or "Function" or "FunctionName" or "Variable" or "Property"
                or "ObjectPointer" or "Value" or "PropertyName" or "ContextExpression")
            {
                object? v = null;
                try { v = f.GetValue(e); } catch { }
                if (v == null) continue;
                var s = SafeStr(v);
                if (!string.IsNullOrWhiteSpace(s) && s != "null") parts.Add($"{f.Name}={s}");
            }
        }
        foreach (var p in t.GetProperties())
        {
            if (p.Name is "StackNode" or "Function" or "FunctionName" or "Variable" or "Property" or "PropertyName")
            {
                object? v = null;
                try { v = p.GetValue(e); } catch { }
                if (v == null) continue;
                var s = SafeStr(v);
                if (!string.IsNullOrWhiteSpace(s) && s != "null") parts.Add($"{p.Name}={s}");
            }
        }
        return string.Join(" ", parts);
    }
}
