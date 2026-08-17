using Canary.Core.Widgets;

namespace Canary.Core.Tests.Widgets;

public class WidgetDiscoveryTests : IDisposable
{
    private readonly string _siteRoot;
    private readonly string _runtimeDistDir;

    public WidgetDiscoveryTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "canary-widget-discovery-" + Guid.NewGuid());
        _siteRoot = Path.Combine(root, "site");
        _runtimeDistDir = Path.Combine(root, "runtime-dist");
        Directory.CreateDirectory(Path.Combine(_siteRoot, "widgets"));
        Directory.CreateDirectory(Path.Combine(_runtimeDistDir, "widgets"));
    }

    public void Dispose()
    {
        var root = Path.GetDirectoryName(_siteRoot)!;
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Discover_FindsBuiltInWidget()
    {
        File.WriteAllText(Path.Combine(_runtimeDistDir, "widgets", "downloads.html"), "<div></div>");

        var found = WidgetDiscovery.Discover(_siteRoot, _runtimeDistDir, "*.html");

        Assert.True(found.ContainsKey("downloads"));
    }

    [Fact]
    public void Discover_FindsSiteAuthoredWidget()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "custom.html"), "<div></div>");

        var found = WidgetDiscovery.Discover(_siteRoot, _runtimeDistDir, "*.html");

        Assert.True(found.ContainsKey("custom"));
    }

    [Fact]
    public void Discover_SiteAuthoredWidget_TakesPrecedenceOverBuiltInOnNameCollision()
    {
        File.WriteAllText(Path.Combine(_runtimeDistDir, "widgets", "downloads.html"), "<div>built-in</div>");
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "downloads.html"), "<div>site override</div>");

        var found = WidgetDiscovery.Discover(_siteRoot, _runtimeDistDir, "*.html");

        Assert.Contains("site override", File.ReadAllText(found["downloads"]));
    }

    [Fact]
    public void Discover_NullRuntimeDistDir_StillFindsSiteAuthoredWidgets()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "custom.html"), "<div></div>");

        var found = WidgetDiscovery.Discover(_siteRoot, runtimeDistDir: null, "*.html");

        Assert.True(found.ContainsKey("custom"));
    }

    [Fact]
    public void Discover_IsCaseInsensitiveByName()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "widgets", "MyWidget.html"), "<div></div>");

        var found = WidgetDiscovery.Discover(_siteRoot, _runtimeDistDir, "*.html");

        Assert.True(found.ContainsKey("mywidget"));
    }
}
