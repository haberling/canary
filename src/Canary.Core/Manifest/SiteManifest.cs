using System.Text.Json.Serialization;

namespace Canary.Core.Manifest;

public sealed class SiteManifest
{
    [JsonPropertyName("nav")]
    public List<NavItem> Nav { get; set; } = new();
}
