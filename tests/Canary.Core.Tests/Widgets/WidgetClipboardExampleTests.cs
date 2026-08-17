using Canary.Core.Widgets;

namespace Canary.Core.Tests.Widgets;

public class WidgetClipboardExampleTests : IDisposable
{
    private readonly string _dir;

    public WidgetClipboardExampleTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "canary-clipboard-example-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private string WriteTemplate(string content)
    {
        var path = Path.Combine(_dir, "widget.html");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Extract_ReturnsContentBetweenMarkers_Trimmed()
    {
        var path = WriteTemplate("""
            <!-- some other comment -->
            <!--clipboard
            ```mywidget
            title: Example
            ```
            -->
            <div>{{title}}</div>
            """);

        var example = WidgetClipboardExample.Extract(path);

        Assert.Equal("```mywidget\ntitle: Example\n```", example);
    }

    [Fact]
    public void Extract_NoClipboardBlock_ReturnsNull()
    {
        var path = WriteTemplate("<div>{{title}}</div>");

        Assert.Null(WidgetClipboardExample.Extract(path));
    }

    [Fact]
    public void Extract_UnclosedClipboardBlock_ReturnsNull()
    {
        var path = WriteTemplate("<!--clipboard\n```mywidget\n```\n<div>{{title}}</div>");

        Assert.Null(WidgetClipboardExample.Extract(path));
    }

    [Fact]
    public void Extract_DoesNotMatchTheMainDocCommentBlock()
    {
        // A widget's regular, non-clipboard doc comment must be ignored --
        // only content between "<!--clipboard" and the next "-->" counts.
        var path = WriteTemplate("""
            <!--
              Regular documentation, not a clipboard example.
            -->
            <!--clipboard
            example content
            -->
            <div></div>
            """);

        Assert.Equal("example content", WidgetClipboardExample.Extract(path));
    }
}
