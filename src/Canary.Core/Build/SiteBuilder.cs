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
    // (runtime/dist/widgets/*.html + *.js, copied there unchanged -- no
    // compile step, see PLAN.md's widget-controversy notes). Null skips
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

        // Own step, sequenced right after the manifest build rather than
        // folded into BuildPrerendered's page-rendering loop -- sitemap/
        // robots generation is a distinct concern from rendering pages, it
        // just happens to need the same route list.
        WriteSeoFiles(config, outputRoot, routes);

        var summary = BuildPrerendered(config, siteRoot, contentRoot, outputRoot, routes, runtimeDistDir, behaviorScripts);

        if (runtimeDistDir != null)
        {
            CopyRuntimeAssets(config.RenderMode, runtimeDistDir, outputRoot);
        }

        // Not gated on runtimeDistDir: site-authored widget behavior
        // scripts are discovered independently of it, and a page's
        // {{widgetScripts}} references them either way -- gating this
        // would leave those <script> tags pointing at files that were
        // never actually copied.
        CopyWidgetFiles(behaviorScripts, Path.Combine(outputRoot, "js", "widgets"));

        return summary;
    }

    private static BuildSummary BuildPrerendered(
        CanaryConfig config, string siteRoot, string contentRoot, string outputRoot, IReadOnlyList<ContentRoute> routes,
        string? runtimeDistDir, Dictionary<string, string> behaviorScripts)
    {
        var shellTemplate = LoadShellTemplate(config, siteRoot);
        var widgetTemplates = Widgets.WidgetDiscovery.Discover(siteRoot, runtimeDistDir, "*.html");
        var widgets = widgetTemplates.ToDictionary(
            kv => kv.Key, Markdown.IWidgetRenderer (kv) => new Widgets.TemplateWidgetRenderer(kv.Value));
        var widgetScriptsHtml = BuildWidgetScriptsHtml(behaviorScripts.Keys);

        var pageBuilder = new PageBuilder(new Markdown.MarkdownRenderer(widgets));

        var rendered = 0;
        var reused = 0;
        foreach (var route in routes)
        {
            var outputPath = Path.Combine(outputRoot, route.OutputRelativePath);
            var result = pageBuilder.BuildPage(route.SourcePath, outputPath, shellTemplate, config.Site.Name!, widgetScriptsHtml);
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
            if (name == ".nav.json" || isMarkdown) continue;

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
