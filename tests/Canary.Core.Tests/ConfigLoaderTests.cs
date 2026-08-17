using Canary.Core.Config;

namespace Canary.Core.Tests;

public class ConfigLoaderTests
{
    private const string ValidJson = """
        {
          "site": { "name": "Test Site", "baseUrl": "https://example.com" },
          "content": { "root": "content" },
          "output": { "dir": "docs" },
          "renderMode": "hybrid",
          "theme": { "shell": "templates/shell.html", "base": "css/framework.css", "theme": "css/theme.css" }
        }
        """;

    [Fact]
    public void LoadFromJson_ParsesAllFields()
    {
        var config = ConfigLoader.LoadFromJson(ValidJson);

        Assert.Equal("Test Site", config.Site.Name);
        Assert.Equal("https://example.com", config.Site.BaseUrl);
        Assert.Equal("content", config.Content.Root);
        Assert.Equal("docs", config.Output.Dir);
        Assert.Equal(RenderMode.Hybrid, config.RenderMode);
        Assert.Equal("templates/shell.html", config.Theme.Shell);
        Assert.Equal("css/framework.css", config.Theme.Base);
        Assert.Equal("css/theme.css", config.Theme.Theme);
    }

    [Theory]
    [InlineData("spa", RenderMode.Spa)]
    [InlineData("static", RenderMode.Static)]
    [InlineData("hybrid", RenderMode.Hybrid)]
    public void LoadFromJson_ParsesRenderModeCaseInsensitiveEnum(string value, RenderMode expected)
    {
        var json = $$"""
            {
              "site": { "name": "Test", "baseUrl": "https://example.com" },
              "content": { "root": "content" },
              "renderMode": "{{value}}"
            }
            """;

        var config = ConfigLoader.LoadFromJson(json);

        Assert.Equal(expected, config.RenderMode);
    }

    [Fact]
    public void LoadFromJson_DefaultsOutputDirToDocs()
    {
        var json = """
            {
              "site": { "name": "Test", "baseUrl": "https://example.com" },
              "content": { "root": "content" }
            }
            """;

        var config = ConfigLoader.LoadFromJson(json);

        Assert.Equal("docs", config.Output.Dir);
    }

    [Fact]
    public void LoadFromJson_MissingRequiredFields_ThrowsWithAllErrorsListed()
    {
        var json = "{}";

        var ex = Assert.Throws<CanaryConfigException>(() => ConfigLoader.LoadFromJson(json));

        Assert.Contains("site.name is required", ex.Message);
        Assert.Contains("site.baseUrl is required", ex.Message);
        Assert.Contains("content.root is required", ex.Message);
    }

    [Fact]
    public void LoadFromJson_InvalidJson_ThrowsCanaryConfigException()
    {
        var ex = Assert.Throws<CanaryConfigException>(() => ConfigLoader.LoadFromJson("not json"));

        Assert.Contains("not valid JSON", ex.Message);
    }

    [Fact]
    public void Load_MissingFile_ThrowsCanaryConfigException()
    {
        var ex = Assert.Throws<CanaryConfigException>(() => ConfigLoader.Load("nonexistent-config.json"));

        Assert.Contains("not found", ex.Message);
    }
}
