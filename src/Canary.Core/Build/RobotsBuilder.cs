namespace Canary.Core.Build;

// Only called for hybrid/static, same reasoning as SitemapBuilder -- spa
// mode is not crawlable by design (see PLAN.md's Render modes section), so
// it gets no robots.txt either rather than one advertising a sitemap that
// doesn't exist.
public static class RobotsBuilder
{
    public static string Build(string baseUrl) =>
        "User-agent: *\n"
        + "Allow: /\n"
        + "\n"
        + $"Sitemap: {baseUrl.TrimEnd('/')}/sitemap.xml\n";
}
