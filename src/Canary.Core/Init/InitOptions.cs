using Canary.Core.Config;

namespace Canary.Core.Init;

// The resolved set of values `canary init` scaffolds a new project from,
// whether they came from interactive prompts or a --config source. Theme
// paths and the tools registry deliberately aren't here -- SiteInitializer
// always fixes those to what it actually scaffolds, never sourcing them
// from user input. See PLAN.md's "Scaffolding" section.
public sealed record InitOptions(
    string SiteName,
    string BaseUrl,
    RenderMode RenderMode,
    string ContentRoot,
    string OutputDir,
    int NavDepth,
    bool CopyDefaultsOnInit,
    bool PreferBuiltIn,
    int ServePort);
