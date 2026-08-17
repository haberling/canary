namespace Canary.Core.Templating;

// Content authors write asset/link paths relative to the site root (e.g.
// "content/games/images/x.png") -- which only resolves correctly if made
// root-relative. See Canary.Core.Markdown.MarkdownRenderer, the original
// home of this logic for ordinary markdown links/images (prerendering to
// real nested paths broke the old pure-SPA assumption that every page
// rendered at "/", so a page-relative path resolved one level too deep;
// leading "/" fixes that regardless of nesting depth). Shared here so
// YamlParser's "!url" tag (see PLAN.md's widget-controversy notes) uses the
// exact same rule -- one implementation, not two copies that can drift.
public static class UrlResolver
{
    public static string Resolve(string url)
    {
        if (url.Length == 0) return url;
        if (url.StartsWith('/') || url.StartsWith('#')) return url;
        if (url.Contains("://") || url.StartsWith("mailto:") || url.StartsWith("tel:")) return url;
        return "/" + url;
    }
}
