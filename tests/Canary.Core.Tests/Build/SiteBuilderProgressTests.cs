using Canary.Core.Build;
using Canary.Core.Config;

namespace Canary.Core.Tests.Build;

// Locks in the IBuildProgress event sequence SiteBuilder.Build fires --
// what the CLI's live spinner/chain renderer (Canary.BuildProgressRenderer)
// is built on top of. Uses a recording fake rather than the real renderer
// so this stays a Canary.Core-only test with no console/terminal
// involvement.
public class SiteBuilderProgressTests : IDisposable
{
    private readonly string _siteRoot;

    public SiteBuilderProgressTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "canary-builderprogress-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_siteRoot, "content"));
        Directory.CreateDirectory(Path.Combine(_siteRoot, "tools"));
        File.WriteAllText(Path.Combine(_siteRoot, "shell.html"), "<html><body><main id=\"app\">{{content}}</main></body></html>");
    }

    public void Dispose()
    {
        if (Directory.Exists(_siteRoot))
        {
            Directory.Delete(_siteRoot, recursive: true);
        }
    }

    private CanaryConfig NewConfig(Dictionary<string, ToolEntry>? tools = null) => new()
    {
        Site = new SiteConfig { Name = "Test Site", BaseUrl = "https://example.com" },
        Content = new ContentConfig { Root = "content" },
        Output = new OutputConfig { Dir = "docs" },
        RenderMode = RenderMode.Hybrid,
        Theme = new ThemeConfig { Shell = "shell.html" },
        Tools = tools ?? new Dictionary<string, ToolEntry>(),
    };

    private sealed class RecordingProgress : IBuildProgress
    {
        public readonly List<string> Phases = [];
        public readonly List<(string Chain, int ActiveIndex)> Stages = [];
        public readonly List<(string Output, bool Written)> PagesFinished = [];

        public void PhaseStarted(string phase) => Phases.Add(phase);
        public void PhaseFinished(string phase, TimeSpan elapsed) { }
        public void RenderStageChanged(IReadOnlyList<string> chain, int activeIndex) =>
            Stages.Add((string.Join(", ", chain), activeIndex));
        public void PageFinished(string outputDisplayName, bool written) =>
            PagesFinished.Add((outputDisplayName, written));
    }

    [Fact]
    public void Build_ReportsAllSixPhasesInOrder()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\nBody.");
        var progress = new RecordingProgress();

        new SiteBuilder().Build(NewConfig(), _siteRoot, progress: progress);

        Assert.Equal(
            ["Scanning content", "Discovering widgets", "Preparing toolchain", "Writing sitemap/robots", "Rendering pages", "Copying assets"],
            progress.Phases);
    }

    [Fact]
    public void Build_PageWithNoTools_ReportsSourceThenOutputStage()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\nBody.");
        var progress = new RecordingProgress();

        new SiteBuilder().Build(NewConfig(), _siteRoot, progress: progress);

        var stage = Assert.Single(progress.Stages);
        Assert.Equal("index.md, index.html", stage.Chain);
        Assert.Equal(1, stage.ActiveIndex);

        var page = Assert.Single(progress.PagesFinished);
        Assert.Equal(("index.html", true), page);
    }

    [Fact]
    public void Build_PageWithTwoChainedTools_ReportsEachStageOnceInOrder()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "tools", "a.cmd"),
            $"@echo off{Environment.NewLine}findstr \"^\"{Environment.NewLine}");
        File.WriteAllText(Path.Combine(_siteRoot, "tools", "b.cmd"),
            $"@echo off{Environment.NewLine}findstr \"^\"{Environment.NewLine}");
        File.WriteAllText(Path.Combine(_siteRoot, "content", ".toolchain.json"), """{ "tools": ["a", "b"] }""");
        File.WriteAllText(Path.Combine(_siteRoot, "content", "index.md"), "# Home\nBody.");
        var progress = new RecordingProgress();
        var tools = new Dictionary<string, ToolEntry> { ["a"] = new ToolEntry("tools/a.cmd"), ["b"] = new ToolEntry("tools/b.cmd") };

        new SiteBuilder().Build(NewConfig(tools), _siteRoot, progress: progress);

        Assert.Equal(
        [
            ("index.md, a, b, index.html", 1),
            ("index.md, a, b, index.html", 2),
            ("index.md, a, b, index.html", 3),
        ], progress.Stages);
    }
}
