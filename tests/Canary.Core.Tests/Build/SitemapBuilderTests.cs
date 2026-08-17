using Canary.Core.Build;

namespace Canary.Core.Tests.Build;

public class SitemapBuilderTests
{
    [Fact]
    public void Build_RootRoute_UsesBaseUrlWithTrailingSlash()
    {
        var xml = SitemapBuilder.Build("https://example.com", [new ContentRoute("index.md", "/", "index.html")]);

        Assert.Contains("<loc>https://example.com/</loc>", xml);
    }

    [Fact]
    public void Build_NestedRoute_AppendsPathWithTrailingSlash()
    {
        var xml = SitemapBuilder.Build("https://example.com", [new ContentRoute("x.md", "characters/mabel", "characters/mabel/index.html")]);

        Assert.Contains("<loc>https://example.com/characters/mabel/</loc>", xml);
    }

    [Fact]
    public void Build_BaseUrlWithTrailingSlash_DoesNotDoubleSlash()
    {
        var xml = SitemapBuilder.Build("https://example.com/", [new ContentRoute("index.md", "/", "index.html")]);

        Assert.Contains("<loc>https://example.com/</loc>", xml);
        Assert.DoesNotContain("//</loc>", xml);
    }

    [Fact]
    public void Build_RoutesAreSortedOrdinally_RootFirst()
    {
        var routes = new[]
        {
            new ContentRoute("b.md", "synopsis", "synopsis/index.html"),
            new ContentRoute("index.md", "/", "index.html"),
            new ContentRoute("a.md", "characters", "characters/index.html"),
        };

        var xml = SitemapBuilder.Build("https://example.com", routes);

        var rootIndex = xml.IndexOf("<loc>https://example.com/</loc>");
        var charactersIndex = xml.IndexOf("<loc>https://example.com/characters/</loc>");
        var synopsisIndex = xml.IndexOf("<loc>https://example.com/synopsis/</loc>");
        Assert.True(rootIndex < charactersIndex);
        Assert.True(charactersIndex < synopsisIndex);
    }

    [Fact]
    public void Build_ProducesValidSitemapXmlns()
    {
        var xml = SitemapBuilder.Build("https://example.com", [new ContentRoute("index.md", "/", "index.html")]);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", xml);
        Assert.Contains("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">", xml);
        Assert.Contains("</urlset>", xml);
    }
}
