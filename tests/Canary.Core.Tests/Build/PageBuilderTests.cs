using Canary.Core.Build;
using Canary.Core.Markdown;

namespace Canary.Core.Tests.Build;

public class PageBuilderTests : IDisposable
{
    private const string ShellTemplate =
        "<html><head><title>{{title}}</title>{{checksum}}</head>" +
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
    public void BuildPage_FirstBuild_RendersContent()
    {
        var source = Path.Combine(_dir, "page.md");
        var output = Path.Combine(_dir, "out", "index.html");
        File.WriteAllText(source, "# Hello\nBody text.");

        var result = NewBuilder().BuildPage(source, output, ShellTemplate, "My Site");

        Assert.Equal(ContentRenderOutcome.Rendered, result.ContentOutcome);
        var html = File.ReadAllText(output);
        Assert.Contains("<h1>Hello</h1>", html);
        Assert.Contains("Body text.", html);
        Assert.Contains("My Site", html);
    }

    [Fact]
    public void BuildPage_UnchangedSource_ReusesContentButRestampsChrome()
    {
        var source = Path.Combine(_dir, "page.md");
        var output = Path.Combine(_dir, "out", "index.html");
        File.WriteAllText(source, "# Hello\nBody text.");

        NewBuilder().BuildPage(source, output, ShellTemplate, "Old Site Name");

        // Chrome inputs change (site name), content source does not.
        var second = NewBuilder().BuildPage(source, output, ShellTemplate, "New Site Name");

        Assert.Equal(ContentRenderOutcome.ReusedUnchanged, second.ContentOutcome);
        var html = File.ReadAllText(output);
        Assert.Contains("New Site Name", html); // chrome was re-stamped
        Assert.DoesNotContain("Old Site Name", html);
        Assert.Contains("<h1>Hello</h1>", html); // content fragment still correct
    }

    [Fact]
    public void BuildPage_ChangedSource_ReRendersContent()
    {
        var source = Path.Combine(_dir, "page.md");
        var output = Path.Combine(_dir, "out", "index.html");
        File.WriteAllText(source, "# Hello\nOriginal body.");
        NewBuilder().BuildPage(source, output, ShellTemplate, "My Site");

        File.WriteAllText(source, "# Hello\nChanged body.");
        var result = NewBuilder().BuildPage(source, output, ShellTemplate, "My Site");

        Assert.Equal(ContentRenderOutcome.Rendered, result.ContentOutcome);
        var html = File.ReadAllText(output);
        Assert.Contains("Changed body.", html);
        Assert.DoesNotContain("Original body.", html);
    }

    [Fact]
    public void BuildPage_EmbedsSourceChecksumComment()
    {
        var source = Path.Combine(_dir, "page.md");
        var output = Path.Combine(_dir, "out", "index.html");
        File.WriteAllText(source, "# Hello");

        NewBuilder().BuildPage(source, output, ShellTemplate, "My Site");

        Assert.Matches(@"<!-- source-checksum: sha256:[0-9a-f]{64} -->", File.ReadAllText(output));
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
