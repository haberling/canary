using Canary.Core.Templating;

namespace Canary.Core.Tests.Templating;

public class MustacheTemplateTests
{
    private static YamlValue Yaml(string source) => YamlParser.Parse(source);

    [Fact]
    public void Render_SimpleVariable()
    {
        var html = MustacheTemplate.Render("<h3>{{title}}</h3>", Yaml("title: Hello"));
        Assert.Equal("<h3>Hello</h3>", html);
    }

    [Fact]
    public void Render_Variable_IsHtmlEscaped()
    {
        var html = MustacheTemplate.Render("<p>{{text}}</p>", Yaml("""text: "<b>&AT&T" """));
        Assert.Equal("<p>&lt;b&gt;&amp;AT&amp;T</p>", html);
    }

    [Fact]
    public void Render_MissingVariable_RendersEmpty()
    {
        var html = MustacheTemplate.Render("<p>{{missing}}</p>", Yaml("title: Hello"));
        Assert.Equal("<p></p>", html);
    }

    [Fact]
    public void Render_Section_IteratesList()
    {
        var yaml = """
            items:
              - label: "One"
              - label: "Two"
            """;
        var html = MustacheTemplate.Render("<ul>{{#items}}<li>{{label}}</li>{{/items}}</ul>", Yaml(yaml));
        Assert.Equal("<ul><li>One</li><li>Two</li></ul>", html);
    }

    [Fact]
    public void Render_Section_EmptyList_RendersNothing()
    {
        var html = MustacheTemplate.Render("<ul>{{#items}}<li>{{label}}</li>{{/items}}</ul>", Yaml("items:\n"));
        Assert.Equal("<ul></ul>", html);
    }

    [Fact]
    public void Render_InvertedSection_RendersWhenFalsy()
    {
        var yaml = "items:\n  - label: Plain\n";
        var template = "{{#items}}{{^copy}}<a>{{label}}</a>{{/copy}}{{#copy}}<button></button>{{/copy}}{{/items}}";
        var html = MustacheTemplate.Render(template, Yaml(yaml));
        Assert.Equal("<a>Plain</a>", html);
    }

    [Fact]
    public void Render_InvertedSection_SkipsWhenTruthy()
    {
        var yaml = "items:\n  - label: Cmd\n    copy: true\n";
        var template = "{{#items}}{{^copy}}<a>{{label}}</a>{{/copy}}{{#copy}}<button>{{label}}</button>{{/copy}}{{/items}}";
        var html = MustacheTemplate.Render(template, Yaml(yaml));
        Assert.Equal("<button>Cmd</button>", html);
    }

    [Fact]
    public void Render_DownloadsWidgetShape_EndToEnd()
    {
        // The exact shape downloads.html uses: a list mixing plain-link
        // items and copy-command items, branching declaratively.
        var yaml = """
            title: Further reading
            items:
              - label: "Wikipedia"
                url: "https://example.com"
              - copy: true
                label: "Install"
                command: "msiexec /i x.msi"
            """;
        var template = """
            <div><h3>{{title}}</h3><ul>{{#items}}{{^copy}}<li><a href="{{url}}">{{label}}</a></li>{{/copy}}{{#copy}}<li data-command="{{command}}">{{label}}</li>{{/copy}}{{/items}}</ul></div>
            """;

        var html = MustacheTemplate.Render(template, Yaml(yaml));

        Assert.Equal(
            "<div><h3>Further reading</h3><ul>" +
            "<li><a href=\"https://example.com\">Wikipedia</a></li>" +
            "<li data-command=\"msiexec /i x.msi\">Install</li>" +
            "</ul></div>",
            html.Trim());
    }

    [Fact]
    public void Render_NestedSectionsWithSameKeyName_TrackDepthCorrectly()
    {
        // Guards ExtractSectionBody's depth-counting: a {{#x}} inside
        // another {{#x}} must not close on the FIRST {{/x}} it sees.
        var yaml = "outer:\n  inner:\n    - label: A\n";
        var template = "{{#outer}}{{#inner}}<li>{{label}}</li>{{/inner}}{{/outer}}";
        var html = MustacheTemplate.Render(template, Yaml(yaml));
        Assert.Equal("<li>A</li>", html);
    }
}
