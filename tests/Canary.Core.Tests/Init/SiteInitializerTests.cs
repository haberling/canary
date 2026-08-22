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
        File.WriteAllText(Path.Combine(_templatesDir, "tools", "example.cs"), "// example passthrough tool");

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

        var configPath = Path.Combine(_targetDir, "canary.json");
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
        Assert.Equal(new Dictionary<string, string> { ["example"] = "dotnet run tools/example.cs" }, config.Tools);
        Assert.True(config.Initialized);

        Assert.True(File.Exists(Path.Combine(_targetDir, "shell.html")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "css", "framework.css")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "css", "theme.css")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "tools", "example.cs")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "content", "index.md")));
        Assert.Contains("Test Site", File.ReadAllText(Path.Combine(_targetDir, "content", "index.md")));

        foreach (var name in new[] { "downloads", "slideshow" })
        {
            foreach (var ext in new[] { "html", "js", "css" })
            {
                Assert.True(File.Exists(Path.Combine(_targetDir, "widgets", $"{name}.{ext}")));
            }
        }
    }

    [Fact]
    public void Initialize_CanaryJsonAlreadyExists_RefusesWithoutForce()
    {
        File.WriteAllText(Path.Combine(_targetDir, "canary.json"), "{ \"not\": \"a real config\" }");

        var result = SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: false);

        Assert.True(result.Refused);
        Assert.NotNull(result.RefusalMessage);
        Assert.False(File.Exists(Path.Combine(_targetDir, "shell.html")));
        Assert.False(Directory.Exists(Path.Combine(_targetDir, "content")));
    }

    [Fact]
    public void Initialize_Force_OverwritesConfigThemeAndExampleTool_ButNotExistingContentIndex()
    {
        SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: false);

        var indexPath = Path.Combine(_targetDir, "content", "index.md");
        File.WriteAllText(indexPath, "# My real, hand-written content");

        var exampleToolPath = Path.Combine(_targetDir, "tools", "example.cs");
        File.WriteAllText(exampleToolPath, "// my customized tool");

        File.WriteAllText(Path.Combine(_templatesDir, "css", "framework.css"), ":root { --bg: #111; }");

        var result = SiteInitializer.Initialize(DefaultOptions(), _targetDir, _templatesDir, _runtimeDistDir, force: true);

        Assert.False(result.Refused);
        Assert.Equal(":root { --bg: #111; }", File.ReadAllText(Path.Combine(_targetDir, "css", "framework.css")));
        Assert.Equal("// example passthrough tool", File.ReadAllText(exampleToolPath));
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
        Assert.True(File.Exists(Path.Combine(_targetDir, "canary.json")));
        Assert.True(File.Exists(Path.Combine(_targetDir, "content", "index.md")));
        Assert.False(File.Exists(Path.Combine(_targetDir, "shell.html")));
        Assert.False(File.Exists(Path.Combine(_targetDir, "tools", "example.cs")));
        Assert.False(Directory.Exists(Path.Combine(_targetDir, "widgets")));
        Assert.NotEmpty(result.Warnings);
    }
}
