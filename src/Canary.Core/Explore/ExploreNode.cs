namespace Canary.Core.Explore;

// Shared shape for both trees `canary explore` walks (nav and toolchain) --
// deliberately just a label plus children, not two tree types wearing one
// interface, since the two builders draw from unrelated data models (see
// plan-0-2-0.md's "Two distinct trees, not one"). All display formatting
// (tool lists, dropdown-only markers, etc.) happens in each builder before
// a node is even constructed; TreeExplorer only ever prints Label verbatim.
public sealed class ExploreNode
{
    public required string Label { get; init; }
    public List<ExploreNode> Children { get; init; } = [];

    // Set by AttachParents, not by either builder -- lets TreeExplorer walk
    // up to a parent (Left arrow on an already-collapsed node/leaf) without
    // either tree builder needing to know that's a thing a consumer wants.
    public ExploreNode? Parent { get; private set; }

    public static void AttachParents(IEnumerable<ExploreNode> roots)
    {
        foreach (var root in roots)
        {
            AttachParents(root);
        }
    }

    private static void AttachParents(ExploreNode node)
    {
        foreach (var child in node.Children)
        {
            child.Parent = node;
            AttachParents(child);
        }
    }
}
