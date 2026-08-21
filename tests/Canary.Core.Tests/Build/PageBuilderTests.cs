using Canary.Core.Build;
using Canary.Core.Markdown;

namespace Canary.Core.Tests.Build;

public class PageBuilderTests : IDisposable
{
    private const string ShellTemplate =
        "<html><head><title>{{title}}</title></head>" +
        "<body><header>{{siteName}}</header><main id=\"app\">{{content}}</main></body></html>";

    private readonly string _dir;

    public PageBuilderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "canary-pagebuilder-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static PageBuilder NewBuilder() =>
        new(new MarkdownRenderer(new Dictionary<string, IWidgetRenderer>()));

    [Fact]
    public void BuildPage_FirstBuild_WritesContent()
    {
        var source = Path.Combine(_dir, "page.md");
        var output = Path.Combine(_dir, "out", "index.html");
        File.WriteAllText(source, "# Hello\nBody text.");

        var result = NewBuilder().BuildPage(source, output, ShellTemplate, "My Site");

        Assert.Equal(PageWriteOutcome.Written, result.Outcome);
        var html = File.ReadAllText(output);
        Assert.Contains("<h1>Hello</h1>", html);
        Assert.Contains("Body text.", html);
        Assert.Contains("My Site", html);
    }

    [Fact]
    public void BuildPage_ChangedSource_ReRendersAndRewrites()
    {
        var source = Path.Combine(_dir, "page.md");
        var output = Path.Combine(_dir, "out", "index.html");
        File.WriteAllText(source, "# Hello\nOriginal body.");
        NewBuilder().BuildPage(source, output, ShellTemplate, "My Site");

        File.WriteAllText(source, "# Hello\nChanged body.");
        var result = NewBuilder().BuildPage(source, output, ShellTemplate, "My Site");

        Assert.Equal(PageWriteOutcome.Written, result.Outcome);
        var html = File.ReadAllText(output);
        Assert.Contains("Changed body.", html);
        Assert.DoesNotContain("Original body.", html);
    }

    [Fact]
    public void BuildPage_ChromeInputChanges_AlwaysReRenders()
    {
        // Every build fully re-renders now (no cache) -- a chrome-only
        // input change (site name) still produces a different file and is
        // reported as Written, same as a content change would be.
        var source = Path.Combine(_dir, "page.md");
        var output = Path.Combine(_dir, "out", "index.html");
        File.WriteAllText(source, "# Hello\nBody text.");

        NewBuilder().BuildPage(source, output, ShellTemplate, "Old Site Name");
        var second = NewBuilder().BuildPage(source, output, ShellTemplate, "New Site Name");

        Assert.Equal(PageWriteOutcome.Written, second.Outcome);
        var html = File.ReadAllText(output);
        Assert.Contains("New Site Name", html);
        Assert.DoesNotContain("Old Site Name", html);
        Assert.Contains("<h1>Hello</h1>", html);
    }

    [Fact]
    public void BuildPage_NothingChanged_LeavesFileUntouchedOnDisk()
    {
        // The write-only-if-different mechanism that replaced checksum-
        // gating: since rendering is deterministic, an unchanged page
        // renders to identical bytes, so the second build must not touch
        // the file at all -- verified here via mtime, not just content
        // equality, so a spurious rewrite would actually be caught.
        var source = Path.Combine(_dir, "page.md");
        var output = Path.Combine(_dir, "out", "index.html");
        File.WriteAllText(source, "# Hello\nBody text.");

        NewBuilder().BuildPage(source, output, ShellTemplate, "My Site");
        var beforeWrite = File.GetLastWriteTimeUtc(output);

        Thread.Sleep(50); // ensure a real filesystem-observable mtime gap if a rewrite did happen
        var result = NewBuilder().BuildPage(source, output, ShellTemplate, "My Site");

        Assert.Equal(PageWriteOutcome.Unchanged, result.Outcome);
        Assert.Equal(beforeWrite, File.GetLastWriteTimeUtc(output));
    }

    [Fact]
    public void BuildPage_BuildingTwiceInARow_IsIdempotent()
    {
        // Coverage for the gap left by removing the old cache-hit tests:
        // building an unchanged site repeatedly must not double-apply a
        // transformSource (the toolchain always runs against the pristine
        // on-disk markdown, never against its own prior output), and must
        // produce byte-identical output both times.
        var source = Path.Combine(_dir, "page.md");
        var output = Path.Combine(_dir, "out", "index.html");
        File.WriteAllText(source, "# Hello\nOriginal body.");

        var applyCount = 0;
        Func<string, string> transform = s => { applyCount++; return s + "\n\nFooter."; };

        NewBuilder().BuildPage(source, output, ShellTemplate, "My Site", transformSource: transform);
        var firstHtml = File.ReadAllText(output);

        NewBuilder().BuildPage(source, output, ShellTemplate, "My Site", transformSource: transform);
        var secondHtml = File.ReadAllText(output);

        Assert.Equal(2, applyCount); // ran fresh both times, never skipped or doubled
        Assert.Equal(firstHtml, secondHtml);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(secondHtml, "Footer\\."));
    }

    [Fact]
    public void BuildPage_AppliesTransformSourceBeforeRendering()
    {
        var source = Path.Combine(_dir, "page.md");
        var output = Path.Combine(_dir, "out", "index.html");
        File.WriteAllText(source, "# Hello\nOriginal body.");

        var result = NewBuilder().BuildPage(
            source, output, ShellTemplate, "My Site",
            transformSource: s => s.Replace("Original", "Transformed"));

        Assert.Equal(PageWriteOutcome.Written, result.Outcome);
        var html = File.ReadAllText(output);
        Assert.Contains("Transformed body.", html);
        Assert.DoesNotContain("Original body.", html);
    }

    [Fact]
    public void BuildPage_HomePageTitle_EqualsSiteName_DoesNotDuplicateSiteName()
    {
        var source = Path.Combine(_dir, "page.md");
        var output = Path.Combine(_dir, "out", "index.html");
        File.WriteAllText(source, "# My Site");

        NewBuilder().BuildPage(source, output, ShellTemplate, "My Site");

        var html = File.ReadAllText(output);
        Assert.Contains("<title>My Site</title>", html);
    }
}
