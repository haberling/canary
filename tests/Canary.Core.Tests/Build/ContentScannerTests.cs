using Canary.Core.Build;

namespace Canary.Core.Tests.Build;

public class ContentScannerTests : IDisposable
{
    private readonly string _contentRoot;

    public ContentScannerTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), "canary-scanner-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_contentRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    private void WriteFile(string relativePath)
    {
        var full = Path.Combine(_contentRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "# Page");
    }

    [Fact]
    public void Scan_RootIndexMd_MapsToSiteRootIndex()
    {
        WriteFile("index.md");

        var routes = ContentScanner.Scan(_contentRoot);

        var route = Assert.Single(routes);
        Assert.Equal("/", route.RoutePath);
        Assert.Equal("index.html", route.OutputRelativePath);
    }

    [Fact]
    public void Scan_HomeMd_HasNoSpecialMeaning()
    {
        // Regression guard: "index.md" replaced consoland's "home.md" as the
        // root landing-page convention (see PLAN.md) -- a file literally
        // named home.md is now just an ordinary route.
        WriteFile("home.md");

        var routes = ContentScanner.Scan(_contentRoot);

        var route = Assert.Single(routes);
        Assert.Equal("home", route.RoutePath);
        Assert.NotEqual("/", route.RoutePath);
    }

    [Fact]
    public void Scan_NestedFile_MapsToNestedDirectoryIndex()
    {
        WriteFile("games/Tesselate.md");

        var routes = ContentScanner.Scan(_contentRoot);

        var route = Assert.Single(routes);
        Assert.Equal("games/Tesselate", route.RoutePath);
        Assert.Equal("games/Tesselate/index.html", route.OutputRelativePath);
    }

    [Fact]
    public void Scan_SubdirectoryIndexMd_MapsToDirectoryItself_NotDirIndex()
    {
        // Matches ManifestBuilder's landing-page convention: content/<dir>/
        // index.md -> route "<dir>", not "<dir>/index", so nav links (which
        // ManifestBuilder derives the same way) always match a real route.
        WriteFile("games/index.md");

        var routes = ContentScanner.Scan(_contentRoot);

        var route = Assert.Single(routes);
        Assert.Equal("games", route.RoutePath);
        Assert.Equal("games/index.html", route.OutputRelativePath);
    }

}
