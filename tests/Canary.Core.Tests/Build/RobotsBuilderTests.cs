using Canary.Core.Build;

namespace Canary.Core.Tests.Build;

public class RobotsBuilderTests
{
    [Fact]
    public void Build_AllowsAllAndPointsAtSitemap()
    {
        var robots = RobotsBuilder.Build("https://example.com");

        Assert.Contains("User-agent: *", robots);
        Assert.Contains("Allow: /", robots);
        Assert.Contains("Sitemap: https://example.com/sitemap.xml", robots);
    }

    [Fact]
    public void Build_BaseUrlWithTrailingSlash_DoesNotDoubleSlash()
    {
        var robots = RobotsBuilder.Build("https://example.com/");

        Assert.Contains("Sitemap: https://example.com/sitemap.xml", robots);
        Assert.DoesNotContain("//sitemap.xml", robots);
    }
}
