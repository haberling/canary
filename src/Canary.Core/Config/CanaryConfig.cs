namespace Canary.Core.Config;

public sealed class CanaryConfig
{
    public SiteConfig Site { get; set; } = new();
    public ContentConfig Content { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
    public RenderMode RenderMode { get; set; } = RenderMode.Hybrid;
    public ThemeConfig Theme { get; set; } = new();
    public WidgetsConfig Widgets { get; set; } = new();
    public NavConfig Nav { get; set; } = new();

    // name -> shell command. Definition only -- application (which pages a
    // tool runs on) lives per-directory in .toolchain.json, not here. See
    // PLAN.md's "Content toolchain" section.
    public Dictionary<string, string> Tools { get; set; } = new();
}
