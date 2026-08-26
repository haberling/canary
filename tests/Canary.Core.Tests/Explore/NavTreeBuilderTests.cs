using Canary.Core.Explore;
using Canary.Core.Manifest;

namespace Canary.Core.Tests.Explore;

public class NavTreeBuilderTests
{
    [Fact]
    public void Build_LeafWithPath_ShowsItsLinkTarget()
    {
        var items = new List<NavItem> { new() { Title = "About", Path = "about" } };

        var roots = NavTreeBuilder.Build(items);

        Assert.Equal("About  -> /about", Assert.Single(roots).Label);
    }

    [Fact]
    public void Build_HomeItem_WithEmptyPath_LinksToSlash()
    {
        var items = new List<NavItem> { new() { Title = "Home", Path = "" } };

        var roots = NavTreeBuilder.Build(items);

        Assert.Equal("Home  -> /", Assert.Single(roots).Label);
    }

    [Fact]
    public void Build_DropdownOnlyItem_WithNullPath_IsMarkedDistinctly()
    {
        // A folder with children but no landing page must not look like a
        // real, clickable link -- plan-0-2-0.md's "Interactive behavior".
        var items = new List<NavItem>
        {
            new()
            {
                Title = "Games",
                Path = null,
                Children = [new NavItem { Title = "Tesselate", Path = "games/tesselate" }],
            },
        };

        var roots = NavTreeBuilder.Build(items);
        var games = Assert.Single(roots);

        Assert.Equal("Games  (dropdown only)", games.Label);
        Assert.DoesNotContain("->", games.Label);
        var child = Assert.Single(games.Children);
        Assert.Equal("Tesselate  -> /games/tesselate", child.Label);
    }
}
