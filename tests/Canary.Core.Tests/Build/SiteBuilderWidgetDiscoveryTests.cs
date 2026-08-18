using Canary.Core.Build;
using Canary.Core.Config;

namespace Canary.Core.Tests.Build;

public class SiteBuilderWidgetDiscoveryTests : IDisposable
{
    private readonly string _siteRoot;

    public SiteBuilderWidgetDiscoveryTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "canary-widget-discovery-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content"));
        File.WriteAllText(Path.Combine(_siteRoot, "shell.html"), "<html><body><main id=\"app\">{{content}}</main></body></html>");
    }

    public void Dispose()
    {
        if (Directory.Exists(_siteRoot))
        {
            Directory.Delete(_siteRoot, recursive: true);
        }
    }

    private CanaryConfig NewConfig() => new()
    {
        Site = new SiteConfig { Name = "Test Site", BaseUrl = "https://example.com" },
        Content = new ContentConfig { Root = "content" },
        Output = new OutputConfig { Dir = "docs" },
        RenderMode = RenderMode.Hybrid,
        Theme = new ThemeConfig { Shell = "shell.html" },
    };

    [Fact]
    public void Build_SiteAuthoredWidget_DroppedInWidgetsFolder_IsAutomaticallyUsed()
    {
        // The actual promise this whole system exists for: a site author
        // writes a plain .html template (real Mustache syntax), drops it in
        // widgets/, references its fence tag in content with a YAML body --
        // no config entry, no registration, no editing Canary's own source.
        // See PLAN.md's widget-controversy notes.
        Directory.CreateDirectory(Path.Combine(_siteRoot, "widgets"));
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), """
            <div class="my-custom-widget">{{title}}: {{name}}</div>
            """);
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), """
            # Home

            ```greeting
            title: Hello
            name: world
            ```
            """);

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        var html = File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html"));
        Assert.Contains("<div class=\"my-custom-widget\">Hello: world</div>", html);
    }

    [Fact]
    public void Build_SiteAuthoredWidgetBehaviorScript_IsCopiedAndReferenced()
    {
        // A widget's optional .js behavior file (shared, referenced once --
        // not inlined per instance) gets copied to output and the shell's
        // {{widgetScripts}} placeholder references it, but only if the
        // widget is actually used somewhere -- see PLAN.md's
        // widget-controversy notes.
        var shellWithScripts = "<html><body><main id=\"app\">{{content}}</main>{{widgetScripts}}</body></html>";
        File.WriteAllText(Path.Combine(_siteRoot, "shell.html"), shellWithScripts);

        Directory.CreateDirectory(Path.Combine(_siteRoot, "widgets"));
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), "<div>{{name}}</div>");
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.js"), "console.log('greeting loaded');");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), """
            # Home

            ```greeting
            name: world
            ```
            """);

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        var html = File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html"));
        Assert.Contains("<script src=\"/js/widgets/greeting.js\" defer></script>", html);

        var copiedScript = Path.Combine(_siteRoot, "docs", "js", "widgets", "greeting.js");
        Assert.True(File.Exists(copiedScript));
        Assert.Equal("console.log('greeting loaded');", File.ReadAllText(copiedScript));
    }

    [Fact]
    public void Build_SiteAuthoredWidgetStylesheet_IsCopiedAndReferenced()
    {
        // A widget's optional .css file is discovered and shipped exactly
        // like its .js sibling -- built-in and site-authored widgets get
        // styling the same way, no special-casing. See PLAN.md's Widget
        // system section: this didn't used to be true (built-in widget CSS
        // was baked directly into framework.css, so a site-authored widget
        // had nowhere to put its own styling).
        var shellWithStyles = "<html><head>{{widgetStyles}}</head><body><main id=\"app\">{{content}}</main></body></html>";
        File.WriteAllText(Path.Combine(_siteRoot, "shell.html"), shellWithStyles);

        Directory.CreateDirectory(Path.Combine(_siteRoot, "widgets"));
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.html"), "<div>{{name}}</div>");
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "greeting.css"), ".greeting { color: red; }");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), """
            # Home

            ```greeting
            name: world
            ```
            """);

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        var html = File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html"));
        Assert.Contains("<link rel=\"stylesheet\" href=\"/css/widgets/greeting.css\">", html);

        var copiedStylesheet = Path.Combine(_siteRoot, "docs", "css", "widgets", "greeting.css");
        Assert.True(File.Exists(copiedStylesheet));
        Assert.Equal(".greeting { color: red; }", File.ReadAllText(copiedStylesheet));
    }

    [Fact]
    public void Build_WidgetUrlTag_ResolvesToRootRelativePath_ThroughRealBuildPipeline()
    {
        // End-to-end version of the YamlParserTests unit tests: a widget's
        // relative "!url"-tagged value comes out root-relative in the
        // ACTUAL generated page, not just in an isolated parser test. See
        // PLAN.md's widget-controversy notes for why this matters --
        // without the tag, a page nested under a subdirectory would embed
        // the raw relative path and 404 in a browser.
        Directory.CreateDirectory(Path.Combine(_siteRoot, "widgets"));
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "pic.html"), "<img src=\"{{src}}\">");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), """
            # Home

            ```pic
            src: !url "content/games/x.png"
            ```
            """);

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        var html = File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html"));
        Assert.Contains("<img src=\"/content/games/x.png\">", html);
    }

    [Fact]
    public void Build_NoMatchingWidgetFile_FallsBackToPlainCodeBlock()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), """
            # Home

            ```nonexistent-widget
            some text
            ```
            """);

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        var html = File.ReadAllText(Path.Combine(_siteRoot, "docs", "index.html"));
        Assert.Contains("<pre><code>some text</code></pre>", html);
    }
}
