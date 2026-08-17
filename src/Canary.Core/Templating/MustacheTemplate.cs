using System.Text;

namespace Canary.Core.Templating;

// Hand-rolled Mustache-syntax templater: {{var}} (HTML-escaped), {{#section}}
// ... {{/section}} (iterates a list; renders once against the outer context
// if the value is a truthy scalar; renders once against the value itself if
// it's a map), {{^section}} ... {{/section}} (renders against the outer
// context only if the value is falsy/empty/missing). Real Mustache syntax,
// a practical subset -- no partials, no triple-mustache/unescaped output,
// no lambdas. See PLAN.md's widget-controversy notes for why this exists:
// a widget author who's used Mustache before needs no Canary-specific
// knowledge to read or write a template.
public static class MustacheTemplate
{
    public static string Render(string template, YamlValue context) =>
        RenderBlock(template, 0, context).output;

    private static (string output, int endIndex) RenderBlock(string template, int start, YamlValue context)
    {
        var sb = new StringBuilder();
        var i = start;

        while (i < template.Length)
        {
            var tagStart = template.IndexOf("{{", i, StringComparison.Ordinal);
            if (tagStart == -1)
            {
                sb.Append(template, i, template.Length - i);
                i = template.Length;
                break;
            }

            sb.Append(template, i, tagStart - i);
            var tagEnd = template.IndexOf("}}", tagStart, StringComparison.Ordinal);
            if (tagEnd == -1)
            {
                sb.Append(template, tagStart, template.Length - tagStart);
                i = template.Length;
                break;
            }

            var tag = template[(tagStart + 2)..tagEnd].Trim();
            i = tagEnd + 2;

            if (tag.StartsWith('#'))
            {
                var key = tag[1..].Trim();
                var (inner, afterEnd) = ExtractSectionBody(template, i, key);
                sb.Append(RenderSection(inner, Lookup(context, key), context, negate: false));
                i = afterEnd;
            }
            else if (tag.StartsWith('^'))
            {
                var key = tag[1..].Trim();
                var (inner, afterEnd) = ExtractSectionBody(template, i, key);
                sb.Append(RenderSection(inner, Lookup(context, key), context, negate: true));
                i = afterEnd;
            }
            else if (tag.StartsWith('/'))
            {
                // Closing tag with no matching opener at this level -- stop
                // here; ExtractSectionBody handles same-key nested closers
                // itself via depth-counting.
                return (sb.ToString(), tagStart);
            }
            else
            {
                sb.Append(EscapeHtml(ScalarText(Lookup(context, tag))));
            }
        }

        return (sb.ToString(), i);
    }

    private static (string body, int endIndex) ExtractSectionBody(string template, int start, string key)
    {
        var depth = 1;
        var i = start;

        while (i < template.Length)
        {
            var tagStart = template.IndexOf("{{", i, StringComparison.Ordinal);
            if (tagStart == -1) break;
            var tagEnd = template.IndexOf("}}", tagStart, StringComparison.Ordinal);
            if (tagEnd == -1) break;
            var tag = template[(tagStart + 2)..tagEnd].Trim();

            if ((tag.StartsWith('#') || tag.StartsWith('^')) && tag[1..].Trim() == key)
            {
                depth++;
            }
            else if (tag.StartsWith('/') && tag[1..].Trim() == key)
            {
                depth--;
                if (depth == 0)
                {
                    return (template[start..tagStart], tagEnd + 2);
                }
            }

            i = tagEnd + 2;
        }

        return (template[start..], template.Length); // unclosed section: rest of template is the body
    }

    private static string RenderSection(string body, YamlValue? value, YamlValue outerContext, bool negate)
    {
        var truthy = IsTruthy(value);

        if (negate)
        {
            return truthy ? "" : RenderBlock(body, 0, outerContext).output;
        }

        if (!truthy) return "";

        if (value is YamlList list)
        {
            var sb = new StringBuilder();
            foreach (var item in list.Items)
            {
                sb.Append(RenderBlock(body, 0, item).output);
            }
            return sb.ToString();
        }

        if (value is YamlMap)
        {
            return RenderBlock(body, 0, value).output;
        }

        // Truthy scalar (e.g. a "copy: true" flag): render once against the
        // OUTER context -- a bare scalar has no fields of its own, so
        // {{label}}/{{command}} inside the section still resolve against
        // the enclosing item.
        return RenderBlock(body, 0, outerContext).output;
    }

    private static bool IsTruthy(YamlValue? value) => value switch
    {
        null => false,
        YamlScalar { Value: null } => false,
        YamlScalar { Value: "" } => false,
        YamlScalar { Value: "false" } => false,
        YamlList list => list.Items.Count > 0,
        _ => true,
    };

    private static YamlValue? Lookup(YamlValue context, string key) =>
        context is YamlMap map && map.Entries.TryGetValue(key, out var value) ? value : null;

    private static string ScalarText(YamlValue? value) =>
        value is YamlScalar { Value: { } s } ? s : "";

    private static string EscapeHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
