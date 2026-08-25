namespace Canary.Core.Config;

// A "tools" registry entry (see CanaryConfig.Tools). Command is what
// ToolchainRunner actually spawns -- same meaning a bare string value had
// before this type existed. Source is purely additive: when present, it
// points at the .cs file `canary tools build` compiles into Command, and
// what the staleness check in ToolRegistryCheck compares Command's mtime
// against. ToolchainRunner itself never sees this type or Source -- see
// PLAN.md's "Content toolchain" and 0.2.0 plan's "persistent
// toolchain-tool workers" sections.
public sealed record ToolEntry(string Command, string? Source = null);
