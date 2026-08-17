using Canary.Core.Markdown;

namespace Canary.Core.Tests.Markdown;

public class MarkdownRendererTests
{
    private static MarkdownRenderer NewRenderer(IReadOnlyDictionary<string, IWidgetRenderer>? widgets = null) =>
        new(widgets ?? new Dictionary<string, IWidgetRenderer>());

    [Fact]
    public void Render_Heading()
    {
        var html = NewRenderer().Render("# Title");
        Assert.Equal("<h1>Title</h1>", html);
    }

    [Fact]
    public void Render_Paragraph_JoinsWrappedLinesWithSpace()
    {
        var html = NewRenderer().Render("line one\nline two");
        Assert.Equal("<p>line one line two</p>", html);
    }

    [Theory]
    [InlineData("**bold**", "<p><strong>bold</strong></p>")]
    [InlineData("__bold__", "<p><strong>bold</strong></p>")]
    [InlineData("*italic*", "<p><em>italic</em></p>")]
    [InlineData("_italic_", "<p><em>italic</em></p>")]
    public void Render_InlineEmphasis(string source, string expected)
    {
        Assert.Equal(expected, NewRenderer().Render(source));
    }

    [Fact]
    public void Render_InlineCode_IsNotMangledByOtherInlineRules()
    {
        // The code span's contents (including a literal "*") must survive
        // untouched by the bold/italic passes that run after code spans are
        // protected behind the marker token.
        var html = NewRenderer().Render("`a * b`");
        Assert.Equal("<p><code>a * b</code></p>", html);
    }

    [Fact]
    public void Render_UnorderedList()
    {
        var html = NewRenderer().Render("- one\n- two");
        Assert.Equal("<ul><li>one</li><li>two</li></ul>", html);
    }

    [Fact]
    public void Render_OrderedList()
    {
        var html = NewRenderer().Render("1. one\n2. two");
        Assert.Equal("<ol><li>one</li><li>two</li></ol>", html);
    }

    [Fact]
    public void Render_Blockquote()
    {
        var html = NewRenderer().Render("> quoted text");
        Assert.Equal("<blockquote><p>quoted text</p></blockquote>", html);
    }

    [Fact]
    public void Render_HorizontalRule()
    {
        Assert.Equal("<hr>", NewRenderer().Render("---"));
    }

    [Fact]
    public void Render_FencedCodeBlock_NoTag_RendersAsPlainCode()
    {
        var html = NewRenderer().Render("```\nplain <text>\n```");
        Assert.Equal("<pre><code>plain &lt;text&gt;</code></pre>", html);
    }

    [Fact]
    public void Render_FencedBlock_UnknownWidgetTag_FallsBackToPlainCode()
    {
        var html = NewRenderer().Render("```nonexistent-widget\nbody\n```");
        Assert.Equal("<pre><code>body</code></pre>", html);
    }

    [Fact]
    public void Render_FencedBlock_DispatchesToRegisteredWidget_CaseInsensitiveTag()
    {
        var widgets = new Dictionary<string, IWidgetRenderer>
        {
            ["mywidget"] = new RecordingWidget(),
        };
        var html = NewRenderer(widgets).Render("```MyWidget\nbody line\n```");
        Assert.Equal("[widget body=\"body line\"]", html);
    }

    [Fact]
    public void Render_Link_RootRelativePath_GetsLeadingSlash()
    {
        // Consoland's content authors write paths relative to the site root
        // (e.g. "content/games/images/x.png"), which only worked in the old
        // pure-SPA where every page rendered at "/". Prerendering to real
        // nested paths requires making these root-relative for real.
        var html = NewRenderer().Render("[text](content/page)");
        Assert.Equal("<p><a href=\"/content/page\">text</a></p>", html);
    }

    [Fact]
    public void Render_Link_UrlWithAmpersand_IsNotDoubleEscaped()
    {
        // Regression test: RenderInline HTML-escapes the whole line before
        // the link regex extracts the URL, so a raw "&" in a query string
        // becomes "&amp;" before EscapeAttr ever sees it -- without
        // UnescapeHtmlEntities, EscapeAttr would escape it again into
        // "&amp;amp;". Found while porting; see PLAN.md's Phase 1 notes.
        var html = NewRenderer().Render("[text](https://example.com?a=1&b=2)");
        Assert.Equal("<p><a href=\"https://example.com?a=1&amp;b=2\">text</a></p>", html);
    }

    [Fact]
    public void Render_Link_AbsoluteUrl_IsLeftAlone()
    {
        var html = NewRenderer().Render("[text](https://example.com/page)");
        Assert.Equal("<p><a href=\"https://example.com/page\">text</a></p>", html);
    }

    [Fact]
    public void Render_Link_AlreadyRootRelative_IsLeftAlone()
    {
        var html = NewRenderer().Render("[text](/already/rooted)");
        Assert.Equal("<p><a href=\"/already/rooted\">text</a></p>", html);
    }

    [Fact]
    public void Render_Image_RelativePath_GetsLeadingSlash()
    {
        var html = NewRenderer().Render("![alt text](content/games/images/x.png)");
        Assert.Equal("<p><img src=\"/content/games/images/x.png\" alt=\"alt text\"></p>", html);
    }

    private sealed class RecordingWidget : IWidgetRenderer
    {
        public string Render(string body) => $"[widget body=\"{body}\"]";
    }
}
