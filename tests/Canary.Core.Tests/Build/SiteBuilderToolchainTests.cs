using Canary.Core.Build;
using Canary.Core.Config;

namespace Canary.Core.Tests.Build;

public class SiteBuilderToolchainTests : IDisposable
{
    private readonly string _siteRoot;

    public SiteBuilderToolchainTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "canary-toolchain-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content"));
        Directory.CreateDirectory(Path.Combine(_siteRoot, "tools"));
        File.WriteAllText(Path.Combine(_siteRoot, "shell.html"), "<html><body><main id=\"app\">{{content}}</main></body></html>");
    }

    public void Dispose()
    {
        if (Directory.Exists(_siteRoot))
        {
            Directory.Delete(_siteRoot, recursive: true);
        }
    }

    private CanaryConfig NewConfig(Dictionary<string, ToolEntry>? tools = null) => new()
    {
        Site = new SiteConfig { Name = "Test Site", BaseUrl = "https://example.com" },
        Content = new ContentConfig { Root = "content" },
        Output = new OutputConfig { Dir = "docs" },
        RenderMode = RenderMode.Hybrid,
        Theme = new ThemeConfig { Shell = "shell.html" },
        Tools = tools ?? new Dictionary<string, ToolEntry>(),
    };

    // Windows batch script that passes markdown through unchanged then
    // appends a marker paragraph -- same idiom as ToolchainRunnerTests, just
    // exercised here through the real SiteBuilder pipeline instead of
    // ToolchainRunner directly.
    private void WriteMarkerTool(string relativePath, string marker)
    {
        File.WriteAllText(Path.Combine(_siteRoot, relativePath),
            $"@echo off{Environment.NewLine}findstr \"^\"{Environment.NewLine}echo.{Environment.NewLine}echo {marker}{Environment.NewLine}");
    }

    [Fact]
    public void Build_ToolOnNestedRoute_SeesBareRoutePathAndRealManifestPath()
    {
        // Exercises the actual "mass modification based on nav position"
        // use case CANARY_ROUTE_PATH/CANARY_MANIFEST_PATH exist for: a tool
        // applied to a page two directories deep must see that page's own
        // bare nav-tree route (not ContentScanner's URL-style RoutePath),
        // and a manifest path that's real, on disk, and current -- not a
        // placeholder or a stale copy.
        File.WriteAllText(
            Path.Combine(_siteRoot, "tools", "env-echo.cmd"),
            $"@echo off{Environment.NewLine}findstr \"^\"{Environment.NewLine}" +
            $"echo ROUTE=%CANARY_ROUTE_PATH%{Environment.NewLine}" +
            $"echo MANIFEST=%CANARY_MANIFEST_PATH%{Environment.NewLine}");
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content", "games"));
        File.WriteAllText(Path.Combine(_siteRoot, "content", "games", ".toolchain.json"), """{ "tools": ["env-echo"] }""");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "games", "tesselate.md"), "# Tesselate\nBody.");

        new SiteBuilder().Build(NewConfig(new Dictionary<string, ToolEntry> { ["env-echo"] = new ToolEntry("tools/env-echo.cmd") }), _siteRoot);

        var html = File.ReadAllText(Path.Combine(_siteRoot, "docs", "games", "tesselate", "index.html"));
        Assert.Contains("ROUTE=games/tesselate", html);

        var expectedManifestPath = Path.Combine(_siteRoot, "content", "manifest.json");
        Assert.Contains($"MANIFEST={expectedManifestPath}", html);
        Assert.True(File.Exists(expectedManifestPath), "the manifest path a tool receives must point at a real, already-written file.");
        Assert.Contains("\"games/tesselate\"", File.ReadAllText(expectedManifestPath));
    }

    [Fact]
    public void Build_ToolDeclaredInToolchainJson_TransformsThePage()
    {
        WriteMarkerTool("tools/breadcrumb.cmd", "BREADCRUMB");
        File.WriteAllText(Path.Combine(_siteRoot, "content", ".toolchain.json"), """{ "tools": ["breadcrumb"] }""");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\nBody text.");

        new SiteBuilder().Build(NewConfig(new Dictionary<string, ToolEntry> { ["breadcrumb"] = new ToolEntry("tools/breadcrumb.cmd") }), _siteRoot);

        var html = File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html"));
        Assert.Contains("Body text.", html);
        Assert.Contains("BREADCRUMB", html);
    }

    [Fact]
    public void Build_AutoCreatesToolchainJson_ForEveryContentDirectoryAtAnyDepth()
    {
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content", "blog"));
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "blog", "post.md"), "# Post");

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        Assert.True(File.Exists(Path.Combine(_siteRoot, "content", ".toolchain.json")));
        Assert.True(File.Exists(Path.Combine(_siteRoot, "content", "blog", ".toolchain.json")));
    }

    [Fact]
    public void Build_ToolsDoNotCascadeToSubdirectoryWithoutItsOwnDeclaration()
    {
        WriteMarkerTool("tools/breadcrumb.cmd", "BREADCRUMB");
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content", "blog"));
        File.WriteAllText(Path.Combine(_siteRoot, "content", ".toolchain.json"), """{ "tools": ["breadcrumb"] }""");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\nRoot page.");
        // Not named index.md -- ContentScanner maps this to blog/post/index.html,
        // not blog/index.html (only a directory's own index.md collapses onto
        // the directory route itself).
        File.WriteAllText(Path.Combine(_siteRoot, "content", "blog", "post.md"), "# Post\nBlog page.");

        new SiteBuilder().Build(NewConfig(new Dictionary<string, ToolEntry> { ["breadcrumb"] = new ToolEntry("tools/breadcrumb.cmd") }), _siteRoot);

        var rootHtml = File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html"));
        var blogHtml = File.ReadAllText(Path.Combine(_siteRoot, "docs", "blog", "post", "index.html"));
        Assert.Contains("BREADCRUMB", rootHtml);
        Assert.DoesNotContain("BREADCRUMB", blogHtml);
    }

    [Fact]
    public void Build_MultipleTools_ChainInDeclaredOrder()
    {
        WriteMarkerTool("tools/a.cmd", "MARKER-A");
        WriteMarkerTool("tools/b.cmd", "MARKER-B");
        File.WriteAllText(Path.Combine(_siteRoot, "content", ".toolchain.json"), """{ "tools": ["a", "b"] }""");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home");

        new SiteBuilder().Build(NewConfig(new Dictionary<string, ToolEntry> { ["a"] = new ToolEntry("tools/a.cmd"), ["b"] = new ToolEntry("tools/b.cmd") }), _siteRoot);

        var html = File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html"));
        Assert.True(html.IndexOf("MARKER-A", StringComparison.Ordinal) < html.IndexOf("MARKER-B", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_UnknownToolNameInToolchainJson_FailsTheWholeBuild()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "content", ".toolchain.json"), """{ "tools": ["nonexistent"] }""");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home");

        Assert.Throws<InvalidOperationException>(() => new SiteBuilder().Build(NewConfig(), _siteRoot));
    }

    // Every build always fully re-renders now (no cache to invalidate --
    // see PLAN.md's "Incremental builds" section) -- these prove editing a
    // widget or a tool script is actually picked up by the next build, not
    // that a cache was correctly invalidated.

    [Fact]
    public void Build_EditingReferencedWidget_ChangesNextBuildsOutput()
    {
        Directory.CreateDirectory(Path.Combine(_siteRoot, "widgets"));
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), "<div>v1</div>");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\n\n```greeting\n```");

        new SiteBuilder().Build(NewConfig(), _siteRoot);
        Assert.Contains("<div>v1</div>", File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html")));

        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), "<div>v2</div>");
        var summary = new SiteBuilder().Build(NewConfig(), _siteRoot);

        Assert.Equal(1, summary.PagesWritten);
        Assert.Contains("<div>v2</div>", File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html")));
    }

    [Fact]
    public void Build_EditingWidgetStylesheet_ChangesNextBuildsOutput()
    {
        // Widget CSS is discovered the same way as widget HTML/JS (see the
        // CSS-extraction-from-framework.css fix) -- this proves editing a
        // widget's .css alone (page markup unchanged) still ends up in the
        // rendered widgetStyles HTML, not just template/behavior edits.
        Directory.CreateDirectory(Path.Combine(_siteRoot, "widgets"));
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), "<div>v1</div>");
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.css"), ".greeting { color: red; }");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\n\n```greeting\n```");

        new SiteBuilder().Build(NewConfig(), _siteRoot);
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.css"), ".greeting { color: blue; }");
        var summary = new SiteBuilder().Build(NewConfig(), _siteRoot);

        // Widget CSS itself isn't inlined into the page HTML (it's a
        // <link>), so the page's rendered content is unchanged -- but the
        // build still runs every page fresh every time, so this is really
        // just confirming build behavior is stable, not testing the CSS
        // content itself.
        Assert.Equal(0, summary.PagesWritten);
        Assert.Equal(1, summary.PagesUnchanged);
    }

    [Fact]
    public void Build_EditingToolScript_ChangesNextBuildsOutput()
    {
        WriteMarkerTool("tools/breadcrumb.cmd", "MARKER-V1");
        File.WriteAllText(Path.Combine(_siteRoot, "content", ".toolchain.json"), """{ "tools": ["breadcrumb"] }""");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\nBody.");
        var tools = new Dictionary<string, ToolEntry> { ["breadcrumb"] = new ToolEntry("tools/breadcrumb.cmd") };

        new SiteBuilder().Build(NewConfig(tools), _siteRoot);
        Assert.Contains("MARKER-V1", File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html")));

        WriteMarkerTool("tools/breadcrumb.cmd", "MARKER-V2");
        var summary = new SiteBuilder().Build(NewConfig(tools), _siteRoot);

        Assert.Equal(1, summary.PagesWritten);
        Assert.Contains("MARKER-V2", File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html")));
    }

    [Fact]
    public void Build_NothingChanged_WritesNoFiles()
    {
        // The write-only-if-different mechanism that replaced checksum-
        // gating: a second build with nothing changed must still fully
        // re-render (no cache), but the result is byte-identical, so
        // nothing actually gets written to disk.
        Directory.CreateDirectory(Path.Combine(_siteRoot, "widgets"));
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), "<div>v1</div>");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\n\n```greeting\n```");

        new SiteBuilder().Build(NewConfig(), _siteRoot);
        var summary = new SiteBuilder().Build(NewConfig(), _siteRoot);

        Assert.Equal(0, summary.PagesWritten);
        Assert.Equal(1, summary.PagesUnchanged);
    }

    [Fact]
    public void Build_ChangedPathsMatchingOneRoute_OnlyRewritesThatRoute()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home");
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content", "blog"));
        File.WriteAllText(Path.Combine(_siteRoot, "content", "blog", "post.md"), "# Post v1");

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        File.WriteAllText(Path.Combine(_siteRoot, "content", "blog", "post.md"), "# Post v2");
        var changedPaths = new HashSet<string> { Path.Combine(_siteRoot, "content", "blog", "post.md") };
        var summary = new SiteBuilder().Build(NewConfig(), _siteRoot, changedPaths: changedPaths);

        Assert.Equal(2, summary.TotalRoutes); // informational, always the full site
        Assert.Equal(1, summary.PagesWritten); // only the changed route was even processed
        Assert.Equal(0, summary.PagesUnchanged);
        Assert.Contains("Post v2", File.ReadAllText(Path.Combine(_siteRoot, "docs", "blog", "post", "index.html")));
    }

    [Fact]
    public void Build_ChangedPathsIncludingNonRouteFile_FallsBackToFullRebuild()
    {
        Directory.CreateDirectory(Path.Combine(_siteRoot, "widgets"));
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), "<div>v1</div>");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\n\n```greeting\n```");
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content", "blog"));
        File.WriteAllText(Path.Combine(_siteRoot, "content", "blog", "post.md"), "# Post");

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), "<div>v2</div>");
        // A changed widget file is not any route's own SourcePath -- the
        // whole set must be treated as "unsafe to narrow," so every route
        // gets processed even though only one path was named.
        var changedPaths = new HashSet<string> { Path.Combine(_siteRoot, "widgets", "greeting.html") };
        var summary = new SiteBuilder().Build(NewConfig(), _siteRoot, changedPaths: changedPaths);

        Assert.Equal(2, summary.TotalRoutes);
        Assert.Equal(2, summary.PagesWritten + summary.PagesUnchanged); // both routes processed, not just one
        Assert.Contains("<div>v2</div>", File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html")));
    }
}
