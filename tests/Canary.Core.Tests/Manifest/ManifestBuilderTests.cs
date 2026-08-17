using Canary.Core.Manifest;

namespace Canary.Core.Tests.Manifest;

public class ManifestBuilderTests : IDisposable
{
    private readonly string _contentRoot;

    public ManifestBuilderTests()
    {
        _contentRoot = Path.Combine(Path.GetTempPath(), "canary-manifest-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_contentRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_contentRoot))
        {
            Directory.Delete(_contentRoot, recursive: true);
        }
    }

    private void WriteFile(string relativePath, string content)
    {
        var full = Path.Combine(_contentRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [Fact]
    public void Build_IndexMd_IsPinnedFirstAsHome()
    {
        // content/index.md replaced consoland's content/home.md as the root
        // landing-page convention (see PLAN.md) -- same pinned-first,
        // titled-"Home" behavior, unified filename.
        WriteFile("index.md", "# Welcome"); // heading is irrelevant -- title is always "Home"
        WriteFile("zzz-page.md", "# ZZZ");

        var manifest = ManifestBuilder.Build(_contentRoot);

        Assert.Equal("Home", manifest.Nav[0].Title);
        // "" (not "home"/"index"/"/"): the bare-relative-fragment convention
        // every nav path uses, producing href="#/" client-side. See the
        // comment in ManifestBuilder.BuildNav for why this differs from
        // ContentScanner's "/" for the same file.
        Assert.Equal("", manifest.Nav[0].Path);
    }

    [Fact]
    public void Build_HomeMd_HasNoSpecialMeaning()
    {
        // Regression guard for the home.md -> index.md rename. Deliberately
        // not titled "Home" in its heading, so a title match can't
        // coincidentally look like the (no longer applicable) pinned
        // special-case -- the real signal is the path, not the title.
        WriteFile("home.md", "# Just A Page");

        var manifest = ManifestBuilder.Build(_contentRoot);

        var item = Assert.Single(manifest.Nav);
        Assert.Equal("home", item.Path); // ordinary ToContentPath result, not the "" pinned-home path
    }

    [Fact]
    public void Build_TopLevelPage_TitleFromFirstHeading()
    {
        WriteFile("about.md", "# About This Site\nSome text.");

        var manifest = ManifestBuilder.Build(_contentRoot);

        var item = Assert.Single(manifest.Nav);
        Assert.Equal("About This Site", item.Title);
        Assert.Equal("about", item.Path);
    }

    [Fact]
    public void Build_TopLevelPage_NoHeading_FallsBackToTitleCasedFilename()
    {
        WriteFile("my-cool-page.md", "no heading here");

        var manifest = ManifestBuilder.Build(_contentRoot);

        Assert.Equal("My Cool Page", manifest.Nav[0].Title);
    }

    [Fact]
    public void Build_DirectoryWithLandingPage_IsClickableWithChildren()
    {
        WriteFile("games/index.md", "# Games");
        WriteFile("games/tesselate.md", "# Tesselate");

        var manifest = ManifestBuilder.Build(_contentRoot);

        var item = Assert.Single(manifest.Nav);
        Assert.Equal("Games", item.Title);
        // "games", not "games/index" -- the whole point of the convention.
        Assert.Equal("games", item.Path);
        var child = Assert.Single(item.Children!);
        Assert.Equal("Tesselate", child.Title);
    }

    [Fact]
    public void Build_FileNamedAfterDirectory_IsNotTreatedAsLandingPage()
    {
        // Regression guard: the old consoland convention (content/<dir>/<Dir>.md
        // as landing page) is deliberately gone -- only exactly "index.md"
        // counts now. A file that merely shares the directory's name is just
        // a regular dropdown child.
        WriteFile("games/games.md", "# Games");
        WriteFile("games/tesselate.md", "# Tesselate");

        var manifest = ManifestBuilder.Build(_contentRoot);

        var item = Assert.Single(manifest.Nav);
        Assert.Null(item.Path);
        Assert.Equal(2, item.Children!.Count);
    }

    [Fact]
    public void Build_DirectoryWithoutLandingPage_IsDropdownOnlyTrigger()
    {
        WriteFile("games/tesselate.md", "# Tesselate");
        WriteFile("games/other.md", "# Other");

        var manifest = ManifestBuilder.Build(_contentRoot);

        var item = Assert.Single(manifest.Nav);
        Assert.Equal("Games", item.Title); // title-cased dir name
        Assert.Null(item.Path);
        Assert.Equal(2, item.Children!.Count);
    }

    [Fact]
    public void Build_DirectoryWithNoLandingPageAndNoChildren_IsOmitted()
    {
        Directory.CreateDirectory(Path.Combine(_contentRoot, "empty-dir"));

        var manifest = ManifestBuilder.Build(_contentRoot);

        Assert.Empty(manifest.Nav);
    }

    [Fact]
    public void Build_NavOverride_NoNav_ExcludesDirectory()
    {
        WriteFile("secret/secret.md", "# Secret");
        WriteFile("secret/.nav.json", """{ "nonav": true }""");

        var manifest = ManifestBuilder.Build(_contentRoot);

        Assert.Empty(manifest.Nav);
    }

    [Fact]
    public void Build_NavOverride_Priority_OrdersBeforeAlphabetical()
    {
        WriteFile("zebra/zebra.md", "# Zebra");
        WriteFile("apple/apple.md", "# Apple");
        WriteFile("zebra/.nav.json", """{ "priority": -10 }""");

        var manifest = ManifestBuilder.Build(_contentRoot);

        Assert.Equal("Zebra", manifest.Nav[0].Title);
        Assert.Equal("Apple", manifest.Nav[1].Title);
    }

    [Fact]
    public void Build_NavOverride_Deny_ExcludesListedChild()
    {
        WriteFile("games/games.md", "# Games");
        WriteFile("games/keep.md", "# Keep");
        WriteFile("games/draft.md", "# Draft");
        WriteFile("games/.nav.json", """{ "deny": ["draft.md"] }""");

        var manifest = ManifestBuilder.Build(_contentRoot);

        var item = manifest.Nav.Single(n => n.Title == "Games");
        var childTitles = item.Children!.Select(c => c.Title).ToList();
        Assert.Contains("Keep", childTitles);
        Assert.DoesNotContain("Draft", childTitles);
    }

    [Fact]
    public void Build_NavOverride_AllowAndDenyTogether_Throws()
    {
        WriteFile("games/games.md", "# Games");
        WriteFile("games/a.md", "# A");
        WriteFile("games/.nav.json", """{ "allow": ["a.md"], "deny": ["a.md"] }""");

        Assert.Throws<InvalidOperationException>(() => ManifestBuilder.Build(_contentRoot));
    }

    [Fact]
    public void Build_DirectoryWithMdFiles_BackfillsDefaultNavJson()
    {
        WriteFile("games/games.md", "# Games");

        ManifestBuilder.Build(_contentRoot);

        var navJsonPath = Path.Combine(_contentRoot, "games", ".nav.json");
        Assert.True(File.Exists(navJsonPath));
        Assert.Contains("\"nonav\"", File.ReadAllText(navJsonPath));
    }

    [Fact]
    public void BuildAndWrite_WritesManifestJsonToContentRoot()
    {
        WriteFile("index.md", "# Home");

        ManifestBuilder.BuildAndWrite(_contentRoot);

        var manifestPath = Path.Combine(_contentRoot, "manifest.json");
        Assert.True(File.Exists(manifestPath));
        Assert.Contains("\"Home\"", File.ReadAllText(manifestPath));
    }
}
