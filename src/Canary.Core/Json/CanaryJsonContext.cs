using System.Text.Json;
using System.Text.Json.Serialization;
using Canary.Core.Config;
using Canary.Core.Manifest;
using Canary.Core.Toolchain;

namespace Canary.Core.Json;

// Source-generated JSON metadata for every type this codebase deserializes/
// serializes, so JsonSerializer never needs reflection to inspect their
// shape -- required for Native AOT (the trimmer can't statically follow
// plain reflection-based JsonSerializer.Deserialize<T> the way this project
// used before). See PLAN.md's packaging work: confirmed by reading every
// model type that none of them are polymorphic and none need a custom
// converter besides RenderMode's camelCase string form below, so one
// context covering all of them is enough.
// WriteIndented applies to every type here even though CanaryConfig is only
// ever deserialized, never serialized -- indentation is a write-only
// formatting concern, so it's inert (and harmless) on the read path.
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    Converters = [typeof(RenderModeJsonConverter)])]
[JsonSerializable(typeof(CanaryConfig))]
[JsonSerializable(typeof(NavOverride))]
[JsonSerializable(typeof(SiteManifest))]
[JsonSerializable(typeof(ToolchainOverride))]
internal partial class CanaryJsonContext : JsonSerializerContext
{
}

// JsonSourceGenerationOptions.Converters only accepts converter types with a
// parameterless constructor, so the camelCase naming policy the old
// reflection-based `new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`
// used has to be baked into a small derived converter instead of passed
// inline.
internal sealed class RenderModeJsonConverter : JsonStringEnumConverter<RenderMode>
{
    public RenderModeJsonConverter() : base(JsonNamingPolicy.CamelCase)
    {
    }
}
