using Canary.Core.Explore;

namespace Canary.Core.Tests.Explore;

public class TreeExplorerTests
{
    // Only the two paths that don't need a real keyboard/console are unit-
    // tested here (the empty-tree short-circuit, and the non-interactive
    // flat-print fallback's own formatting) -- the actual interactive
    // ReadKey loop is exercised manually per plan-0-2-0.md's verification
    // checklist, the same way this codebase already treats other hand-
    // rolled terminal UI.
    [Fact]
    public void Run_WithNoRoots_PrintsEmptyMessage_WithoutTouchingConsoleMode()
    {
        var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            TreeExplorer.Run([], "No content found.");
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal("No content found." + Environment.NewLine, writer.ToString());
    }

    [Fact]
    public void PrintFlat_WritesFullDepthIndentedTree_OneLinePerNode()
    {
        var roots = new List<ExploreNode>
        {
            new()
            {
                Label = "blog/  [clear-metadata]",
                Children =
                [
                    new ExploreNode { Label = "2020/  (no tools)" },
                ],
            },
            new() { Label = "games/  (no tools)" },
        };

        var writer = new StringWriter();
        var original = Console.Out;
        Console.SetOut(writer);
        try
        {
            TreeExplorer.PrintFlat(roots);
        }
        finally
        {
            Console.SetOut(original);
        }

        var expected = string.Join(Environment.NewLine,
        [
            "blog/  [clear-metadata]",
            "  2020/  (no tools)",
            "games/  (no tools)",
            "",
        ]);
        Assert.Equal(expected, writer.ToString());
    }
}
