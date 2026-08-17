namespace Canary.Core.Config;

// Config schema only, as of this writing -- neither field's actual behavior
// is wired up yet (see PLAN.md's widget-controversy notes). CopyDefaultsOnInit
// needs `canary init` to exist first (Phase 4, not started); PreferBuiltIn
// needs a precedence-flip added to Widgets.WidgetDiscovery. Deliberately
// scoped this way: adding the schema now without the behavior avoids a
// config field that silently does nothing if someone starts using it before
// either piece lands, since it simply won't be recognized/read at all yet.
public sealed class WidgetsConfig
{
    // On `canary init`, copy the built-in widgets (downloads/slideshow)
    // into the new project's own widgets/ folder as an editable starting
    // point -- rather than leaving them only inside Canary's own
    // installation, invisible until a site author goes looking for them.
    // Site-authored widgets already take precedence over built-in ones on
    // a name collision, so once copied, these local copies become the
    // active version on the very next build.
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
