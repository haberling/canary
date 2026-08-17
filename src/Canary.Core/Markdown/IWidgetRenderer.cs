namespace Canary.Core.Markdown;

// Contract a fenced ```<name> block dispatches to. Owned here (the consumer)
// rather than in Canary.Core.Widgets (the implementers) -- concrete widgets
// live in their own namespace and depend on this abstraction, not the other
// way around.
//
// body is the fence block's raw content, expected to be YAML (see
// Canary.Core.Templating.YamlParser) -- there's no separate "title"
// parameter anymore; a widget that wants a title just declares a "title"
// field in its own YAML data like any other field. See PLAN.md's
// widget-controversy notes: the standard implementation
// (Canary.Core.Widgets.TemplateWidgetRenderer) is fully generic -- parses
// body as YAML, fills the widget's own Mustache-syntax .html template with
// it. No widget-specific code runs anywhere in Canary itself.
public interface IWidgetRenderer
{
    string Render(string body);
}
