using Canary.Core.Config;
using Canary.Core.Init;

namespace Canary.Core.Tests.Init;

public class SiteInitializerTests : IDisposable
{
    private readonly string _root;
    private readonly string _targetDir;
    private readonly string _templatesDir;
    private readonly string _runtimeDistDir;

    public SiteInitializerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "canary-siteinitializer-tests-" + Guid.NewGuid());
        _targetDir = Path.Combine(_root, "target");
        _templatesDir = Path.Combine(_root, "templates", "default");
        _runtimeDistDir = Path.Combine(_root, "runtime", "dist");

        Directory.CreateDirectory(_targetDir);
        Directory.CreateDirectory(Path.Combine(_templatesDir, "css"));
        Directory.CreateDirectory(Path.Combine(_templatesDir, "tools"));
        Directory.CreateDirectory(Path.Combine(_runtimeDistDir, "widgets"));

        File.WriteAllText(Path.Combine(_templatesDir, "shell.html"), "<html>{{content}}</html>");
        File.WriteAllText(Path.Combine(_templatesDir, "css", "framework.css"), ":root { --bg: #fff; }");
        File.WriteAllText(Path.Combine(_templatesDir, "css", "theme.css"), ":root { --accent: #000; }");
        File.WriteAllText(Path.Combine(_templatesDir, "tools", "curtain.cs"), "// curtain passthrough tool");
        File.WriteAllText(Path.Combine(_templatesDir, "tools", "reading-time.ps1"), "# reading-time passthrough tool");

        foreach (var name in new[] { "downloads", "slideshow" })
        {
            foreach (var ext in new[] { "html", "js", "css" })
            {
                File.WriteAllText(Path.Combine(_runtimeDistDir, "widgets", $"{name}.{ext}"), $"built-in {name}.{ext}");
            }
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static InitOptions DefaultOptions(bool copyDefaultsOnInit = true, bool preferBuiltIn = false, int servePort = 6913) =>
        new("Test Site", "https://example.com", RenderMode.Hybrid, "content", "docs", 1, copyDefaultsOnInit, preferBuiltIn, servePort);

    [Fact]
    public void Initialize_FreshProject_WritesConfigThemeContentAndWidgets()
    {
        var result = SiteInitializer.Initialize(DefaultOptions(servePort: 9090), _targetDir, _templatesDir, _runtimeDistDir, force: false);

        Assert.False(result.Refused);

        var configPath = Path.Combine(_targetDir, "canary.jsonc");
        Assert.True(File.Exists(configPath));

        var config = ConfigLoader.Load(configPath);
        Assert.Equal("Test Site", config.Site.Name);
        Assert.Equal("https://example.com", config.Site.BaseUrl);
        Assert.Equal("content", config.Content.Root);
        Assert.Equal("docs", config.Output.Dir);
        Assert.Equal(RenderMode.Hybrid, config.RenderMode);
        Assert.Equal(1, config.Nav.Depth);
        Assert.Equal(9090, config.Serve.Port);
        Assert.Equal("shell.html", config.Theme.Shell);
        Assert.Equal("css/framework.css", config.Theme.Base);
        Assert.Equal("css/theme.css", config.Theme.Theme);
        Assert.True(config.Widgets.CopyDefaultsOnInit);
        Assert.False(config.Widgets.PreferBuiltIn);
        Assert.Equal(new Dictionary<string, ToolEntry>
        {
            ["reading-time"] = new ToolEntry("powershell -NoProfile -ExecutionPolicy Bypass -File tools/reading-time.ps1"),
            ["curtain"] = new ToolEntry("dotnet run tools/curtain.cs"),
        }, config.Tools);
        Assert.True(config.Initialized);

        Assert.True(File.Exists(Path.Combine(_targetDir, "shell.html")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "css", "framework.css")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "css", "theme.css")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "tools", "curtain.cs")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "tools", "reading-time.ps1")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "content", "index.md")));
        Assert.Contains("Test Site", File.ReadAllText(Path.Combine(_targetDir, "content", "index.md")));

        foreach (var name in new[] { "downloads", "slideshow" })
        {
            foreach (var ext in new[] { "html", "js", "css" })
            {
                Assert.True(File.Exists(Path.Combine(_targetDir, "widgets", $"{name}.{ext}")));
            }
        }

        Assert.True(Directory.Exists(Path.Combine(_targetDir, "root-copy")));
    }

    // A re-scaffold (--force) against a project where root-copy/ already
    // has real content in it (a CNAME, say) must never touch that content
    // -- Directory.CreateDirectory is a no-op on an existing directory, so
    // there's no code path here that could clear or overwrite it, but
    // that's exactly the kind of thing worth locking in with a test rather
    // than trusting by inspection.
    [Fact]
    public void Initialize_Force_DoesNotTouchExistingRootCopyContents()
    {
        SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: false);
        File.WriteAllText(Path.Combine(_targetDir, "root-copy", "CNAME"), "example.com");

        SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: true);

        Assert.Equal("example.com", File.ReadAllText(Path.Combine(_targetDir, "root-copy", "CNAME")));
    }

    // The scaffolded canary.jsonc also shows, commented out, the
    // precompiled-tool form for the same "curtain" key -- confirms the
    // JSONC comment is actually skipped by the parser rather than somehow
    // producing a second/duplicate registry entry.
    [Fact]
    public void Initialize_ScaffoldedConfig_CommentedPrecompileExampleDoesNotAddExtraEntry()
    {
        SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: false);

        var configPath = Path.Combine(_targetDir, "canary.jsonc");
        var rawText = File.ReadAllText(configPath);
        Assert.Contains("canary tools build curtain", rawText);
        Assert.Contains("// \"curtain\": { \"command\"", rawText);

        var config = ConfigLoader.Load(configPath);
        Assert.Equal(2, config.Tools.Count);
        Assert.Equal("dotnet run tools/curtain.cs", config.Tools["curtain"].Command);
        Assert.Null(config.Tools["curtain"].Source);
        Assert.Equal("powershell -NoProfile -ExecutionPolicy Bypass -File tools/reading-time.ps1", config.Tools["reading-time"].Command);
    }

    // output.dir must NOT be actively ignored by default -- it defaults to
    // "docs" specifically to match GitHub Pages' "serve from /docs on
    // main" convention, which requires that directory to be committed.
    // The scaffolded .gitignore shows it as a commented-out option for
    // sites that deploy a different way, never as a live rule.
    [Fact]
    public void Initialize_FreshProject_WritesGitignoreCoveringToolsBin_WithOutputDirCommentedOutOnly()
    {
        SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: false);

        var gitignorePath = Path.Combine(_targetDir, ".gitignore");
        Assert.True(File.Exists(gitignorePath));
        var lines = File.ReadAllLines(gitignorePath);
        Assert.Contains("tools/bin/", lines);
        Assert.DoesNotContain("docs/", lines);
        Assert.Contains("# docs/", lines);
    }

    [Fact]
    public void Initialize_Force_NeverOverwritesExistingGitignore()
    {
        SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: false);

        var gitignorePath = Path.Combine(_targetDir, ".gitignore");
        File.WriteAllText(gitignorePath, "my-custom-entry/\n");

        SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: true);

        Assert.Equal("my-custom-entry/\n", File.ReadAllText(gitignorePath));
    }

    [Fact]
    public void Initialize_CanaryJsonAlreadyExists_RefusesWithoutForce()
    {
        File.WriteAllText(Path.Combine(_targetDir, "canary.jsonc"), "{ \"not\": \"a real config\" }");

        var result = SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: false);

        Assert.True(result.Refused);
        Assert.NotNull(result.RefusalMessage);
        Assert.False(File.Exists(Path.Combine(_targetDir, "shell.html")));
        Assert.False(Directory.Exists(Path.Combine(_targetDir, "content")));
    }

    [Fact]
    public void Initialize_Force_OverwritesConfigThemeAndBothScaffoldTools_ButNotExistingContentIndex()
    {
        SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: false);

        var indexPath = Path.Combine(_targetDir, "content", "index.md");
        File.WriteAllText(indexPath, "# My real, hand-written content");

        var curtainToolPath = Path.Combine(_targetDir, "tools", "curtain.cs");
        File.WriteAllText(curtainToolPath, "// my customized tool");
        var readingTimeToolPath = Path.Combine(_targetDir, "tools", "reading-time.ps1");
        File.WriteAllText(readingTimeToolPath, "# my customized tool");

        File.WriteAllText(Path.Combine(_templatesDir, "css", "framework.css"), ":root { --bg: #111; }");

        var result = SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: true);

        Assert.False(result.Refused);
        Assert.Equal(":root { --bg: #111; }", File.ReadAllText(Path.Combine(_targetDir, "css", "framework.css")));
        Assert.Equal("// curtain passthrough tool", File.ReadAllText(curtainToolPath));
        Assert.Equal("# reading-time passthrough tool", File.ReadAllText(readingTimeToolPath));
        Assert.Equal("# My real, hand-written content", File.ReadAllText(indexPath));
    }

    [Fact]
    public void Initialize_CopyDefaultsOnInit_AlwaysOverwritesLocallyCustomizedWidgets()
    {
        SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: false);

        var downloadsHtml = Path.Combine(_targetDir, "widgets", "downloads.html");
        File.WriteAllText(downloadsHtml, "my customized downloads widget");

        SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: true);

        Assert.Equal("built-in downloads.html", File.ReadAllText(downloadsHtml));
    }

    [Fact]
    public void Initialize_CopyDefaultsOnInitFalse_SkipsWidgets()
    {
        var result = SiteInitializer.Initialize(DefaultOptions(copyDefaultsOnInit: false), _targetDir, _templatesDir, _runtimeDistDir, force: false);

        Assert.False(result.Refused);
        Assert.False(Directory.Exists(Path.Combine(_targetDir, "widgets")));
    }

    [Fact]
    public void Initialize_MissingTemplatesAndRuntimeDist_DegradesGracefully()
    {
        var result = SiteInitializer.Initialize(DefaultOptions(), _targetDir, templatesDir: null, runtimeDistDir: null, force: false);

        Assert.False(result.Refused);
        Assert.True(File.Exists(Path.Combine(_targetDir, "canary.jsonc")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "content", "index.md")));
        Assert.False(File.Exists(Path.Combine(_targetDir, "shell.html")));
        Assert.False(File.Exists(Path.Combine(_targetDir, "tools", "curtain.cs")));
        Assert.False(File.Exists(Path.Combine(_targetDir, "tools", "reading-time.ps1")));
        Assert.False(Directory.Exists(Path.Combine(_targetDir, "widgets")));
        Assert.NotEmpty(result.Warnings);
    }
}
