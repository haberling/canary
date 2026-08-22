namespace Canary.Core.Config;

// CopyDefaultsOnInit is wired up as of `canary init` (see
// Canary.Core.Init.SiteInitializer). PreferBuiltIn is still schema only --
// `canary init` reads/writes its value, but nothing acts on it yet; it
// needs a precedence-flip added to Widgets.WidgetDiscovery (today:
// site-authored unconditionally wins regardless of this setting).
public sealed class WidgetsConfig
{
    // On `canary init`, copy the built-in widgets (downloads/slideshow)
    // into the new project's own widgets/ folder as an editable starting
    // point -- rather than leaving them only inside Canary's own
    // installation, invisible until a site author goes looking for them.
    // Site-authored widgets already take precedence over built-in ones on
    // a name collision, so once copied, these local copies become the
    // active version on the very next build. `canary init` always
    // overwrites these files on every run, not just a fresh project.
    public bool CopyDefaultsOnInit { get; set; } = true;

    // Single site-wide switch (not per-widget): when true, ignore a
    // project's own widgets/downloads.html /slideshow.html even if present,
    // and use Canary's built-in versions instead. The escape hatch for
    // CopyDefaultsOnInit's tradeoff -- a project with its own copies is
    // normally frozen at whatever Canary shipped at init time (local always
    // wins on a name collision); this opts back into always tracking
    // Canary's current built-in behavior without deleting local files.
    public bool PreferBuiltIn { get; set; }
}
