namespace Canary.Core.Explore;

// Single-screen, keyboard-driven tree browser shared by `canary explore
// nav` and `canary explore toolchain` -- redrawn in place each frame (full
// Console.Clear + repaint), not left to scroll console history. Built by
// hand against System.Console (ReadKey/Clear/cursor) rather than a
// third-party TUI package: this codebase already prefers owning small infra
// itself (Serve.StaticFileServer on a bare HttpListener over a web
// framework), and a TUI dependency would need its own AOT/win-x64
// compatibility proof first. See plan-0-2-0.md's "Interactive behavior".
public static class TreeExplorer
{
    // Console.ReadKey needs a real keyboard. A redirected/no-TTY stdin or
    // stdout (piped, CI, `> out.txt`) means an interactive screen can never
    // run -- it would hang forever on a key that will never arrive, or
    // throw on Console.Clear()/CursorVisible. Same condition the --clean
    // confirmation prompt and the usage-wrapping feature already have to
    // account for.
    public static bool IsInteractive => !Console.IsInputRedirected && !Console.IsOutputRedirected;

    public static void Run(IReadOnlyList<ExploreNode> roots, string emptyMessage)
    {
        if (roots.Count == 0)
        {
            Console.WriteLine(emptyMessage);
            return;
        }

        ExploreNode.AttachParents(roots);

        if (!IsInteractive)
        {
            PrintFlat(roots);
            return;
        }

        RunInteractive(roots);
    }

    // The non-interactive fallback: the whole tree, full depth, indentation
    // only, one line per node -- no expand state, nothing to key into.
    public static void PrintFlat(IReadOnlyList<ExploreNode> roots)
    {
        foreach (var root in roots)
        {
            PrintFlatNode(root, 0);
        }
    }

    private static void PrintFlatNode(ExploreNode node, int depth)
    {
        Console.WriteLine(new string(' ', depth * 2) + node.Label);
        foreach (var child in node.Children)
        {
            PrintFlatNode(child, depth + 1);
        }
    }

    private static void RunInteractive(IReadOnlyList<ExploreNode> roots)
    {
        // Nothing expanded to start -- a drill-down tool should open on the
        // flat top-level list, not dump the whole tree on the first frame.
        var expanded = new HashSet<ExploreNode>();
        var selected = 0;
        var scrollOffset = 0;

        // Canary.Core doesn't declare itself Windows-only at the project
        // level (Canary.csproj, the actual shipped exe, does via its
        // win-x64 RID) -- CursorVisible is real and safe here regardless,
        // since nothing calls RunInteractive off Windows.
#pragma warning disable CA1416
        var cursorWasVisible = Console.CursorVisible;
        Console.CursorVisible = false;
#pragma warning restore CA1416
        try
        {
            while (true)
            {
                var visible = Flatten(roots, expanded);
                if (selected >= visible.Count) selected = visible.Count - 1;
                if (selected < 0) selected = 0;

                Draw(visible, selected, expanded, ref scrollOffset);

                var key = Console.ReadKey(intercept: true);
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.K:
                        selected = Math.Max(0, selected - 1);
                        break;

                    case ConsoleKey.DownArrow:
                    case ConsoleKey.J:
                        selected = Math.Min(visible.Count - 1, selected + 1);
                        break;

                    case ConsoleKey.RightArrow:
                    case ConsoleKey.Enter:
                        {
                            var node = visible[selected].Node;
                            if (node.Children.Count > 0) expanded.Add(node);
                            break;
                        }

                    case ConsoleKey.LeftArrow:
                        {
                            var node = visible[selected].Node;
                            if (expanded.Contains(node))
                            {
                                expanded.Remove(node);
                            }
                            else if (node.Parent != null)
                            {
                                var parentIndex = visible.FindIndex(v => v.Node == node.Parent);
                                if (parentIndex >= 0) selected = parentIndex;
                            }
                            break;
                        }

                    case ConsoleKey.Q:
                    case ConsoleKey.Escape:
                        return;
                }
            }
        }
        finally
        {
            // Restore a normal, usable terminal regardless of how the loop
            // exited -- no leftover cursor-positioning weirdness, per
            // plan-0-2-0.md's verification checklist for this feature.
#pragma warning disable CA1416
            Console.CursorVisible = cursorWasVisible;
#pragma warning restore CA1416
            Console.WriteLine();
        }
    }

    private static List<(ExploreNode Node, int Depth)> Flatten(
        IReadOnlyList<ExploreNode> roots, HashSet<ExploreNode> expanded)
    {
        var result = new List<(ExploreNode, int)>();
        foreach (var root in roots)
        {
            Walk(root, 0);
        }
        return result;

        void Walk(ExploreNode node, int depth)
        {
            result.Add((node, depth));
            if (node.Children.Count > 0 && expanded.Contains(node))
            {
                foreach (var child in node.Children)
                {
                    Walk(child, depth + 1);
                }
            }
        }
    }

    private static void Draw(
        List<(ExploreNode Node, int Depth)> visible, int selected, HashSet<ExploreNode> expanded, ref int scrollOffset)
    {
        const int headerLines = 2;
        var maxRows = Math.Max(3, Console.WindowHeight - headerLines);

        if (selected < scrollOffset) scrollOffset = selected;
        if (selected >= scrollOffset + maxRows) scrollOffset = selected - maxRows + 1;

        Console.Clear();
        Console.WriteLine("canary explore -- up/down or j/k move, right/enter expand, left collapse/back, q/esc quit");
        Console.WriteLine();

        var end = Math.Min(visible.Count, scrollOffset + maxRows);
        for (var i = scrollOffset; i < end; i++)
        {
            var (node, depth) = visible[i];
            var marker = node.Children.Count == 0 ? "  " : expanded.Contains(node) ? "v " : "> ";
            var line = new string(' ', depth * 2) + marker + node.Label;

            if (i == selected)
            {
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.ForegroundColor = ConsoleColor.Black;
                Console.WriteLine(line);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine(line);
            }
        }
    }
}
