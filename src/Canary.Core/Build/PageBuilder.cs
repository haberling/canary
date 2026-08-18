using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Canary.Core.Build;

public enum ContentRenderOutcome
{
    Rendered,
    ReusedUnchanged,
}

public sealed record PageBuildResult(string OutputPath, ContentRenderOutcome ContentOutcome);

// Builds one prerendered page: markdown -> HTML content, stamped into the
// shell template. Chrome (the shell) is unconditionally re-stamped every
// call; only the markdown render step is checksum-gated (see PLAN.md's
// "Incremental builds" section) -- when the checksum matches what's embedded
// in the existing output file, the previous render's #app fragment is
// extracted and reused instead of re-running hooks and the markdown
// renderer.
//
// The checksum covers more than just the markdown source (hence
// "content-checksum", not the old "source-checksum" name): extraChecksumSeed
// lets the caller fold in anything else that can change a page's rendered
// output without the markdown itself changing -- widget template/behavior
// file contents, a page's applicable hooks and their registered commands
// (see SiteBuilder, Canary.Core.Hooks.HookRunner.ChecksumSeed). Without
// this, editing a widget or a hook wouldn't invalidate the cache for pages
// that use it -- exactly the gap tracked in PLAN.md's Known bugs before this
// fix.
public sealed partial class PageBuilder
{
    private readonly Markdown.MarkdownRenderer _markdownRenderer;

    public PageBuilder(Markdown.MarkdownRenderer markdownRenderer)
    {
        _markdownRenderer = markdownRenderer;
    }

    // shellTemplate placeholders: {{title}}, {{siteName}}, {{checksum}},
    // {{content}}, {{widgetScripts}}, {{widgetStyles}}. widgetScriptsHtml/
    // widgetStylesHtml are pre-built by the caller (SiteBuilder, which
    // knows what's discovered site-wide) -- PageBuilder just substitutes
    // them, same as every other placeholder.
    //
    // transformSource, if given, is applied to the raw markdown before
    // rendering -- but ONLY on a cache miss (see below), never just to
    // compute the checksum. Hooks are potentially expensive external
    // commands; the entire point of checksum-gating is to skip expensive
    // work when nothing relevant changed, so running hooks unconditionally
    // just to decide whether to skip them would defeat the gate. The title
    // is always read from the raw, pre-hook source (hooks are for
    // structural additions like a breadcrumb or footer link, not rewriting
    // a page's own heading).
    public PageBuildResult BuildPage(
        string sourceMarkdownPath, string outputHtmlPath, string shellTemplate, string siteName,
        string widgetScriptsHtml = "", string widgetStylesHtml = "", string extraChecksumSeed = "", Func<string, string>? transformSource = null)
    {
        var source = File.ReadAllText(sourceMarkdownPath);
        var checksum = ComputeChecksum(source, extraChecksumSeed);

        var existingContent = TryReuseExisting(outputHtmlPath, checksum);
        string content;
        ContentRenderOutcome outcome;
        if (existingContent != null)
        {
            content = existingContent;
            outcome = ContentRenderOutcome.ReusedUnchanged;
        }
        else
        {
            var transformed = transformSource != null ? transformSource(source) : source;
            content = _markdownRenderer.Render(transformed);
            outcome = ContentRenderOutcome.Rendered;
        }

        var title = TitleFromMarkdown(source, Path.GetFileNameWithoutExtension(sourceMarkdownPath));
        var pageTitle = title == siteName ? siteName : $"{title} — {siteName}";
        var checksumComment = $"<!-- content-checksum: sha256:{checksum} -->";

        var html = shellTemplate
            .Replace("{{title}}", pageTitle)
            .Replace("{{checksum}}", checksumComment)
            .Replace("{{siteName}}", siteName)
            .Replace("{{widgetScripts}}", widgetScriptsHtml)
            .Replace("{{widgetStyles}}", widgetStylesHtml)
            .Replace("{{content}}", content);

        var outputDir = Path.GetDirectoryName(outputHtmlPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
        File.WriteAllText(outputHtmlPath, html);

        return new PageBuildResult(outputHtmlPath, outcome);
    }

    private static string? TryReuseExisting(string outputHtmlPath, string currentChecksum)
    {
        if (!File.Exists(outputHtmlPath)) return null;

        var existingHtml = File.ReadAllText(outputHtmlPath);
        var checksumMatch = ChecksumCommentRegex().Match(existingHtml);
        if (!checksumMatch.Success || checksumMatch.Groups[1].Value != currentChecksum) return null;

        var contentMatch = AppFragmentRegex().Match(existingHtml);
        return contentMatch.Success ? contentMatch.Groups[1].Value : null;
    }

    private static string ComputeChecksum(string source, string extraSeed)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source + "\0" + extraSeed));
        return Convert.ToHexStringLower(bytes);
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

    [GeneratedRegex(@"<!--\s*content-checksum:\s*sha256:([0-9a-f]{64})\s*-->")]
    private static partial Regex ChecksumCommentRegex();

    [GeneratedRegex(@"<main id=""app"">(.*)</main>", RegexOptions.Singleline)]
    private static partial Regex AppFragmentRegex();
}
