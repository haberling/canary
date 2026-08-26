using Canary.Core.Toolchain;

namespace Canary.Core.Explore;

// Walks every content directory recursively for `canary explore toolchain`,
// unbounded by config.Nav.Depth -- .toolchain.json applies "at any depth the
// content tree reaches" (see ToolchainOverrideFile's own doc comment), so
// this deliberately does not stop where NavTreeBuilder's curated tree would;
// the whole point is surfacing tool assignment at folder levels nav
// wouldn't show at all. Read-only: resolves whatever .toolchain.json is
// already on disk (or nothing, if a build has never run) and never writes
// or backfills one -- see plan-0-2-0.md's "keep the explorer read-only and
// simple" leaning.
public static class ToolchainTreeBuilder
{
    // Null when contentRoot doesn't exist, or exists but has no markdown
    // anywhere under it -- nothing to show either way.
    public static ExploreNode? Build(string contentRoot)
    {
        return Directory.Exists(contentRoot) ? BuildNode(contentRoot) : null;
    }

    private static ExploreNode? BuildNode(string dir)
    {
        var hasMarkdown = Directory.GetFiles(dir, "*.md", SearchOption.TopDirectoryOnly).Length > 0;

        var children = Directory.GetDirectories(dir)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(BuildNode)
            .Where(n => n != null)
            .Select(n => n!)
            .ToList();

        // A directory with no markdown of its own and no descendant that
        // has any is an empty branch -- prune it rather than show a folder
        // nobody would ever click into.
        if (!hasMarkdown && children.Count == 0) return null;

        var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var label = hasMarkdown
            ? $"{name}/  {FormatTools(ToolchainOverrideFile.ResolveForDirectory(dir))}"
            : $"{name}/";

        return new ExploreNode { Label = label, Children = children };
    }

    // A directory with markdown but an empty (or missing) tool list reads
    // as "genuinely no tools assigned", not "nothing loaded yet" -- an
    // explicit marker rather than a blank label, per plan-0-2-0.md.
    private static string FormatTools(IReadOnlyList<string> tools) =>
        tools.Count == 0 ? "(no tools)" : $"[{string.Join(", ", tools)}]";
}
