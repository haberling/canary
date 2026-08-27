using Canary.Core.Build;
using Canary.Core.Config;

namespace Canary.Core.Tests.Build;

// root-copy/ is a site's escape hatch for files that need to land at the
// OUTPUT ROOT itself (GitHub Pages' CNAME, .nojekyll, a robots.txt/
// favicon.ico override, ...) -- unlike content/, whose assets always nest
// under docs/content/... See SiteBuilder.CopyRootCopyAssets's own doc
// comment for why this is a convention directory, not a config field.
public class SiteBuilderRootCopyAssetsTests : IDisposable
{
    private readonly string _siteRoot;

    public SiteBuilderRootCopyAssetsTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "canary-rootcopyassets-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content"));
        File.WriteAllText(Path.Combine(_siteRoot, "shell.html"), "<html><body><main id=\"app\">{{content}}</main></body></html>");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\nBody.");
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
    public void Build_NoRootCopyDir_DoesNotFail()
    {
        new SiteBuilder().Build(NewConfig(), _siteRoot);

        Assert.False(Directory.Exists(Path.Combine(_siteRoot, "root-copy")));
    }

    [Fact]
    public void Build_RootCopyDirFile_LandsAtOutputRootNotNestedUnderContent()
    {
        Directory.CreateDirectory(Path.Combine(_siteRoot, "root-copy"));
        File.WriteAllText(Path.Combine(_siteRoot, "root-copy", "CNAME"), "example.com");

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        Assert.Equal("example.com", File.ReadAllText(Path.Combine(_siteRoot, "docs", "CNAME")));
        Assert.False(File.Exists(Path.Combine(_siteRoot, "docs", "content", "CNAME")));
    }

    [Fact]
    public void Build_RootCopyDirIsRecursive()
    {
        Directory.CreateDirectory(Path.Combine(_siteRoot, "root-copy", "nested"));
        File.WriteAllText(Path.Combine(_siteRoot, "root-copy", "nested", "file.txt"), "hi");

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        Assert.Equal("hi", File.ReadAllText(Path.Combine(_siteRoot, "docs", "nested", "file.txt")));
    }

    [Fact]
    public void Build_RootCopyDirFile_OverridesCanaryGeneratedFileAtSamePath()
    {
        Directory.CreateDirectory(Path.Combine(_siteRoot, "root-copy"));
        File.WriteAllText(Path.Combine(_siteRoot, "root-copy", "robots.txt"), "User-agent: *\nDisallow: /");

        new SiteBuilder().Build(NewConfig(), _siteRoot);

        Assert.Equal("User-agent: *\nDisallow: /", File.ReadAllText(Path.Combine(_siteRoot, "docs", "robots.txt")));
    }
}
