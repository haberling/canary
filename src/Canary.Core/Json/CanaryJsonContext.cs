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
// model type that only two need a custom converter -- RenderMode's
// camelCase string form, and ToolEntry's string-or-object union (see
// ToolEntryJsonConverter below) -- so one context covering all of them is
// enough.
// WriteIndented applies to every type here even though CanaryConfig is only
// ever deserialized, never serialized -- indentation is a write-only
// formatting concern, so it's inert (and harmless) on the read path.
// ReadCommentHandling/AllowTrailingCommas make every file this context
// reads JSONC rather than strict JSON (canary.jsonc, .toolchain.json,
// .nav.json alike) -- deliberate: a config file worth hand-editing is
// worth being able to comment, and this project leans toward a heavily-
// annotated config as an onboarding aid over strict-JSON purity. Stdlib
// only, no third-party parser -- real JSON5 (unquoted keys, single-quoted
// strings) was considered and rejected for that reason; comments +
// trailing commas cover the actual onboarding goal.
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    Converters = [typeof(RenderModeJsonConverter), typeof(ToolEntryJsonConverter)])]
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

// A "tools" registry value is either a bare command string (today's only
// form -- becomes ToolEntry with Source null) or an object with
// "command"/"source" fields (opts into `canary tools build`
// precompilation -- see the 0.2.0 plan's "persistent toolchain-tool
// workers" section). System.Text.Json has no built-in "string or object"
// union support, hence this hand-written converter instead of relying on
// the source generator's normal per-property mapping.
internal sealed class ToolEntryJsonConverter : JsonConverter<ToolEntry>
{
    public override ToolEntry Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new ToolEntry(reader.GetString()!);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(
                $"A \"tools\" registry entry must be a string or an object with \"command\"/\"source\" fields, found {reader.TokenType}.");
        }

        string? command = null;
        string? source = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var propertyName = reader.GetString();
            reader.Read();
            switch (propertyName?.ToLowerInvariant())
            {
                case "command":
                    command = reader.GetString();
                    break;
                case "source":
                    source = reader.GetString();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (string.IsNullOrEmpty(command))
        {
            throw new JsonException("A \"tools\" registry entry object must have a non-empty \"command\" field.");
        }

        return new ToolEntry(command, source);
    }

    public override void Write(Utf8JsonWriter writer, ToolEntry value, JsonSerializerOptions options)
    {
        if (value.Source is null)
        {
            writer.WriteStringValue(value.Command);
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("command", value.Command);
        writer.WriteString("source", value.Source);
        writer.WriteEndObject();
    }
}
