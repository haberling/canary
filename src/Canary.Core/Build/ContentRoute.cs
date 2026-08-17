namespace Canary.Core.Build;

// One content/**.md file, resolved to where it lives in both the client-side
// route space (content root's own index.md -> "/", any other <dir>/index.md
// -> "<dir>", everything else -> its relative path) and the prerendered
// output tree. Note: main.ts's contentFileFor still hardcodes "home" as of
// this writing -- that's part of the still-pending TS rewrite, see PLAN.md.
public sealed record ContentRoute(
    string SourcePath,
    string RoutePath,
    string OutputRelativePath
);
