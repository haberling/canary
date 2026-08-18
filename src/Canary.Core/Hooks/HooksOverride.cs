using System.Text.Json.Serialization;

namespace Canary.Core.Hooks;

// content/<dir>/.hooks.json -- which registered hooks (see CanaryConfig.Hooks)
// apply to .md files directly inside that directory. Deliberately
// non-recursive: no inheritance from a parent directory's .hooks.json. See
// PLAN.md's "Content hooks" section for why.
public sealed class HooksOverride
{
    [JsonPropertyName("hooks")]
    public List<string> Hooks { get; set; } = new();
}
