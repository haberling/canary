using System.Text.Json.Serialization;

namespace Canary.Core.Manifest;

public sealed class NavItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("children")]
    public List<NavItem>? Children { get; set; }

    [JsonIgnore]
    public int Priority { get; set; }
}
