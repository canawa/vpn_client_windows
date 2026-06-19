using System.IO;

namespace CoffeeManiaVPN.Helpers;

public static class AppPaths
{
    private static string? _root;

    public static string Root => _root ??= ResolveRoot();

    public static string XrayDirectory => Path.Combine(Root, "xray");

    public static string AssetsDirectory => Path.Combine(Root, "Assets");

    private static string ResolveRoot()
    {
        foreach (var candidate in GetCandidates())
        {
            if (File.Exists(Path.Combine(candidate, "xray", "xray.exe")))
                return candidate;
        }

        return AppContext.BaseDirectory;
    }

    private static IEnumerable<string> GetCandidates()
    {
        yield return AppContext.BaseDirectory;

        if (Environment.ProcessPath is not { } processPath)
            yield break;

        var exeDir = Path.GetDirectoryName(processPath)!;
        yield return exeDir;

        if (!Directory.Exists(exeDir))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories(exeDir, "*.extracted", SearchOption.TopDirectoryOnly))
            yield return dir;

        var namedExtracted = Path.Combine(exeDir, Path.GetFileNameWithoutExtension(processPath) + ".extracted");
        if (Directory.Exists(namedExtracted))
            yield return namedExtracted;
    }
}
