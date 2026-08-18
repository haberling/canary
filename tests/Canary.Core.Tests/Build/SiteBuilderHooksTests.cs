using Canary.Core.Build;
using Canary.Core.Config;

namespace Canary.Core.Tests.Build;

public class SiteBuilderHooksTests : IDisposable
{
    private readonly string _siteRoot;

    public SiteBuilderHooksTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "canary-hooks-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content"));
        Directory.CreateDirectory(Path.Combine(_siteRoot, "tools"));
        // {{checksum}} must be present for checksum-gating tests to mean
        // anything -- without it, TryReuseExisting's regex never matches
        // the output HTML, so every build looks like a cache miss
        // regardless of what actually changed. Found via a real test
        // failure caused by this exact gap in an earlier draft.
        File.WriteAllText(Path.Combine(_siteRoot, "shell.html"), "<html><body>{{checksum}}<main id=\"app\">{{content}}</main></body></html>");
    }

    public void Dispose()
    {
        if (Directory.Exists(_siteRoot))
        {
            Directory.Delete(_siteRoot, recursive: true);
        }
    }

    private CanaryConfig NewConfig(Dictionary<string, string>? hooks = null) => new()
    {
        Site = new SiteConfig { Name = "Test Site", BaseUrl = "https://example.com" },
        Content = new ContentConfig { Root = "content" },
        Output = new OutputConfig { Dir = "docs" },
        RenderMode = RenderMode.Hybrid,
        Theme = new ThemeConfig { Shell = "shell.html" },
        Hooks = hooks ?? new Dictionary<string, string>(),
    };

    // Windows batch script that passes markdown through unchanged then
    // appends a marker paragraph -- same idiom as HookRunnerTests, just
    // exercised here through the real SiteBuilder pipeline instead of
    // HookRunner directly.
    private void WriteMarkerHook(string relativePath, string marker)
    {
        File.WriteAllText(Path.Combine(_siteRoot, relativePath),
            $"@echo off{Environment.NewLine}findstr \"^\"{Environment.NewLine}echo.{Environment.NewLine}echo {marker}{Environment.NewLine}");
    }

    [Fact]
    public void Build_HookDeclaredInHooksJson_TransformsThePage()
    {
        WriteMarkerHook("tools/breadcrumb.cmd", "BREADCRUMB");
        File.WriteAllText(Path.Combine(_siteRoot, "content", ".hooks.json"), """{ "hooks": ["breadcrumb"] }""");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\nBody text.");

        new SiteBuilder().Build(NewConfig(new Dictionary<string, string> { ["breadcrumb"] = "tools/breadcrumb.cmd" }), _siteRoot);

        var html = File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html"));
        Assert.Contains("Body text.", html);
        Assert.Contains("BREADCRUMB", html);
    }

    [Fact]
    public void Build_AutoCreatesHooksJson_ForEveryContentDirectoryAtAnyDepth()
    {
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content", "blog"));
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "blog", "post.md"), "# Post");

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        Assert.True(File.Exists(Path.Combine(_siteRoot, "content", ".hooks.json")));
        Assert.True(File.Exists(Path.Combine(_siteRoot, "content", "blog", ".hooks.json")));
    }

    [Fact]
    public void Build_HooksDoNotCascadeToSubdirectoryWithoutItsOwnDeclaration()
    {
        WriteMarkerHook("tools/breadcrumb.cmd", "BREADCRUMB");
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content", "blog"));
        File.WriteAllText(Path.Combine(_siteRoot, "content", ".hooks.json"), """{ "hooks": ["breadcrumb"] }""");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\nRoot page.");
        // Not named index.md -- ContentScanner maps this to blog/post/index.html,
        // not blog/index.html (only a directory's own index.md collapses onto
        // the directory route itself).
        File.WriteAllText(Path.Combine(_siteRoot, "content", "blog", "post.md"), "# Post\nBlog page.");

        new SiteBuilder().Build(NewConfig(new Dictionary<string, string> { ["breadcrumb"] = "tools/breadcrumb.cmd" }), _siteRoot);

        var rootHtml = File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html"));
        var blogHtml = File.ReadAllText(Path.Combine(_siteRoot, "docs", "blog", "post", "index.html"));
        Assert.Contains("BREADCRUMB", rootHtml);
        Assert.DoesNotContain("BREADCRUMB", blogHtml);
    }

    [Fact]
    public void Build_MultipleHooks_ChainInDeclaredOrder()
    {
        WriteMarkerHook("tools/a.cmd", "MARKER-A");
        WriteMarkerHook("tools/b.cmd", "MARKER-B");
        File.WriteAllText(Path.Combine(_siteRoot, "content", ".hooks.json"), """{ "hooks": ["a", "b"] }""");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home");

        new SiteBuilder().Build(NewConfig(new Dictionary<string, string> { ["a"] = "tools/a.cmd", ["b"] = "tools/b.cmd" }), _siteRoot);

        var html = File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html"));
        Assert.True(html.IndexOf("MARKER-A", StringComparison.Ordinal) < html.IndexOf("MARKER-B", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_UnknownHookNameInHooksJson_FailsTheWholeBuild()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "content", ".hooks.json"), """{ "hooks": ["nonexistent"] }""");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home");

        Assert.Throws<InvalidOperationException>(() => new SiteBuilder().Build(NewConfig(), _siteRoot));
    }

    // The actual bug this round of work set out to fix: editing a widget or
    // a hook used to be invisible to the incremental-build cache (see
    // PLAN.md's Known bugs). These two tests prove it end-to-end, through
    // the real pipeline, not just at the checksum-computation unit level.

    [Fact]
    public void Build_EditingReferencedWidget_InvalidatesThatPagesCache()
    {
        Directory.CreateDirectory(Path.Combine(_siteRoot, "widgets"));
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), "<div>v1</div>");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\n\n```greeting\n```");

        new SiteBuilder().Build(NewConfig(), _siteRoot);
        Assert.Contains("<div>v1</div>", File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html")));

        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), "<div>v2</div>");
        var summary = new SiteBuilder().Build(NewConfig(), _siteRoot);

        Assert.Equal(1, summary.PagesRendered);
        Assert.Contains("<div>v2</div>", File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html")));
    }

    [Fact]
    public void Build_EditingWidgetStylesheet_InvalidatesEveryPagesCache()
    {
        // Widget CSS is now discovered/checksummed the same way as widget
        // HTML/JS (see the CSS-extraction-from-framework.css fix) -- this
        // proves editing a widget's .css alone (page markup unchanged)
        // still invalidates the cache, not just template/behavior edits.
        Directory.CreateDirectory(Path.Combine(_siteRoot, "widgets"));
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), "<div>v1</div>");
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.css"), ".greeting { color: red; }");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\n\n```greeting\n```");

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.css"), ".greeting { color: blue; }");
        var summary = new SiteBuilder().Build(NewConfig(), _siteRoot);

        Assert.Equal(1, summary.PagesRendered);
    }

    [Fact]
    public void Build_EditingHookScript_InvalidatesThatPagesCache()
    {
        WriteMarkerHook("tools/breadcrumb.cmd", "MARKER-V1");
        File.WriteAllText(Path.Combine(_siteRoot, "content", ".hooks.json"), """{ "hooks": ["breadcrumb"] }""");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\nBody.");
        var hooks = new Dictionary<string, string> { ["breadcrumb"] = "tools/breadcrumb.cmd" };

        new SiteBuilder().Build(NewConfig(hooks), _siteRoot);
        Assert.Contains("MARKER-V1", File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html")));

        WriteMarkerHook("tools/breadcrumb.cmd", "MARKER-V2");
        var summary = new SiteBuilder().Build(NewConfig(hooks), _siteRoot);

        Assert.Equal(1, summary.PagesRendered);
        Assert.Contains("MARKER-V2", File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html")));
    }

    [Fact]
    public void Build_NothingChanged_StillReusesCachedPage()
    {
        // Guards against overcorrecting: the checksum-gating fix must not
        // make EVERY build a full re-render regardless of change.
        Directory.CreateDirectory(Path.Combine(_siteRoot, "widgets"));
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), "<div>v1</div>");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\n\n```greeting\n```");

        new SiteBuilder().Build(NewConfig(), _siteRoot);
        var summary = new SiteBuilder().Build(NewConfig(), _siteRoot);

        Assert.Equal(0, summary.PagesRendered);
        Assert.Equal(1, summary.PagesReusedUnchanged);
    }
}
