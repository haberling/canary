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
    public ServeConfig Serve { get; set; } = new();

    // name -> tool entry. Definition only -- application (which pages a
    // tool runs on) lives per-directory in .toolchain.json, not here. See
    // PLAN.md's "Content toolchain" section. A JSON value here can be a
    // bare command string (ToolEntry.Source stays null) or an object with
    // "command"/"source" fields, opting into `canary tools build`
    // precompilation -- see ToolEntry and Json.CanaryJsonContext's
    // ToolEntryJsonConverter, and the 0.2.0 plan's "persistent
    // toolchain-tool workers" section.
    public Dictionary<string, ToolEntry> Tools { get; set; } = new();

    // A single arbitrary external command, run by `canary publish` after a
    // fresh build. Optional and not validated -- Canary doesn't know or
    // want to know how a site is actually hosted; this is deliberately a
    // bare string, not a deploy-target abstraction, same "just run
    // whatever the site author wrote" philosophy as a toolchain tool. See
    // PLAN.md's "Publishing" section.
    public string? Publish { get; set; }

    // Written by `canary init` on a successful scaffold; checked by future
    // `canary init` runs against the same directory to refuse silently
    // overwriting an existing project without --force. An explicit,
    // self-documenting marker rather than inferring "already a project"
    // from canary.jsonc's mere existence -- see PLAN.md's "Scaffolding"
    // section. Not validated by ConfigLoader, not read anywhere in the
    // build/serve pipeline.
    public bool Initialized { get; set; }
}
