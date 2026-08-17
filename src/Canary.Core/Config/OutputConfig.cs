namespace Canary.Core.Config;

public sealed class OutputConfig
{
    // Defaults to "docs" for GitHub Pages' "serve from /docs on main" convention.
    public string Dir { get; set; } = "docs";
}
