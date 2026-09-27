using CUE4Parse.FileProvider;
using CUE4Parse.UE4.Versions;
using Newtonsoft.Json;
using Serilog;

if (args.Length > 1 && args[0] == "probe") { ProbeName.Run(args[1]); return; }
if (args.Length > 1 && args[0] == "kismet") { Kismet.Run(args[1], args.Length > 2 ? args[2] : null); return; }
if (args.Length > 1 && args[0] == "bounds") { Bounds.Run(args[1]); return; }
if (args.Length > 1 && args[0] == "imports") { Imports.Run(args[1], args.Length > 2 ? args[2] : null); return; }

Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Console().CreateLogger();

var gameDir = Paths.Paks;
var outDir = Paths.Out;
Directory.CreateDirectory(outDir);

Console.WriteLine($"Scanning: {gameDir}");
Console.WriteLine($"Directory.Exists: {Directory.Exists(gameDir)}");
foreach (var f in Directory.EnumerateFiles(gameDir))
    Console.WriteLine($"  raw file: {f}");

var usmapPath = Paths.Usmap;
var provider = new DefaultFileProvider(gameDir, SearchOption.AllDirectories, new VersionContainer(EGame.GAME_UE5_LATEST));
provider.MappingsContainer = new CUE4Parse.MappingsProvider.Usmap.FileUsmapTypeMappingsProvider(usmapPath);
Console.WriteLine($"After construction: Files={provider.Files.Count} MountedVfs={provider.MountedVfs.Count} UnloadedVfs={provider.UnloadedVfs.Count}");
provider.Initialize();
Console.WriteLine($"After Initialize: Files={provider.Files.Count} MountedVfs={provider.MountedVfs.Count} UnloadedVfs={provider.UnloadedVfs.Count}");

Console.WriteLine($"Found {provider.Files.Count} files across {provider.MountedVfs.Count} archive(s). Mounting...");
try
{
    var mountedCount = provider.Mount();
    Console.WriteLine($"Mount() returned: {mountedCount}");
}
catch (Exception ex)
{
    Console.WriteLine($"Mount FAILED: {ex.GetType().Name}: {ex.Message}");
    Console.WriteLine(ex.ToString());
    return;
}
Console.WriteLine($"Mounted OK. {provider.Files.Count} files visible after mount.");

// Enumerate every weapon Blueprint in the game so we can pick a semi-auto donor for the Barrett.
var weaponFiles = provider.Files.Keys
    .Where(k => (k.Contains("/Weapons/", StringComparison.OrdinalIgnoreCase) || k.Contains("DarkWeapons/Content/Blueprints", StringComparison.OrdinalIgnoreCase))
                && k.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase))
    .OrderBy(k => k)
    .ToList();
Console.WriteLine($"\nWeapon-related .uasset files: {weaponFiles.Count}");
foreach (var f in weaponFiles) Console.WriteLine($"  {f}");

string[] targets = new[]
{
    "Geronimo/Plugins/DarkWeapons/Content/Blueprints/GunRifleBase",
    "Geronimo/Content/Weapons/KAC_SR25/Blueprints/Firearm_Rifle_KAC_M110",
    "Geronimo/Content/Weapons/G28/Blueprint/Firearm_Rifle_G28",
};

foreach (var t in targets)
{
    Console.WriteLine($"\n=== {t} ===");
    try
    {
        var pkg = provider.LoadPackage(t);
        Console.WriteLine($"Package type: {pkg.GetType().FullName}");

        // Structural dump (class hierarchy / outer / super) via reflection - works even
        // without a .usmap, since it doesn't require full property deserialization.
        var exportMapProp = pkg.GetType().GetProperty("ExportMap");
        if (exportMapProp?.GetValue(pkg) is System.Collections.IEnumerable exportMap)
        {
            Console.WriteLine("-- ExportMap (structural, no property values) --");
            foreach (var exp in exportMap)
            {
                var et = exp.GetType();
                string Get(string name) => et.GetProperty(name)?.GetValue(exp)?.ToString() ?? "?";
                Console.WriteLine($"  ObjectName={Get("ObjectName")} ClassIndex={Get("ClassIndex")} SuperIndex={Get("SuperIndex")} OuterIndex={Get("OuterIndex")} TemplateIndex={Get("TemplateIndex")}");
            }
        }
        else
        {
            Console.WriteLine("(no ExportMap property found via reflection)");
        }

        // Full property deserialization (needs .usmap for unversioned packages).
        try
        {
            var exports = pkg.GetExports().ToList();
            Console.WriteLine($"Loaded {exports.Count} fully-deserialized export(s).");
            var json = JsonConvert.SerializeObject(exports, Formatting.Indented);
            var outPath = Path.Combine(outDir, t.Replace('/', '_') + ".json");
            File.WriteAllText(outPath, json);
            Console.WriteLine($"Dumped -> {outPath} ({new FileInfo(outPath).Length} bytes)");
        }
        catch (Exception ex2)
        {
            Console.WriteLine($"Full property deserialization FAILED: {ex2.GetType().Name}: {ex2.Message}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAILED: {ex.GetType().Name}: {ex.Message}");
    }
}
