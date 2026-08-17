namespace Canary.Core.Build;

// Enumerates content/**.md into routes. One rule, applied at every depth
// including the content root itself, kept in sync with
// Canary.Core.Manifest.ManifestBuilder's landing-page rules: a file named
// "index.md" is that directory's own landing page, mapping to the directory
// itself rather than "<dir>/index" -- so content root's index.md maps to the
// site root ("/"), and <subdir>/index.md maps to <subdir> itself, matching
// ManifestBuilder's nav item path for that directory (nav links and
// generated files always agree). Everything else maps to its path relative
// to the content root, extension stripped.
public static class ContentScanner
{
    public static IReadOnlyList<ContentRoute> Scan(string contentRoot)
    {
        var routes = new List<ContentRoute>();

        foreach (var file in Directory.GetFiles(contentRoot, "*.md", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(contentRoot, file).Replace('\\', '/');
            var fileName = Path.GetFileName(file);

            string routePath;
            string outputRelativePath;

            if (relative.Equals("index.md", StringComparison.Ordinal))
            {
                // Content root's own index.md -- the site root.
                routePath = "/";
                outputRelativePath = "index.html";
            }
            else if (fileName.Equals("index.md", StringComparison.OrdinalIgnoreCase))
            {
                var dirRelative = relative[..^"index.md".Length].TrimEnd('/');
                routePath = dirRelative;
                outputRelativePath = $"{dirRelative}/index.html";
            }
            else
            {
                var withoutExt = relative[..^3]; // strip ".md"
                routePath = withoutExt;
                outputRelativePath = $"{withoutExt}/index.html";
            }

            routes.Add(new ContentRoute(file, routePath, outputRelativePath));
        }

        return routes;
    }
}
