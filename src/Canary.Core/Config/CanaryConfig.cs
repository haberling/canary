namespace Canary.Core.Config;

public sealed class CanaryConfig
{
    public SiteConfig Site { get; set; } = new();
    public ContentConfig Content { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
    public RenderMode RenderMode { get; set; } = RenderMode.Hybrid;
    public ThemeConfig Theme { get; set; } = new();
    public WidgetsConfig Widgets { get; set; } = new();
}
