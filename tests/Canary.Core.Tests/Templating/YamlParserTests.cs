using Canary.Core.Templating;

namespace Canary.Core.Tests.Templating;

public class YamlParserTests
{
    [Fact]
    public void Parse_FlatScalarMap()
    {
        var result = (YamlMap)YamlParser.Parse("title: Hello\nname: World");

        Assert.Equal("Hello", ((YamlScalar)result.Entries["title"]).Value);
        Assert.Equal("World", ((YamlScalar)result.Entries["name"]).Value);
    }

    [Fact]
    public void Parse_UrlTag_ResolvesRelativePathToRootRelative()
    {
        // Explicit "!url" tag (real YAML tag syntax), not automatic
        // field-name detection -- an author opts a specific value in. See
        // PLAN.md's widget-controversy notes and Canary.Core.Templating.
        // UrlResolver.
        var result = (YamlMap)YamlParser.Parse("""url: !url "content/games/x.png" """);
        Assert.Equal("/content/games/x.png", ((YamlScalar)result.Entries["url"]).Value);
    }

    [Fact]
    public void Parse_UrlTag_LeavesAbsoluteUrlAlone()
    {
        var result = (YamlMap)YamlParser.Parse("""url: !url "https://example.com/x.png" """);
        Assert.Equal("https://example.com/x.png", ((YamlScalar)result.Entries["url"]).Value);
    }

    [Fact]
    public void Parse_UrlTag_WorksOnUnquotedValue()
    {
        var result = (YamlMap)YamlParser.Parse("url: !url content/x.png");
        Assert.Equal("/content/x.png", ((YamlScalar)result.Entries["url"]).Value);
    }

    [Fact]
    public void Parse_UrlTag_WorksInsideListItem()
    {
        var yaml = """
            slides:
              - src: !url "content/games/images/x.png"
                caption: "A caption"
            """;
        var result = (YamlMap)YamlParser.Parse(yaml);
        var slide = (YamlMap)((YamlList)result.Entries["slides"]).Items[0];

        Assert.Equal("/content/games/images/x.png", ((YamlScalar)slide.Entries["src"]).Value);
    }

    [Fact]
    public void Parse_WithoutUrlTag_RelativePathIsLeftAsIs()
    {
        // Confirms the tag is opt-in -- no automatic resolution just because
        // a field happens to be named "url".
        var result = (YamlMap)YamlParser.Parse("""url: "content/games/x.png" """);
        Assert.Equal("content/games/x.png", ((YamlScalar)result.Entries["url"]).Value);
    }

    [Fact]
    public void Parse_QuotedScalar_StripsQuotesAndPreservesInternalColon()
    {
        // A colon-space inside a quoted value must not be misread as a new
        // key -- this is exactly the "Wikipedia: The Pirates of Penzance"
        // shape the downloads widget uses.
        var result = (YamlMap)YamlParser.Parse("""label: "Wikipedia: The Pirates of Penzance" """);

        Assert.Equal("Wikipedia: The Pirates of Penzance", ((YamlScalar)result.Entries["label"]).Value);
    }

    [Fact]
    public void Parse_ListOfMaps()
    {
        var yaml = """
            title: Further reading
            items:
              - label: "Wikipedia"
                url: "https://example.com"
              - label: "Other"
                url: "https://example.org"
            """;

        var result = (YamlMap)YamlParser.Parse(yaml);
        var items = (YamlList)result.Entries["items"];

        Assert.Equal(2, items.Items.Count);
        var first = (YamlMap)items.Items[0];
        Assert.Equal("Wikipedia", ((YamlScalar)first.Entries["label"]).Value);
        Assert.Equal("https://example.com", ((YamlScalar)first.Entries["url"]).Value);
        var second = (YamlMap)items.Items[1];
        Assert.Equal("Other", ((YamlScalar)second.Entries["label"]).Value);
    }

    [Fact]
    public void Parse_ListItemWithBooleanFlag()
    {
        var yaml = """
            items:
              - copy: true
                label: "Install"
                command: "msiexec /i x.msi"
            """;

        var result = (YamlMap)YamlParser.Parse(yaml);
        var item = (YamlMap)((YamlList)result.Entries["items"]).Items[0];

        Assert.Equal("true", ((YamlScalar)item.Entries["copy"]).Value);
        Assert.Equal("Install", ((YamlScalar)item.Entries["label"]).Value);
        Assert.Equal("msiexec /i x.msi", ((YamlScalar)item.Entries["command"]).Value);
    }

    [Fact]
    public void Parse_MissingKey_IsAbsentFromMap()
    {
        var result = (YamlMap)YamlParser.Parse("label: Install");

        Assert.False(result.Entries.ContainsKey("url"));
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyMap()
    {
        var result = (YamlMap)YamlParser.Parse("");
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void Parse_CommentLines_AreIgnored()
    {
        var result = (YamlMap)YamlParser.Parse("# a comment\ntitle: Hello\n# another\n");
        Assert.Equal("Hello", ((YamlScalar)result.Entries["title"]).Value);
    }

    [Fact]
    public void Parse_SlideshowShape_NestedListOfMaps()
    {
        var yaml = """
            title: Original Production
            slides:
              - src: "https://example.com/1.jpg"
                caption: "First slide"
              - src: "https://example.com/2.jpg"
                caption: "Second slide"
            """;

        var result = (YamlMap)YamlParser.Parse(yaml);
        var slides = (YamlList)result.Entries["slides"];

        Assert.Equal(2, slides.Items.Count);
        var first = (YamlMap)slides.Items[0];
        Assert.Equal("https://example.com/1.jpg", ((YamlScalar)first.Entries["src"]).Value);
        Assert.Equal("First slide", ((YamlScalar)first.Entries["caption"]).Value);
    }
}
