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
    public BuildSummary Build(CanaryConfig config, string siteRoot, string? runtimeDistDir = null)
    {
        var contentRoot = Path.Combine(siteRoot, config.Content.Root!);
        var outputRoot = Path.Combine(siteRoot, config.Output.Dir);
        Directory.CreateDirectory(outputRoot);

        Manifest.ManifestBuilder.BuildAndWrite(contentRoot);
        var routes = ContentScanner.Scan(contentRoot);
        var behaviorScripts = Widgets.WidgetDiscovery.Discover(siteRoot, runtimeDistDir, "*.js");
        // Widget CSS: discovered and shipped exactly like behavior scripts
        // -- built-in and site-authored are found the same way, no widget
        // ever gets special-cased styling another widget can't also have.
        // See PLAN.md's Widget system section for why this exists (it
        // didn't, until a review caught built-in widget CSS baked directly
        // into framework.css, breaking that same-treatment promise).
        var styleSheets = Widgets.WidgetDiscovery.Discover(siteRoot, runtimeDistDir, "*.css");

        // Auto-create a self-documenting .hooks.json for every content
        // directory that has markdown directly inside it (derived from the
        // route list just scanned, not a second filesystem walk) -- same
        // "drop a file in, discoverable, editable" spirit as .nav.json's
        // auto-creation, just scoped deeper. See Hooks.HooksOverrideFile.
        Hooks.HooksOverrideFile.EnsureFilesExist(routes.Select(r => Path.GetDirectoryName(r.SourcePath)!));

        // Own step, sequenced right after the manifest build rather than
        // folded into BuildPrerendered's page-rendering loop -- sitemap/
        // robots generation is a distinct concern from rendering pages, it
        // just happens to need the same route list.
        WriteSeoFiles(config, outputRoot, routes);

        var summary = BuildPrerendered(config, siteRoot, contentRoot, outputRoot, routes, runtimeDistDir, behaviorScripts, styleSheets);

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

    private static BuildSummary BuildPrerendered(
        CanaryConfig config, string siteRoot, string contentRoot, string outputRoot, IReadOnlyList<ContentRoute> routes,
        string? runtimeDistDir, Dictionary<string, string> behaviorScripts, Dictionary<string, string> styleSheets)
    {
        var shellTemplate = LoadShellTemplate(config, siteRoot);
        var widgetTemplates = Widgets.WidgetDiscovery.Discover(siteRoot, runtimeDistDir, "*.html");
        var widgets = widgetTemplates.ToDictionary(
            kv => kv.Key, Markdown.IWidgetRenderer (kv) => new Widgets.TemplateWidgetRenderer(kv.Value));
        var widgetScriptsHtml = BuildWidgetScriptsHtml(behaviorScripts.Keys);
        var widgetStylesHtml = BuildWidgetStylesHtml(styleSheets.Keys);

        // One combined hash of every discovered widget file's content,
        // computed once for the whole build (not per page) -- same
        // site-wide-not-per-usage simplicity tradeoff already made for
        // {{widgetScripts}}/{{widgetStyles}}, rather than scanning each
        // page's markdown to know exactly which widgets it references.
        // Folded into every page's checksum below so editing any widget
        // (template, behavior, OR style) invalidates every page's cache,
        // fixing the gap tracked in PLAN.md's Known bugs.
        var widgetChecksumSeed = ComputeWidgetChecksumSeed(widgetTemplates, behaviorScripts, styleSheets);

        var pageBuilder = new PageBuilder(new Markdown.MarkdownRenderer(widgets));

        var rendered = 0;
        var reused = 0;
        foreach (var route in routes)
        {
            var outputPath = Path.Combine(outputRoot, route.OutputRelativePath);
            var routeDir = Path.GetDirectoryName(route.SourcePath)!;
            var hookNames = Hooks.HooksOverrideFile.ResolveForDirectory(routeDir);
            var extraChecksumSeed = widgetChecksumSeed + "\0" + Hooks.HookRunner.ChecksumSeed(hookNames, config.Hooks, siteRoot);
            Func<string, string>? transformSource = hookNames.Count > 0
                ? source => Hooks.HookRunner.Run(hookNames, config.Hooks, siteRoot, source)
                : null;

            var result = pageBuilder.BuildPage(
                route.SourcePath, outputPath, shellTemplate, config.Site.Name!,
                widgetScriptsHtml, widgetStylesHtml, extraChecksumSeed, transformSource);
            if (result.ContentOutcome == ContentRenderOutcome.Rendered) rendered++;
            else reused++;
        }

        CopyThemeAssets(config, siteRoot, outputRoot);
        // Content is always prerendered -- markdown source never ships to
        // the client, there's nothing for the browser to fetch.
        CopyContentAssets(contentRoot, outputRoot);

        return new BuildSummary(routes.Count, rendered, reused, outputRoot);
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

    private static string ComputeWidgetChecksumSeed(
        Dictionary<string, string> templates, Dictionary<string, string> behaviorScripts, Dictionary<string, string> styleSheets)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var path in templates.Values.Concat(behaviorScripts.Values).Concat(styleSheets.Values).OrderBy(p => p, StringComparer.Ordinal))
        {
            sb.Append(File.ReadAllText(path)).Append('\0');
        }
        return sb.ToString();
    }

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
            if (name is ".nav.json" or ".hooks.json" || isMarkdown) continue;

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
