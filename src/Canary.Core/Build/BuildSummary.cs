namespace Canary.Core.Build;

// TotalRoutes is always the full site's route count, regardless of whether
// this build was a targeted single-route rebuild (see SiteBuilder's
// changedPaths parameter) -- PagesWritten/PagesUnchanged only reflect
// whatever subset of routes was actually processed this call.
public sealed record BuildSummary(
    int TotalRoutes,
    int PagesWritten,
    int PagesUnchanged,
    string OutputRoot
);
