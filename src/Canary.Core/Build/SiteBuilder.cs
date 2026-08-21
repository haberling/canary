using Canary.Core.Config;

namespace Canary.Core.Build;

// Top-level orchestrator -- the only layer in Canary.Core allowed to know
// about Manifest, Markdown, Widgets, and Templating all at once, and the
// only layer that touches CanaryConfig directly. Everything below it takes
// plain paths/strings, not config objects.
public sealed class SiteBuilder
{
    // runtimeDistDir points at the compiled JS runtime (runtime/dist/, see
    // PLAN.md's Client runtime packaging section) AND the built-in widgets
    // (runtime/dist/widgets/*.html + *.js + *.css, copied there unchanged --
    // no compile step, see PLAN.md's widget-controversy notes). Null skips
    // both: the build still produces valid HTML/CSS, just without nav/
    // routing JS and without any widgets rendering (fenced blocks fall back
    // to plain code blocks) -- true embedding into the Canary package
    // itself is still a known, tracked gap, not solved here; this parameter
    // is the honest interim.
    // changedPaths, when given, comes from SiteWatcher's debounced file-
    // change accumulation (see Serve.SiteWatcher and Program.cs's serve
    // command) -- a non-null set means "only these files changed since the
    // last build, and every one of them was a plain edit to an existing
    // file" (SiteWatcher itself downgrades anything else -- a create,
    // delete, rename -- to null before it ever reaches here). null means
    // "render every route" (a bare `canary build`, serve's initial build,
    // or any structural change SiteWatcher couldn't safely narrow down).
    // See PLAN.md's "Incremental builds" section.
    public BuildSummary Build(CanaryConfig config, string siteRoot, string? runtimeDistDir = null, IReadOnlySet<string>? changedPaths = null)
    {
        var contentRoot = Path.Combine(siteRoot, config.Content.Root!);
        var outputRoot = Path.Combine(siteRoot, config.Output.Dir);
        Directory.CreateDirectory(outputRoot);

        // Site-wide bookkeeping always runs in full, even for a targeted
        // rebuild -- these are cheap file-list scans and small writes, not
        // the expensive part, and skipping them would risk the nav tree/
        // sitemap silently going stale relative to the content that just
        // changed.
        Manifest.ManifestBuilder.BuildAndWrite(contentRoot, config.Nav.Depth);
        var routes = ContentScanner.Scan(contentRoot);
        var behaviorScripts = Widgets.WidgetDiscovery.Discover(siteRoot, runtimeDistDir, "*.js");
        // Widget CSS: discovered and shipped exactly like behavior scripts
        // -- built-in and site-authored are found the same way, no widget
        // ever gets special-cased styling another widget can't also have.
        // See PLAN.md's Widget system section for why this exists (it
        // didn't, until a review caught built-in widget CSS baked directly
        // into framework.css, breaking that same-treatment promise).
        var styleSheets = Widgets.WidgetDiscovery.Discover(siteRoot, runtimeDistDir, "*.css");

        // Auto-create a self-documenting .toolchain.json for every content
        // directory that has markdown directly inside it (derived from the
        // route list just scanned, not a second filesystem walk) -- same
        // "drop a file in, discoverable, editable" spirit as .nav.json's
        // auto-creation, just scoped deeper. See Toolchain.ToolchainOverrideFile.
        Toolchain.ToolchainOverrideFile.EnsureFilesExist(routes.Select(r => Path.GetDirectoryName(r.SourcePath)!));

        // Own step, sequenced right after the manifest build rather than
        // folded into BuildPrerendered's page-rendering loop -- sitemap/
        // robots generation is a distinct concern from rendering pages, it
        // just happens to need the same route list.
        WriteSeoFiles(config, outputRoot, routes);

        var routesToRender = ResolveRoutesToRender(routes, changedPaths);
        var summary = BuildPrerendered(config, siteRoot, contentRoot, outputRoot, routes, routesToRender, runtimeDistDir, behaviorScripts, styleSheets);

        if (runtimeDistDir != null)
        {
            CopyRuntimeAssets(config.RenderMode, runtimeDistDir, outputRoot);
        }

        // Not gated on runtimeDistDir: site-authored widget behavior
        // scripts/styles are discovered independently of it, and a page's
        // {{widgetScripts}}/{{widgetStyles}} reference them either way --
        // gating this would leave those tags pointing at files that were
        // never actually copied.
        CopyWidgetFiles(behaviorScripts, Path.Combine(outputRoot, "js", "widgets"));
        CopyWidgetFiles(styleSheets, Path.Combine(outputRoot, "css", "widgets"));

        return summary;
    }

    // null (render everything) unless every path in changedPaths maps
    // cleanly onto an existing route's own source file -- a changed path
    // that isn't any route's SourcePath (a widget/tool-script/theme/
    // config.json edit, say) means the change could affect more than just
    // one page, so it falls back to rendering everything rather than
    // guessing.
    private static IReadOnlyList<ContentRoute> ResolveRoutesToRender(IReadOnlyList<ContentRoute> routes, IReadOnlySet<string>? changedPaths)
    {
        if (changedPaths == null || changedPaths.Count == 0) return routes;

        var normalizedChanged = new HashSet<string>(changedPaths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
        var matched = routes.Where(r => normalizedChanged.Contains(Path.GetFullPath(r.SourcePath))).ToList();

        var matchedSourcePaths = new HashSet<string>(matched.Select(r => Path.GetFullPath(r.SourcePath)), StringComparer.OrdinalIgnoreCase);
        var everyChangeIsARoute = normalizedChanged.All(matchedSourcePaths.Contains);

        return everyChangeIsARoute ? matched : routes;
    }

    private static BuildSummary BuildPrerendered(
        CanaryConfig config, string siteRoot, string contentRoot, string outputRoot, IReadOnlyList<ContentRoute> allRoutes, IReadOnlyList<ContentRoute> routesToRender,
        string? runtimeDistDir, Dictionary<string, string> behaviorScripts, Dictionary<string, string> styleSheets)
    {
        var shellTemplate = LoadShellTemplate(config, siteRoot);
        var widgetTemplates = Widgets.WidgetDiscovery.Discover(siteRoot, runtimeDistDir, "*.html");
        var widgets = widgetTemplates.ToDictionary(
            kv => kv.Key, Markdown.IWidgetRenderer (kv) => new Widgets.TemplateWidgetRenderer(kv.Value));
        var widgetScriptsHtml = BuildWidgetScriptsHtml(behaviorScripts.Keys);
        var widgetStylesHtml = BuildWidgetStylesHtml(styleSheets.Keys);

        var pageBuilder = new PageBuilder(new Markdown.MarkdownRenderer(widgets));

        // Written by ManifestBuilder.BuildAndWrite above (Build's caller),
        // so it's already current on disk by the time any tool runs.
        var manifestPath = Path.Combine(contentRoot, "manifest.json");

        var written = 0;
        var unchanged = 0;
        foreach (var route in routesToRender)
        {
            var outputPath = Path.Combine(outputRoot, route.OutputRelativePath);
            var routeDir = Path.GetDirectoryName(route.SourcePath)!;
            var toolNames = Toolchain.ToolchainOverrideFile.ResolveForDirectory(routeDir);
            // Bare nav-tree convention, not ContentScanner's URL-style
            // RoutePath -- see Toolchain.ToolchainContext's doc comment.
            var navRoutePath = route.RoutePath == "/" ? "" : route.RoutePath;
            var toolchainContext = new Toolchain.ToolchainContext(siteRoot, navRoutePath, manifestPath);
            Func<string, string>? transformSource = toolNames.Count > 0
                ? source => Toolchain.ToolchainRunner.Run(toolNames, config.Tools, toolchainContext, source)
                : null;

            var result = pageBuilder.BuildPage(
                route.SourcePath, outputPath, shellTemplate, config.Site.Name!,
                widgetScriptsHtml, widgetStylesHtml, transformSource);
            if (result.Outcome == PageWriteOutcome.Written) written++;
            else unchanged++;
        }

        CopyThemeAssets(config, siteRoot, outputRoot);
        // Content is always prerendered -- markdown source never ships to
        // the client, there's nothing for the browser to fetch.
        CopyContentAssets(contentRoot, outputRoot);

        return new BuildSummary(allRoutes.Count, written, unchanged, outputRoot);
    }

    private static void WriteSeoFiles(CanaryConfig config, string outputRoot, IReadOnlyList<ContentRoute> routes)
    {
        File.WriteAllText(Path.Combine(outputRoot, "sitemap.xml"), SitemapBuilder.Build(config.Site.BaseUrl!, routes));
        File.WriteAllText(Path.Combine(outputRoot, "robots.txt"), RobotsBuilder.Build(config.Site.BaseUrl!));
    }

    private static string LoadShellTemplate(CanaryConfig config, string siteRoot)
    {
        if (string.IsNullOrWhiteSpace(config.Theme.Shell))
        {
            throw new InvalidOperationException("theme.shell is required.");
        }

        var shellPath = Path.Combine(siteRoot, config.Theme.Shell);
        if (!File.Exists(shellPath))
        {
            throw new InvalidOperationException($"theme.shell file not found: {shellPath}");
        }

        return File.ReadAllText(shellPath);
    }

    private static string BuildWidgetScriptsHtml(IEnumerable<string> widgetNames) =>
        string.Concat(widgetNames.OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => $"<script src=\"/js/widgets/{n}.js\" defer></script>\n"));

    private static string BuildWidgetStylesHtml(IEnumerable<string> widgetNames) =>
        string.Concat(widgetNames.OrderBy(n => n, StringComparer.Ordinal)
            .Select(n => $"<link rel=\"stylesheet\" href=\"/css/widgets/{n}.css\">\n"));

    private static void CopyWidgetFiles(Dictionary<string, string> files, string destDir)
    {
        foreach (var (name, sourcePath) in files)
        {
            CopyFile(sourcePath, Path.Combine(destDir, $"{name}{Path.GetExtension(sourcePath)}"));
        }
    }

    private static void CopyThemeAssets(CanaryConfig config, string siteRoot, string outputRoot)
    {
        if (!string.IsNullOrWhiteSpace(config.Theme.Base))
        {
            CopyFile(Path.Combine(siteRoot, config.Theme.Base), Path.Combine(outputRoot, "css", "framework.css"));
        }

        if (!string.IsNullOrWhiteSpace(config.Theme.Theme))
        {
            CopyFile(Path.Combine(siteRoot, config.Theme.Theme), Path.Combine(outputRoot, "css", "theme.css"));
        }
    }

    // Mirrors consoland's deploy.cs CopyContent. Markdown source is never
    // copied -- content is always prerendered, so there's nothing for the
    // browser to fetch.
    private static void CopyContentAssets(string contentRoot, string outputRoot)
    {
        var destRoot = Path.Combine(outputRoot, "content");
        foreach (var file in Directory.GetFiles(contentRoot, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            var isMarkdown = file.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
            if (name is ".nav.json" || name == Toolchain.ToolchainOverrideFile.FileName || isMarkdown) continue;

            var relative = Path.GetRelativePath(contentRoot, file);
            CopyFile(file, Path.Combine(destRoot, relative));
        }
    }

    private static void CopyRuntimeAssets(RenderMode mode, string runtimeDistDir, string outputRoot)
    {
        var (entryFile, supportFiles) = RuntimeAssetManifest.For(mode);
        var jsOutputDir = Path.Combine(outputRoot, "js");

        CopyFile(Path.Combine(runtimeDistDir, entryFile), Path.Combine(jsOutputDir, "main.js"));
        foreach (var file in supportFiles)
        {
            CopyFile(Path.Combine(runtimeDistDir, file), Path.Combine(jsOutputDir, file));
        }
    }

    private static void CopyFile(string sourcePath, string destPath)
    {
        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException($"Expected asset not found: {sourcePath}");
        }

        var destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        File.Copy(sourcePath, destPath, overwrite: true);
    }
}
