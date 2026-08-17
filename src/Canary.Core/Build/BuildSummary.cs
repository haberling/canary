namespace Canary.Core.Build;

public sealed record BuildSummary(
    int TotalRoutes,
    int PagesRendered,
    int PagesReusedUnchanged,
    string OutputRoot
);
