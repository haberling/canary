using Canary.Core.Manifest;

namespace Canary.Core.Explore;

// Wraps ManifestBuilder's own curated nav tree (SiteManifest.Nav) for
// `canary explore nav` -- capped at config.Nav.Depth, entries can be hidden
// via .nav.json, exactly what a site visitor's nav menu shows. See
// plan-0-2-0.md's "Two distinct trees, not one"; ToolchainTreeBuilder is the
// unrelated sibling that walks the filesystem directly instead.
public static class NavTreeBuilder
{
    public static List<ExploreNode> Build(IEnumerable<NavItem> items) =>
        items.Select(BuildNode).ToList();

    private static ExploreNode BuildNode(NavItem item)
    {
        // A dropdown-only node (a folder with children but no landing page,
        // Path == null) must read visibly differently from a real link --
        // plan-0-2-0.md's "Interactive behavior" section calls out that
        // these can't look identical.
        var label = item.Path is null
            ? $"{item.Title}  (dropdown only)"
            : $"{item.Title}  -> /{item.Path}";

        return new ExploreNode
        {
            Label = label,
            Children = item.Children?.Select(BuildNode).ToList() ?? [],
        };
    }
}
