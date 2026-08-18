using System.Text.Json;

namespace Canary.Core.Hooks;

// Owns .hooks.json end-to-end: loading, and auto-creating an empty one for
// every content directory that has markdown directly inside it. Deliberately
// its own module, not folded into Canary.Core.Manifest.ManifestBuilder
// (which owns the analogous .nav.json) -- nav visibility and hook
// application are orthogonal concerns, same reasoning already applied to
// keep SitemapBuilder independent of the nav tree. See PLAN.md's "Content
// hooks" section.
//
// Auto-creation goes deeper than .nav.json's does: ManifestBuilder.BuildNav
// only ever walks one level into content/, since nav's data model is flat
// categories with children. Hooks apply non-recursively with no
// inheritance, so a directory's hook list only ever covers .md files
// directly inside it, at any depth -- auto-creation has to reach every such
// directory, not just the top level.
public static class HooksOverrideFile
{
    private const string FileName = ".hooks.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    // directories: every content directory that has at least one .md file
    // directly inside it, at any depth. Callers derive this from the same
    // route list ContentScanner already computed, rather than this class
    // re-walking the filesystem with its own, potentially-diverging
    // definition of "a content directory."
    public static void EnsureFilesExist(IEnumerable<string> directories)
    {
        foreach (var dir in directories.Distinct())
        {
            var path = Path.Combine(dir, FileName);
            if (File.Exists(path)) continue;

            var json = JsonSerializer.Serialize(new HooksOverride(), SerializerOptions);
            File.WriteAllText(path, json + "\n");
        }
    }

    public static IReadOnlyList<string> ResolveForDirectory(string dir)
    {
        var path = Path.Combine(dir, FileName);
        if (!File.Exists(path)) return Array.Empty<string>();

        var raw = File.ReadAllText(path);
        var config = JsonSerializer.Deserialize<HooksOverride>(raw, SerializerOptions);
        return config?.Hooks ?? (IReadOnlyList<string>)Array.Empty<string>();
    }
}
