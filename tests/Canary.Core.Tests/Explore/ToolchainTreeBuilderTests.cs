using Canary.Core.Explore;
using Canary.Core.Toolchain;

namespace Canary.Core.Tests.Explore;

public class ToolchainTreeBuilderTests : IDisposable
{
    private readonly string _contentRoot;

    public ToolchainTreeBuilderTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), "canary-toolchaintree-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_contentRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    private void WriteFile(string relativePath, string content = "content")
    {
        var full = Path.Combine(_contentRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [Fact]
    public void Build_ReturnsNull_WhenContentRootMissing()
    {
        Directory.Delete(_contentRoot);
        Assert.Null(ToolchainTreeBuilder.Build(_contentRoot));
    }

    [Fact]
    public void Build_ReturnsNull_WhenNoMarkdownAnywhere()
    {
        Directory.CreateDirectory(Path.Combine(_contentRoot, "empty-dir"));
        Assert.Null(ToolchainTreeBuilder.Build(_contentRoot));
    }

    [Fact]
    public void Build_RootNode_ShowsNoTools_WhenToolchainFileEmptyOrMissing()
    {
        WriteFile("index.md");

        var root = ToolchainTreeBuilder.Build(_contentRoot)!;

        Assert.Contains("(no tools)", root.Label);
    }

    [Fact]
    public void Build_RootNode_ShowsResolvedToolsFromToolchainFile()
    {
        WriteFile("index.md");
        File.WriteAllText(Path.Combine(_contentRoot, ".toolchain.json"), """{ "tools": ["curtain", "reading-time"] }""");

        var root = ToolchainTreeBuilder.Build(_contentRoot)!;

        Assert.Contains("[curtain, reading-time]", root.Label);
    }

    [Fact]
    public void Build_SurfacesDirectoryBeyondNavDepth()
    {
        // Nav.Depth defaults to 1 -- this directory sits several levels
        // deeper than that. Unlike NavTreeBuilder's curated tree, the
        // toolchain tree must still surface it, per plan-0-2-0.md's "Two
        // distinct trees, not one".
        WriteFile("blog/archive/2020/deep-post.md");
        File.WriteAllText(
            Path.Combine(_contentRoot, "blog", "archive", "2020", ".toolchain.json"),
            """{ "tools": ["clear-metadata"] }""");

        var root = ToolchainTreeBuilder.Build(_contentRoot)!;

        var blog = Assert.Single(root.Children);
        var archive = Assert.Single(blog.Children);
        var year = Assert.Single(archive.Children);
        Assert.Contains("[clear-metadata]", year.Label);
    }

    [Fact]
    public void Build_PrunesDirectoriesWithNoMarkdownAnywhereUnderThem()
    {
        WriteFile("games/tesselate.md");
        Directory.CreateDirectory(Path.Combine(_contentRoot, "assets", "images"));

        var root = ToolchainTreeBuilder.Build(_contentRoot)!;

        Assert.DoesNotContain(root.Children, c => c.Label.StartsWith("assets/"));
    }

    [Fact]
    public void Build_DoesNotWriteAnyToolchainFile()
    {
        // Explicitly read-only -- see the "keep the explorer read-only and
        // simple" leaning in plan-0-2-0.md.
        WriteFile("index.md");

        ToolchainTreeBuilder.Build(_contentRoot);

        Assert.False(File.Exists(Path.Combine(_contentRoot, ToolchainOverrideFile.FileName)));
    }
}
