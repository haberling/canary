namespace Canary.Core.Templating;

// Minimal data model for the practical YAML subset widget fence bodies use.
// See YamlParser for exactly what's supported.
public abstract class YamlValue
{
    public static readonly YamlValue Null = new YamlScalar(null);
}

public sealed class YamlScalar : YamlValue
{
    public string? Value { get; }
    public YamlScalar(string? value) => Value = value;
}

public sealed class YamlMap : YamlValue
{
    public Dictionary<string, YamlValue> Entries { get; } = new();
}

public sealed class YamlList : YamlValue
{
    public List<YamlValue> Items { get; } = new();
}
