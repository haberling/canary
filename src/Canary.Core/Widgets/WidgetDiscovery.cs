namespace Canary.Core.Widgets;

// Discovers widget files (templates and behavior scripts) the same way for
// every consumer -- SiteBuilder's build pipeline and the `canary widgets`/
// `canary widget <name>` CLI commands both need the exact same rule. One
// implementation, not two copies that could drift -- see PLAN.md's
// widget-controversy notes for why that matters in this codebase
// specifically (it's exactly how two earlier bugs happened).
//
// No registration, no config entry: "drop a file in and it's used if
// referenced." Two sources, site-authored taking precedence on a name
// collision:
//   - built-in: runtimeDistDir/widgets/ (Canary's own downloads/slideshow
//     .html templates + .js behavior, copied unchanged as part of Canary's
//     own release -- no compile step)
//   - site-authored: <siteRoot>/widgets/
// Call once per extension: "*.html" for templates, "*.js" for optional
// shared behavior scripts.
public static class WidgetDiscovery
{
    public static Dictionary<string, string> Discover(string siteRoot, string? runtimeDistDir, string pattern)
    {
        var files = new Dictionary<string, string>();
        if (runtimeDistDir != null)
        {
            AddFilesFrom(files, Path.Combine(runtimeDistDir, "widgets"), pattern);
        }
        AddFilesFrom(files, Path.Combine(siteRoot, "widgets"), pattern);
        return files;
    }

    private static void AddFilesFrom(Dictionary<string, string> files, string dir, string pattern)
    {
        if (!Directory.Exists(dir)) return;

        foreach (var file in Directory.GetFiles(dir, pattern, SearchOption.TopDirectoryOnly))
        {
            files[Path.GetFileNameWithoutExtension(file).ToLowerInvariant()] = file;
        }
    }
}
