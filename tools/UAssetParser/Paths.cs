/// <summary>
/// Machine-specific paths, overridable by environment variable so nothing is hardcoded
/// to one install.
///
///   GERONIMO_USMAP     full path to the .usmap produced by Dumper-7
///   GERONIMO_PAKS      the game's Content\Paks directory
///   GERONIMO_OUT       where the default provider mode writes JSON dumps
/// </summary>
public static class Paths
{
    public static string Usmap =>
        Environment.GetEnvironmentVariable("GERONIMO_USMAP")
        ?? throw new InvalidOperationException(
            "Set GERONIMO_USMAP to your Dumper-7 .usmap path, e.g.\n" +
            @"  set GERONIMO_USMAP=C:\Dumper-7\<build>\Mappings\<build>.usmap");

    public static string Paks =>
        Environment.GetEnvironmentVariable("GERONIMO_PAKS")
        ?? @"C:\Program Files (x86)\Steam\steamapps\common\GERONIMO\Geronimo\Content\Paks";

    public static string Out =>
        Environment.GetEnvironmentVariable("GERONIMO_OUT")
        ?? Path.Combine(AppContext.BaseDirectory, "Dumps");
}
