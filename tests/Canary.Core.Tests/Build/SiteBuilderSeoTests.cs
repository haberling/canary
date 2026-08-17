using Canary.Core.Build;
using Canary.Core.Config;

namespace Canary.Core.Tests.Build;

public class SiteBuilderSeoTests : IDisposable
{
    private readonly string _siteRoot;

    public SiteBuilderSeoTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "canary-seo-tests-" + Guid.NewGuid());
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

    private CanaryConfig NewConfig(RenderMode mode) => new()
    {
        Site = new SiteConfig { Name = "Test Site", BaseUrl = "https://example.com" },
        Content = new ContentConfig { Root = "content" },
        Output = new OutputConfig { Dir = "docs" },
        RenderMode = mode,
        Theme = new ThemeConfig { Shell = "shell.html" },
    };

    [Theory]
    [InlineData(RenderMode.Hybrid)]
    [InlineData(RenderMode.Static)]
    public void Build_PrerenderedModes_WriteSitemapAndRobots(RenderMode mode)
    {
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home");
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content", "about"));
        File.WriteAllText(Path.Combine(_siteRoot, "content", "about", "index.md"), "# About");

        new SiteBuilder().Build(NewConfig(mode), _siteRoot);

        var sitemapPath = Path.Combine(_siteRoot, "docs", "sitemap.xml");
        var robotsPath = Path.Combine(_siteRoot, "docs", "robots.txt");
        Assert.True(File.Exists(sitemapPath));
        Assert.True(File.Exists(robotsPath));

        var sitemap = File.ReadAllText(sitemapPath);
        Assert.Contains("<loc>https://example.com/</loc>", sitemap);
        Assert.Contains("<loc>https://example.com/about/</loc>", sitemap);

        Assert.Contains("Sitemap: https://example.com/sitemap.xml", File.ReadAllText(robotsPath));
    }

    [Fact]
    public void Build_SpaMode_WritesNeitherSitemapNorRobots()
    {
        // spa has no real per-route files to point a crawler at -- see
        // PLAN.md's Render modes section ("not crawlable" by design).
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home");

        new SiteBuilder().Build(NewConfig(RenderMode.Spa), _siteRoot);

        Assert.False(File.Exists(Path.Combine(_siteRoot, "docs", "sitemap.xml")));
        Assert.False(File.Exists(Path.Combine(_siteRoot, "docs", "robots.txt")));
    }
}
