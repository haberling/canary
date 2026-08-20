namespace Canary.Core.Build;

public enum PageWriteOutcome
{
    Written,
    Unchanged,
}

public sealed record PageBuildResult(string OutputPath, PageWriteOutcome Outcome);

// Builds one prerendered page: markdown -> HTML content, stamped into the
// shell template. Every call fully renders -- markdown source is always
// re-read and re-rendered, and transformSource (hooks) always runs; there is
// no cache to hit or miss (see PLAN.md's "Incremental builds" section for
// why checksum-gating was removed in favor of this plus SiteBuilder's
// targeted single-route rebuilds).
//
// The only thing this class still avoids is an unnecessary DISK WRITE: since
// rendering is deterministic, an unchanged page renders to the exact same
// HTML string as last time, so the freshly-built HTML is compared against
// whatever's already on disk and only written when it actually differs.
// That keeps a committed output.dir's git diffs small by construction
// (direct comparison against reality) rather than by tracking every input
// that could make a cached result stale -- the exact bug class checksum-
// gating kept hitting.
public sealed class PageBuilder
{
    private readonly Markdown.MarkdownRenderer _markdownRenderer;

    public PageBuilder(Markdown.MarkdownRenderer markdownRenderer)
    {
        _markdownRenderer = markdownRenderer;
    }

    // shellTemplate placeholders: {{title}}, {{siteName}}, {{content}},
    // {{widgetScripts}}, {{widgetStyles}}. widgetScriptsHtml/widgetStylesHtml
    // are pre-built by the caller (SiteBuilder, which knows what's
    // discovered site-wide) -- PageBuilder just substitutes them, same as
    // every other placeholder.
    //
    // transformSource, if given, is applied to the raw markdown before
    // rendering. The title is always read from the raw, pre-transform
    // source (hooks are for structural additions like a breadcrumb or
    // footer link, not rewriting a page's own heading).
    public PageBuildResult BuildPage(
        string sourceMarkdownPath, string outputHtmlPath, string shellTemplate, string siteName,
        string widgetScriptsHtml = "", string widgetStylesHtml = "", Func<string, string>? transformSource = null)
    {
        var source = File.ReadAllText(sourceMarkdownPath);
        var transformed = transformSource != null ? transformSource(source) : source;
        var content = _markdownRenderer.Render(transformed);

        var title = TitleFromMarkdown(source, Path.GetFileNameWithoutExtension(sourceMarkdownPath));
        var pageTitle = title == siteName ? siteName : $"{title} — {siteName}";

        var html = shellTemplate
            .Replace("{{title}}", pageTitle)
            .Replace("{{siteName}}", siteName)
            .Replace("{{widgetScripts}}", widgetScriptsHtml)
            .Replace("{{widgetStyles}}", widgetStylesHtml)
            .Replace("{{content}}", content);

        if (File.Exists(outputHtmlPath) && File.ReadAllText(outputHtmlPath) == html)
        {
            return new PageBuildResult(outputHtmlPath, PageWriteOutcome.Unchanged);
        }

        var outputDir = Path.GetDirectoryName(outputHtmlPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
        File.WriteAllText(outputHtmlPath, html);

        return new PageBuildResult(outputHtmlPath, PageWriteOutcome.Written);
    }

    private static string TitleFromMarkdown(string source, string fallbackSlug)
    {
        foreach (var line in source.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ")) return trimmed[2..].TrimEnd('\r').Trim();
        }
        return TitleCase(fallbackSlug);
    }

    private static string TitleCase(string slug)
    {
        var words = slug.Replace('-', ' ').Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }
}
