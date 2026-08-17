using System.Text.Json.Serialization;

namespace Canary.Core.Manifest;

// content/<dir>/.nav.json -- controls how that directory's nav item is built.
// (No "siteRoot" field here: Canary's site root is always config.Content.Root,
// there's no marker-file search like consoland's original build-manifest.cs.)
public sealed class NavOverride
{
    [JsonPropertyName("allow")]
    public List<string>? Allow { get; set; }

    [JsonPropertyName("deny")]
    public List<string>? Deny { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("nonav")]
    public bool NoNav { get; set; }
}
