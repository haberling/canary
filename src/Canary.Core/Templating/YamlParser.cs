namespace Canary.Core.Templating;

// Hand-rolled parser for the practical YAML subset widget fence bodies use:
// block-style mappings, lists of mappings, scalar leaf values (quoted or
// bare, plus "true"/"false"). Deliberately NOT full YAML -- no flow style
// ({...}/[...]), no anchors, no multi-line block scalars, no multi-document
// streams. Real YAML syntax for what it does support, not a bespoke format
// -- readable/writable by anyone who's used YAML before. See PLAN.md's
// widget-controversy notes for why this exists.
public static class YamlParser
{
    private sealed record Line(int Indent, string Content);

    public static YamlValue Parse(string source)
    {
        var lines = new List<Line>();
        foreach (var raw in source.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmedEnd = raw.TrimEnd();
            if (trimmedEnd.Length == 0) continue;
            var trimmed = trimmedEnd.TrimStart();
            if (trimmed.StartsWith('#')) continue; // comment-only line
            var indent = trimmedEnd.Length - trimmed.Length;
            lines.Add(new Line(indent, trimmed));
        }

        if (lines.Count == 0) return new YamlMap();

        var index = 0;
        return ParseBlock(lines, ref index, lines[0].Indent);
    }

    private static YamlValue ParseBlock(List<Line> lines, ref int index, int indent)
    {
        if (index >= lines.Count || lines[index].Indent != indent)
        {
            return new YamlScalar(null);
        }

        return lines[index].Content.StartsWith("- ") || lines[index].Content == "-"
            ? ParseList(lines, ref index, indent)
            : ParseMap(lines, ref index, indent);
    }

    private static YamlList ParseList(List<Line> lines, ref int index, int indent)
    {
        var list = new YamlList();
        var inlineIndent = indent + 2; // where content after "- " begins

        while (index < lines.Count && lines[index].Indent == indent &&
               (lines[index].Content.StartsWith("- ") || lines[index].Content == "-"))
        {
            var itemContent = lines[index].Content.Length > 1 ? lines[index].Content[2..] : "";

            if (itemContent.Length == 0)
            {
                index++;
                list.Items.Add(index < lines.Count && lines[index].Indent > indent
                    ? ParseBlock(lines, ref index, lines[index].Indent)
                    : new YamlScalar(null));
                continue;
            }

            var colonIndex = FindKeyColon(itemContent);
            if (colonIndex == -1)
            {
                // Bare scalar list item, e.g. "- value".
                list.Items.Add(ParseScalarValue(itemContent));
                index++;
                continue;
            }

            // Mapping list item: "- key: value" (or "- key:" with a nested
            // block, e.g. its own sub-list) starts a map; subsequent lines
            // indented to align with "key" (past the "- ") continue the
            // same item's map until indent drops back to this list's own
            // indent (next item or dedent).
            var map = new YamlMap();
            var firstKey = itemContent[..colonIndex].Trim();
            var firstRest = itemContent[(colonIndex + 1)..].Trim();
            index++;
            if (firstRest.Length == 0)
            {
                map.Entries[firstKey] = index < lines.Count && lines[index].Indent > inlineIndent
                    ? ParseBlock(lines, ref index, lines[index].Indent)
                    : new YamlScalar(null);
            }
            else
            {
                map.Entries[firstKey] = ParseScalarValue(firstRest);
            }

            while (index < lines.Count && lines[index].Indent == inlineIndent)
            {
                var line = lines[index].Content;
                if (line.StartsWith("- ") || line == "-") break;

                var lineColon = FindKeyColon(line);
                if (lineColon == -1) break;

                var key = line[..lineColon].Trim();
                var rest = line[(lineColon + 1)..].Trim();
                if (rest.Length == 0)
                {
                    index++;
                    map.Entries[key] = index < lines.Count && lines[index].Indent > inlineIndent
                        ? ParseBlock(lines, ref index, lines[index].Indent)
                        : new YamlScalar(null);
                }
                else
                {
                    map.Entries[key] = ParseScalarValue(rest);
                    index++;
                }
            }

            list.Items.Add(map);
        }

        return list;
    }

    private static YamlMap ParseMap(List<Line> lines, ref int index, int indent)
    {
        var map = new YamlMap();

        while (index < lines.Count && lines[index].Indent == indent)
        {
            var line = lines[index].Content;
            if (line.StartsWith("- ") || line == "-") break; // a list started here; not part of this map

            var colonIndex = FindKeyColon(line);
            if (colonIndex == -1)
            {
                index++;
                continue; // malformed line; skip rather than throw, keep the parser forgiving
            }

            var key = line[..colonIndex].Trim();
            var rest = line[(colonIndex + 1)..].Trim();

            if (rest.Length == 0)
            {
                index++;
                map.Entries[key] = index < lines.Count && lines[index].Indent > indent
                    ? ParseBlock(lines, ref index, lines[index].Indent)
                    : new YamlScalar(null);
            }
            else
            {
                map.Entries[key] = ParseScalarValue(rest);
                index++;
            }
        }

        return map;
    }

    // Finds the ":" that separates a mapping key from its value: the first
    // ": " (colon-space) or a trailing colon at end-of-line, skipping over
    // anything inside a quoted scalar (so a value like "Wikipedia: The
    // Pirates of Penzance" doesn't get misread as its own key).
    private static int FindKeyColon(string line)
    {
        char? inQuote = null;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuote != null)
            {
                if (c == inQuote) inQuote = null;
                continue;
            }
            if (c is '"' or '\'')
            {
                inQuote = c;
                continue;
            }
            if (c == ':' && (i + 1 == line.Length || line[i + 1] == ' '))
            {
                return i;
            }
        }
        return -1;
    }

    private static string ParseScalarText(string text)
    {
        if (text.Length >= 2 && ((text[0] == '"' && text[^1] == '"') || (text[0] == '\'' && text[^1] == '\'')))
        {
            return text[1..^1];
        }
        return text;
    }

    // "!url" is a real YAML tag (the "!name" syntax is a standard YAML
    // feature, e.g. "!Ref" in CloudFormation, "!include" elsewhere) marking
    // a scalar as a URL that should get UrlResolver's root-relative
    // treatment. Deliberately explicit rather than automatic: a widget
    // author writes "url: !url \"content/x.png\"" to opt a specific value
    // in, rather than Canary guessing from the field's name (e.g. any field
    // called "url") -- see PLAN.md's widget-controversy notes for why an
    // explicit marker was chosen over an implicit naming convention.
    private const string UrlTag = "!url";

    private static YamlScalar ParseScalarValue(string rest)
    {
        if (rest.StartsWith(UrlTag, StringComparison.Ordinal) &&
            (rest.Length == UrlTag.Length || rest[UrlTag.Length] == ' '))
        {
            var tagged = ParseScalarText(rest[UrlTag.Length..].TrimStart());
            return new YamlScalar(UrlResolver.Resolve(tagged));
        }

        return new YamlScalar(ParseScalarText(rest));
    }
}
