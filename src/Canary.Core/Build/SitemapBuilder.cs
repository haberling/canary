namespace Canary.Core.Build;

// Only called for hybrid/static (see SiteBuilder.BuildPrerendered) -- spa
// mode has no real per-route files to point a crawler at (see PLAN.md's
// Render modes section: spa is "not crawlable" by design), so it gets no
// sitemap at all rather than one listing URLs that 404 without a hash.
public static class SitemapBuilder
{
    public static string Build(string baseUrl, IReadOnlyList<ContentRoute> routes)
    {
        var urls = routes
            .Select(r => r.RoutePath)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => $"  <url><loc>{EscapeXml(BuildUrl(baseUrl, p))}</loc></url>");

        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n"
            + string.Join('\n', urls) + "\n"
            + "</urlset>\n";
    }

    // Every prerendered route (root included) maps to its own directory's
    // index.html (see ContentScanner), so canonical URLs are directory-style
    // with a trailing slash, not a bare filename.
    internal static string BuildUrl(string baseUrl, string routePath)
    {
        var trimmedBase = baseUrl.TrimEnd('/');
        return routePath == "/" ? $"{trimmedBase}/" : $"{trimmedBase}/{routePath}/";
    }

    private static string EscapeXml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
