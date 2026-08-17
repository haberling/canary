using Canary.Core.Markdown;
using Canary.Core.Templating;

namespace Canary.Core.Widgets;

// The standard widget implementation (see PLAN.md's widget-controversy
// notes): fully generic, no widget-specific code runs here or anywhere in
// Canary. A widget is an .html file (real Mustache syntax); the fence
// block's body is parsed as YAML and used to fill it. This class is the
// entire "renderer" -- every widget, built-in or site-authored, goes
// through exactly this same parse-and-fill, never anything bespoke.
public sealed class TemplateWidgetRenderer : IWidgetRenderer
{
    private readonly string _templatePath;

    public TemplateWidgetRenderer(string templatePath)
    {
        _templatePath = templatePath;
    }

    public string Render(string body)
    {
        var template = File.ReadAllText(_templatePath);
        var data = YamlParser.Parse(body);
        return MustacheTemplate.Render(template, data);
    }
}
