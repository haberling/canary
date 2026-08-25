using Canary.Core.Config;

namespace Canary.Core.Tests.Config;

// Exercises ToolEntryJsonConverter (Canary.Core/Json/CanaryJsonContext.cs)
// through ConfigLoader, the same public entry point every other config
// test uses -- not JsonSerializer directly, since the converter is only
// ever reached via CanaryJsonContext's source-generated metadata.
public class ToolEntryJsonConverterTests
{
    private const string ConfigPrefix = """
        {
          "site": { "name": "Test Site", "baseUrl": "https://example.com" },
          "content": { "root": "content" },
        """;

    [Fact]
    public void Tools_BareStringEntry_ParsesToToolEntryWithNullSource()
    {
        var json = ConfigPrefix + """
              "tools": { "reading-time": "powershell -File tools/reading-time.ps1" }
            }
            """;

        var config = ConfigLoader.LoadFromJson(json);

        Assert.Equal(new ToolEntry("powershell -File tools/reading-time.ps1"), config.Tools["reading-time"]);
        Assert.Null(config.Tools["reading-time"].Source);
    }

    [Fact]
    public void Tools_ObjectEntry_ParsesCommandAndSource()
    {
        var json = ConfigPrefix + """
              "tools": { "curtain": { "command": "tools/bin/curtain.exe", "source": "tools/curtain.cs" } }
            }
            """;

        var config = ConfigLoader.LoadFromJson(json);

        Assert.Equal(new ToolEntry("tools/bin/curtain.exe", "tools/curtain.cs"), config.Tools["curtain"]);
    }

    [Fact]
    public void Tools_ObjectEntryMissingCommand_ThrowsCanaryConfigException()
    {
        var json = ConfigPrefix + """
              "tools": { "curtain": { "source": "tools/curtain.cs" } }
            }
            """;

        Assert.Throws<CanaryConfigException>(() => ConfigLoader.LoadFromJson(json));
    }

    [Fact]
    public void Tools_MixedStringAndObjectEntries_BothParseCorrectly()
    {
        var json = ConfigPrefix + """
              "tools": {
                "reading-time": "powershell -File tools/reading-time.ps1",
                "curtain": { "command": "tools/bin/curtain.exe", "source": "tools/curtain.cs" }
              }
            }
            """;

        var config = ConfigLoader.LoadFromJson(json);

        Assert.Equal(2, config.Tools.Count);
        Assert.Null(config.Tools["reading-time"].Source);
        Assert.Equal("tools/curtain.cs", config.Tools["curtain"].Source);
    }
}
