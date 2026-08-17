using System.Text.RegularExpressions;

namespace Canary.Core.Markdown;

// Ported from consoland's src/ts/markdown.ts -- the hand-rolled markdown -> HTML
// renderer covering: headings, paragraphs, bold/italic, inline code,
// links/images, fenced code blocks, widgets, unordered/ordered lists,
// blockquotes, hr. Semantics are kept identical to the TS original so a page
// renders the same whether it went through this (build-time, `hybrid`/
// `static`) or the client-side renderer (`spa` mode only, see PLAN.md).
//
// One deliberate difference from the TS version: widget dispatch is a
// synchronous dictionary lookup instead of a dynamic `import()`, since the
// full widget set is known upfront at build time (no dynamic loading needed).
public sealed partial class MarkdownRenderer
{
    // Delimiter characters that can't appear in escaped source text, used
    // the same way the TS original uses String.fromCharCode(0)/(1).
    private static readonly string CodeSpanMarker = ((char)0).ToString();
    private static readonly string EscapedBackslashMarker = ((char)1).ToString();

    private readonly IReadOnlyDictionary<string, IWidgetRenderer> _widgets;

    public MarkdownRenderer(IReadOnlyDictionary<string, IWidgetRenderer> widgets)
    {
        _widgets = widgets;
    }

    public string Render(string source)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var html = new List<string>();

        var paragraphBuf = new List<string>();
        var listBuf = new List<string>();
        string? listType = null;
        var quoteBuf = new List<string>();

        void FlushParagraph()
        {
            if (paragraphBuf.Count > 0)
            {
                html.Add($"<p>{RenderInline(string.Join(" ", paragraphBuf))}</p>");
                paragraphBuf.Clear();
            }
        }

        void FlushList()
        {
            if (listBuf.Count > 0 && listType != null)
            {
                var items = string.Concat(listBuf.Select(item => $"<li>{RenderInline(item)}</li>"));
                html.Add($"<{listType}>{items}</{listType}>");
            }
            listBuf.Clear();
            listType = null;
        }

        void FlushQuote()
        {
            if (quoteBuf.Count > 0)
            {
                html.Add($"<blockquote><p>{RenderInline(string.Join(" ", quoteBuf))}</p></blockquote>");
                quoteBuf.Clear();
            }
        }

        void FlushAll()
        {
            FlushParagraph();
            FlushList();
            FlushQuote();
        }

        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            var fenceMatch = FenceRegex().Match(trimmed);
            if (fenceMatch.Success)
            {
                FlushAll();
                var info = fenceMatch.Groups[1].Value.Trim();
                var bodyLines = new List<string>();
                i++;
                while (i < lines.Length && lines[i].Trim() != "```")
                {
                    bodyLines.Add(lines[i]);
                    i++;
                }
                i++; // skip closing fence
                var body = string.Join("\n", bodyLines);

                string? rendered = null;
                if (info.Length > 0)
                {
                    // The fence info string is just the widget name -- no
                    // ":title" suffix anymore. A widget that wants a title
                    // declares a "title" field in its own YAML body like
                    // any other field (see IWidgetRenderer).
                    if (_widgets.TryGetValue(info.ToLowerInvariant(), out var widget))
                    {
                        rendered = widget.Render(body);
                    }
                }

                html.Add(rendered ?? $"<pre><code>{EscapeHtml(body)}</code></pre>");
                continue;
            }

            if (trimmed.Length == 0)
            {
                FlushAll();
                i++;
                continue;
            }

            var headingMatch = HeadingRegex().Match(trimmed);
            if (headingMatch.Success)
            {
                FlushAll();
                var level = headingMatch.Groups[1].Value.Length;
                html.Add($"<h{level}>{RenderInline(headingMatch.Groups[2].Value)}</h{level}>");
                i++;
                continue;
            }

            if (HrRegex().IsMatch(trimmed))
            {
                FlushAll();
                html.Add("<hr>");
                i++;
                continue;
            }

            var ulMatch = UlRegex().Match(trimmed);
            if (ulMatch.Success)
            {
                FlushParagraph();
                FlushQuote();
                if (listType != null && listType != "ul") FlushList();
                listType = "ul";
                listBuf.Add(ulMatch.Groups[1].Value);
                i++;
                continue;
            }

            var olMatch = OlRegex().Match(trimmed);
            if (olMatch.Success)
            {
                FlushParagraph();
                FlushQuote();
                if (listType != null && listType != "ol") FlushList();
                listType = "ol";
                listBuf.Add(olMatch.Groups[1].Value);
                i++;
                continue;
            }

            var quoteMatch = QuoteRegex().Match(trimmed);
            if (quoteMatch.Success)
            {
                FlushParagraph();
                FlushList();
                quoteBuf.Add(quoteMatch.Groups[1].Value);
                i++;
                continue;
            }

            FlushList();
            FlushQuote();
            paragraphBuf.Add(trimmed);
            i++;
        }

        FlushAll();
        return string.Join("\n", html);
    }

    private static string EscapeHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string EscapeAttr(string s) =>
        EscapeHtml(s).Replace("\"", "&quot;");

    // Delegates to Canary.Core.Templating.UrlResolver -- see that class for
    // why this exists (kept as a thin alias here so call sites below don't
    // need a "using Canary.Core.Templating" just for one method name).
    private static string ResolveUrl(string url) => Templating.UrlResolver.Resolve(url);

    // Reverses EscapeHtml's entity substitutions. Needed because RenderInline
    // escapes the WHOLE line before the image/link regexes extract a URL out
    // of it -- without this, a URL containing "&" (e.g. a query string) gets
    // escaped once by that whole-line pass and AGAIN by EscapeAttr below,
    // producing "&amp;amp;". Found while porting; the same bug exists in the
    // original markdown.ts and is fixed there too (runtime/ts/markdown.ts),
    // coordinated rather than fixed on only one side -- see PLAN.md's Phase 1
    // notes for why that matters (build-time/client-time render divergence).
    private static string UnescapeHtmlEntities(string s) =>
        s.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");

    private static string RenderInline(string text)
    {
        var codeSpans = new List<string>();
        var outStr = EscapeHtml(text);

        // Protect inline code spans before any other inline syntax is
        // processed, using a NUL-delimited token that can't appear in
        // escaped source text.
        outStr = CodeSpanRegex().Replace(outStr, m =>
        {
            codeSpans.Add($"<code>{m.Groups[1].Value}</code>");
            return $"{CodeSpanMarker}{codeSpans.Count - 1}{CodeSpanMarker}";
        });

        outStr = ImageRegex().Replace(outStr, m =>
            $"<img src=\"{EscapeAttr(ResolveUrl(UnescapeHtmlEntities(m.Groups[2].Value)))}\" alt=\"{EscapeAttr(m.Groups[1].Value)}\">");

        outStr = LinkRegex().Replace(outStr, m =>
            $"<a href=\"{EscapeAttr(ResolveUrl(UnescapeHtmlEntities(m.Groups[2].Value)))}\">{m.Groups[1].Value}</a>");

        outStr = BoldStarRegex().Replace(outStr, "<strong>$1</strong>");
        outStr = BoldUnderscoreRegex().Replace(outStr, "<strong>$1</strong>");
        outStr = ItalicStarRegex().Replace(outStr, "<em>$1</em>");
        outStr = ItalicUnderscoreRegex().Replace(outStr, "<em>$1</em>");

        // Explicit escapes/passthroughs, applied after EscapeHtml() above has
        // already turned "<" and "\" into inert text. "\\" is pulled out
        // first (CommonMark-style) so a literal backslash before "-" isn't
        // itself consumed by the "\-" escape, e.g. "\\-" -> literal "\-".
        outStr = outStr.Replace("\\\\", EscapedBackslashMarker);
        outStr = BrRegex().Replace(outStr, "<br>");
        outStr = outStr.Replace("\\-", "-");
        outStr = outStr.Replace(EscapedBackslashMarker, "\\");

        outStr = CodeSpanMarkerRegex().Replace(outStr, m => codeSpans[int.Parse(m.Groups[1].Value)]);

        return outStr;
    }

    [GeneratedRegex(@"^```(.*)$")]
    private static partial Regex FenceRegex();

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(-{3,}|\*{3,}|_{3,})$")]
    private static partial Regex HrRegex();

    [GeneratedRegex(@"^[-*]\s+(.*)$")]
    private static partial Regex UlRegex();

    [GeneratedRegex(@"^\d+\.\s+(.*)$")]
    private static partial Regex OlRegex();

    [GeneratedRegex(@"^>\s?(.*)$")]
    private static partial Regex QuoteRegex();

    [GeneratedRegex("`([^`]+)`")]
    private static partial Regex CodeSpanRegex();

    [GeneratedRegex(@"!\[([^\]]*)\]\(([^)\s]+)\)")]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"\[([^\]]*)\]\(([^)\s]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex BoldStarRegex();

    [GeneratedRegex(@"__([^_]+)__")]
    private static partial Regex BoldUnderscoreRegex();

    [GeneratedRegex(@"\*([^*]+)\*")]
    private static partial Regex ItalicStarRegex();

    [GeneratedRegex(@"_([^_]+)_")]
    private static partial Regex ItalicUnderscoreRegex();

    [GeneratedRegex("&lt;br\\s*/?&gt;", RegexOptions.IgnoreCase)]
    private static partial Regex BrRegex();

    // Matches CodeSpanMarker (the character written here as \\u0000) on
    // either side of a captured digit run.
    [GeneratedRegex("\\u0000(\\d+)\\u0000")]
    private static partial Regex CodeSpanMarkerRegex();
}
