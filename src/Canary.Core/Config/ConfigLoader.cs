using System.Text.Json;
using System.Text.Json.Serialization;

namespace Canary.Core.Config;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static CanaryConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new CanaryConfigException($"Config file not found: {path}");
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException ex)
        {
            throw new CanaryConfigException($"Could not read config file: {path}", ex);
        }

        return LoadFromJson(json, path);
    }

    public static CanaryConfig LoadFromJson(string json, string? sourceDescription = null)
    {
        CanaryConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<CanaryConfig>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            var source = sourceDescription ?? "<string>";
            throw new CanaryConfigException($"Config at {source} is not valid JSON: {ex.Message}", ex);
        }

        if (config is null)
        {
            var source = sourceDescription ?? "<string>";
            throw new CanaryConfigException($"Config at {source} deserialized to nothing (was it 'null' or empty?).");
        }

        Validate(config, sourceDescription);
        return config;
    }

    private static void Validate(CanaryConfig config, string? sourceDescription)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(config.Site.Name))
        {
            errors.Add("site.name is required");
        }

        if (string.IsNullOrWhiteSpace(config.Site.BaseUrl))
        {
            errors.Add("site.baseUrl is required");
        }

        if (string.IsNullOrWhiteSpace(config.Content.Root))
        {
            errors.Add("content.root is required");
        }

        if (string.IsNullOrWhiteSpace(config.Output.Dir))
        {
            errors.Add("output.dir cannot be empty");
        }

        if (errors.Count == 0)
        {
            return;
        }

        var source = sourceDescription is null ? string.Empty : $" ({sourceDescription})";
        var message = $"Invalid Canary config{source}:{Environment.NewLine}" +
                       string.Join(Environment.NewLine, errors.Select(e => $"  - {e}"));
        throw new CanaryConfigException(message);
    }
}
