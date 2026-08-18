using Canary.Core.Config;

namespace Canary.Core.Build;

// Which compiled runtime JS files (from runtime/dist/, see PLAN.md's Client
// runtime packaging section) ship for each renderMode, and which one is the
// entry point -- always copied out as js/main.js so the shell template's
// <script src="/js/main.js"> tag never needs to vary per mode. A hand-written
// dependency list rather than a real bundler: matches the "no framework,
// hand-built" ethos, and runtime/ts/ is a small, fixed set of files where
// tracking imports by hand is genuinely simpler than adding a bundler.
public static class RuntimeAssetManifest
{
    public static (string EntryFile, IReadOnlyList<string> SupportFiles) For(RenderMode mode) => mode switch
    {
        // static-nav.js only imports nav.js -- no router, no content-swap
        // logic, no widget JS (widgets are self-contained inline scripts).
        RenderMode.Static => ("static-nav.js", new[] { "nav.js" }),

        // hybrid-router.js imports router.js + nav.js; never markdown.js --
        // content is always prerendered, so the browser never renders
        // markdown itself in this mode.
        RenderMode.Hybrid => ("hybrid-router.js", (IReadOnlyList<string>)["router.js", "nav.js"]),

        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown render mode."),
    };
}
