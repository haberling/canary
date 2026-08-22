namespace Canary.Core.Config;

public sealed class ThemeConfig
{
    public string? Shell { get; set; }
    public string? Base { get; set; }
    public string? Theme { get; set; }

    // Both optional, paths relative to siteRoot like Base/Theme above. Left
    // unset by default (including in templates/default's scaffold) so a
    // freshly-init'd site renders with no logo/favicon rather than
    // inheriting Canary's own branding -- a site opts in explicitly, same
    // "explicit over implicit" principle as the !url YAML tag. See
    // SiteBuilder.CopyThemeAssets and PLAN.md's logo/favicon note.
    public string? Logo { get; set; }
    public string? Favicon { get; set; }
}
