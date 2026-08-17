using System.Text.Json;

namespace Canary.Core.Manifest;

// Ported from consoland's tools/build-manifest.cs (see PLAN.md's "Core built-in
// functionality" section), with three behavior changes from the original:
//  - Site-root discovery is gone: Canary already knows the content root from
//    the site config (Content.Root), so there's no ".nav.json siteRoot: true"
//    marker search to do.
//  - A subdirectory's landing page is now found by the fixed filename
//    "index.md", not by matching the directory's own name. The old
//    "content/<dir>/<Dir>.md" convention produced routes like "games/games"
//    -- a redundant extra path segment. "content/<dir>/index.md" maps to the
//    route "<dir>" (via ContentScanner's matching convention), same as the
//    standard "a directory's index.html is its own landing page" idea.
//  - The site root's own landing page uses that exact same "index.md"
//    filename too, replacing consoland's separate "home.md" convention --
//    one rule ("index.md is a directory's landing page") instead of two.
//    The pinned "Home" nav item behavior (always first, always titled
//    "Home" regardless of the file's own heading) is unchanged; only the
//    filename Canary looks for changed.
//
// Rules:
//  - content/index.md, if present, always becomes a "Home" nav item pinned
//    first, ahead of the alphabetically-sorted rest.
//  - A loose top-level content/<name>.md becomes a flat nav item.
//  - A top-level content/<dir>/ becomes a nav item:
//      - If content/<dir>/index.md exists, that page is the landing page:
//        the nav item is clickable and links to <dir> itself (not
//        <dir>/index).
//      - Every other .md file directly in <dir>/ becomes a dropdown child.
//      - No landing page -> the nav item is a non-navigating dropdown
//        trigger. No landing page AND no children -> omitted entirely.
//  - Each item's title is its file's first "# Heading" line, else a
//    title-cased version of the filename/directory name.
//  - content/<dir>/.nav.json can set allow/deny (mutually exclusive),
//    priority (int, default 0, lower sorts earlier), and nonav (bool).
//    "Home" is always pinned first regardless of priority.
//  - Any top-level content/<dir>/ with at least one .md file gets a
//    self-documenting .nav.json written/backfilled if missing fields.
//
// Only recurses one level into content/ subdirectories.
public static class ManifestBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public static SiteManifest Build(string contentRoot)
    {
        var nav = BuildNav(contentRoot);
        return new SiteManifest { Nav = nav };
    }

    public static SiteManifest BuildAndWrite(string contentRoot, string? outputPath = null)
    {
        var manifest = Build(contentRoot);
        var path = outputPath ?? Path.Combine(contentRoot, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, SerializerOptions);
        File.WriteAllText(path, json + "\n");
        return manifest;
    }

    private static List<NavItem> BuildNav(string contentRoot)
    {
        var items = new List<NavItem>();

        var homeFile = Path.Combine(contentRoot, "index.md");
        if (File.Exists(homeFile))
        {
            // "" (not "/") to match the same bare-relative-fragment
            // convention every other nav path uses -- the client renders
            // href="#/${path}", so "" produces the canonical root hash "#/".
            // (ContentScanner's routePath for this same file is "/", a
            // different representation for a different purpose: identifying
            // the actual generated route/output path, not a client href
            // fragment. They're intentionally not the same string.)
            items.Add(new NavItem { Title = "Home", Path = "" });
        }

        var rest = new List<NavItem>();

        foreach (var file in Directory.GetFiles(contentRoot, "*.md", SearchOption.TopDirectoryOnly))
        {
            var slug = Path.GetFileNameWithoutExtension(file);
            if (string.Equals(slug, "index", StringComparison.OrdinalIgnoreCase)) continue;

            rest.Add(new NavItem
            {
                Title = TitleFromFile(file, slug),
                Path = ToContentPath(contentRoot, file),
            });
        }

        foreach (var dir in Directory.GetDirectories(contentRoot))
        {
            var item = BuildDirectoryNavItem(contentRoot, dir);
            if (item != null)
            {
                rest.Add(item);
            }
        }

        items.AddRange(rest.OrderBy(n => n.Priority).ThenBy(n => n.Title, StringComparer.OrdinalIgnoreCase));
        return items;
    }

    private static NavItem? BuildDirectoryNavItem(string contentRoot, string dir)
    {
        var dirName = Path.GetFileName(dir);
        var mdFiles = Directory.GetFiles(dir, "*.md", SearchOption.TopDirectoryOnly);

        var config = LoadOrCreateNavOverride(dir, mdFiles.Length > 0);
        if (config.NoNav)
        {
            return null;
        }

        var landingFile = mdFiles.FirstOrDefault(f =>
            string.Equals(Path.GetFileName(f), "index.md", StringComparison.OrdinalIgnoreCase));

        var candidates = mdFiles.Where(f => f != landingFile).ToList();
        var allowed = ApplyNavOverride(dir, candidates, config);

        var children = allowed
            .Select(f => new NavItem { Title = TitleFromFile(f, Path.GetFileNameWithoutExtension(f)), Path = ToContentPath(contentRoot, f) })
            .OrderBy(n => n.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string title;
        string? path = null;

        if (landingFile != null)
        {
            title = TitleFromFile(landingFile, dirName);
            // The directory's own route, not "dir/index" -- matches
            // ContentScanner mapping <dir>/index.md to <dir>/index.html so a
            // subdirectory's landing page has no redundant extra path
            // segment (the old consoland convention of games/games.md ->
            // "games/games" is exactly what this replaces).
            path = Path.GetRelativePath(contentRoot, dir).Replace('\\', '/');
        }
        else
        {
            title = TitleCase(dirName);
        }

        if (landingFile == null && children.Count == 0) return null;

        return new NavItem
        {
            Title = title,
            Path = path,
            Children = children.Count > 0 ? children : null,
            Priority = config.Priority,
        };
    }

    private static NavOverride LoadOrCreateNavOverride(string dir, bool hasMdFiles)
    {
        var overridePath = Path.Combine(dir, ".nav.json");

        if (!File.Exists(overridePath))
        {
            if (!hasMdFiles) return new NavOverride();

            var created = new NavOverride();
            WriteNavOverride(overridePath, created);
            return created;
        }

        var raw = File.ReadAllText(overridePath);
        var config = JsonSerializer.Deserialize<NavOverride>(raw, SerializerOptions) ?? new NavOverride();

        if (!raw.Contains("\"nonav\""))
        {
            WriteNavOverride(overridePath, config);
        }

        return config;
    }

    private static void WriteNavOverride(string path, NavOverride config)
    {
        var json = JsonSerializer.Serialize(config, SerializerOptions);
        File.WriteAllText(path, json + "\n");
    }

    private static List<string> ApplyNavOverride(string dir, List<string> candidateFiles, NavOverride? config)
    {
        if (config == null) return candidateFiles;

        if (config.Allow != null && config.Deny != null)
            throw new InvalidOperationException($"{Path.Combine(dir, ".nav.json")}: specify \"allow\" or \"deny\", not both.");

        if (config.Allow != null)
        {
            var allowSet = new HashSet<string>(config.Allow, StringComparer.OrdinalIgnoreCase);
            return candidateFiles.Where(f => allowSet.Contains(Path.GetFileName(f))).ToList();
        }

        if (config.Deny != null)
        {
            var denySet = new HashSet<string>(config.Deny, StringComparer.OrdinalIgnoreCase);
            return candidateFiles.Where(f => !denySet.Contains(Path.GetFileName(f))).ToList();
        }

        return candidateFiles;
    }

    private static string TitleFromFile(string file, string fallbackSlug)
    {
        foreach (var line in File.ReadLines(file))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ")) return trimmed[2..].Trim();
        }
        return TitleCase(fallbackSlug);
    }

    private static string TitleCase(string slug)
    {
        var words = slug.Replace('-', ' ').Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    private static string ToContentPath(string contentRoot, string file)
    {
        var rel = Path.GetRelativePath(contentRoot, file).Replace('\\', '/');
        return rel[..^3]; // strip ".md"
    }
}
