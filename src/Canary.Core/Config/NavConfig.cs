namespace Canary.Core.Config;

public sealed class NavConfig
{
    // How many directory levels deep the generated nav tree recurses. 1
    // (default) is Canary's original behavior, carried over unexamined from
    // consoland: a top-level content/<dir>/ becomes a nav item, .md files
    // directly inside it become flat dropdown children, nothing deeper gets
    // discovered at all. That was a fine default for consoland's own flat
    // site, but it's a site-specific choice, not a universal one -- a site
    // with genuine multi-level structure (e.g. docs/guides/widgets.md)
    // needs more. <= 0 means unlimited depth.
    //
    // Not a Content concern: ContentScanner already finds every route at
    // any depth regardless of this setting, so every page always builds
    // and gets a real URL either way. This only controls how deep the NAV
    // MENU tree itself goes -- a page beyond nav.depth still exists and is
    // reachable by a direct link, it just isn't surfaced in the nav.
    public int Depth { get; set; } = 1;
}
