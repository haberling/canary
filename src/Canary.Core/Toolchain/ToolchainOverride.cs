using System.Text.Json.Serialization;

namespace Canary.Core.Toolchain;

// content/<dir>/.toolchain.json -- which registered tools (see CanaryConfig.Tools)
// apply to .md files directly inside that directory. Deliberately
// non-recursive: no inheritance from a parent directory's .toolchain.json. See
// PLAN.md's "Content toolchain" section for why.
public sealed class ToolchainOverride
{
    [JsonPropertyName("tools")]
    public List<string> Tools { get; set; } = new();
}
