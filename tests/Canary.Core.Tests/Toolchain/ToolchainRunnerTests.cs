using Canary.Core.Toolchain;

namespace Canary.Core.Tests.Toolchain;

public class ToolchainRunnerTests : IDisposable
{
    private readonly string _siteRoot;

    public ToolchainRunnerTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "canary-toolchainrunner-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_siteRoot, "tools"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_siteRoot))
        {
            Directory.Delete(_siteRoot, recursive: true);
        }
    }

    private ToolchainContext NewContext(string routePath = "games/tesselate", string manifestPath = "manifest.json") =>
        new(_siteRoot, routePath, Path.Combine(_siteRoot, manifestPath));

    // Windows batch script that echoes stdin verbatim (the "findstr ^" idiom
    // -- ^ matches start-of-line, so it matches and re-prints every line)
    // then appends a fixed marker line, so a test can prove both "stdin
    // reached the process" and "stdout was captured" in one deterministic
    // command.
    private string WriteMarkerScript(string relativePath, string marker)
    {
        var fullPath = Path.Combine(_siteRoot, relativePath);
        File.WriteAllText(fullPath, $"@echo off{Environment.NewLine}findstr \"^\"{Environment.NewLine}echo {marker}{Environment.NewLine}");
        return relativePath;
    }

    [Fact]
    public void Run_SingleTool_TransformsMarkdown()
    {
        var script = WriteMarkerScript("tools/marker-a.cmd", "MARKER-A");
        var registry = new Dictionary<string, string> { ["marker-a"] = script };

        var result = ToolchainRunner.Run(["marker-a"], registry, NewContext(), "# Hello");

        Assert.Contains("# Hello", result);
        Assert.Contains("MARKER-A", result);
    }

    [Fact]
    public void Run_MultipleTools_ChainsInDeclaredOrder()
    {
        var scriptA = WriteMarkerScript("tools/marker-a.cmd", "MARKER-A");
        var scriptB = WriteMarkerScript("tools/marker-b.cmd", "MARKER-B");
        var registry = new Dictionary<string, string> { ["a"] = scriptA, ["b"] = scriptB };

        var result = ToolchainRunner.Run(["a", "b"], registry, NewContext(), "SOURCE");

        var indexA = result.IndexOf("MARKER-A", StringComparison.Ordinal);
        var indexB = result.IndexOf("MARKER-B", StringComparison.Ordinal);
        Assert.True(indexA >= 0 && indexB >= 0 && indexA < indexB,
            "MARKER-A (from the first tool) must appear before MARKER-B (from the second) -- proves each tool's stdout became the next tool's stdin, in declared order.");
    }

    [Fact]
    public void Run_NonZeroExit_ThrowsAndFailsTheBuild()
    {
        var registry = new Dictionary<string, string> { ["fail"] = "exit /b 1" };

        var ex = Assert.Throws<InvalidOperationException>(() => ToolchainRunner.Run(["fail"], registry, NewContext(), "source"));
        Assert.Contains("exit /b 1", ex.Message);
    }

    [Fact]
    public void Run_UnknownToolName_Throws()
    {
        var registry = new Dictionary<string, string>();

        var ex = Assert.Throws<InvalidOperationException>(() => ToolchainRunner.Run(["nonexistent"], registry, NewContext(), "source"));
        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public void Run_ToolCanReadRoutePathAndManifestPathFromEnvironment()
    {
        var scriptPath = Path.Combine(_siteRoot, "tools", "env-echo.cmd");
        File.WriteAllText(scriptPath,
            $"@echo off{Environment.NewLine}findstr \"^\"{Environment.NewLine}" +
            $"echo ROUTE=%CANARY_ROUTE_PATH%{Environment.NewLine}" +
            $"echo MANIFEST=%CANARY_MANIFEST_PATH%{Environment.NewLine}");
        var registry = new Dictionary<string, string> { ["env-echo"] = "tools/env-echo.cmd" };
        var manifestPath = Path.Combine(_siteRoot, "content", "manifest.json");
        var context = new ToolchainContext(_siteRoot, "games/tesselate", manifestPath);

        var result = ToolchainRunner.Run(["env-echo"], registry, context, "# Hello");

        Assert.Contains("ROUTE=games/tesselate", result);
        Assert.Contains($"MANIFEST={manifestPath}", result);
    }

    [Fact]
    public void Run_ToolSeesEmptyRoutePathForSiteRoot()
    {
        var scriptPath = Path.Combine(_siteRoot, "tools", "env-echo.cmd");
        File.WriteAllText(scriptPath,
            $"@echo off{Environment.NewLine}findstr \"^\"{Environment.NewLine}echo ROUTE=[%CANARY_ROUTE_PATH%]{Environment.NewLine}");
        var registry = new Dictionary<string, string> { ["env-echo"] = "tools/env-echo.cmd" };
        var context = new ToolchainContext(_siteRoot, "", Path.Combine(_siteRoot, "manifest.json"));

        var result = ToolchainRunner.Run(["env-echo"], registry, context, "# Home");

        Assert.Contains("ROUTE=[]", result);
    }

    // Regression test for a real bug: non-ASCII markdown (math symbols like
    // ∑, subscripts like ₙ, typographic ellipses like …) came out as
    // mojibake after passing through a tool chain, because neither side of
    // the stdio pipe was pinned to UTF-8 -- .NET's Process fell back to the
    // OS console codepage (437/1252 on Windows, never UTF-8) for the
    // redirected pipe. Uses a "dotnet run" file-based tool -- the exact
    // shape every real tool in Canary's own guide and consoland's site
    // both use (Console.In.ReadToEnd() / Console.Out.Write()) -- rather
    // than a .cmd script, since a native console tool like findstr doesn't
    // exercise .NET's Process stdio encoding path at all and wouldn't have
    // caught this. The tool itself sets Console.InputEncoding/
    // OutputEncoding to UTF-8, matching what the toolchain guide now asks
    // tool authors to do -- this test is pinning Canary's half of that
    // contract (see ToolchainRunner.ToolIoEncoding).
    [Fact]
    public void Run_NonAsciiMarkdown_SurvivesRoundTripThroughDotnetRunTool()
    {
        var toolPath = Path.Combine(_siteRoot, "tools", "echo.cs");
        File.WriteAllText(toolPath, """
            using System.Text;
            Console.InputEncoding = new UTF8Encoding(false);
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.Out.Write(Console.In.ReadToEnd());
            """);
        var registry = new Dictionary<string, string> { ["echo"] = "dotnet run tools/echo.cs" };
        const string markdown = "sum: ∑ W = w₁+w₂ + … + wₙ";

        var result = ToolchainRunner.Run(["echo"], registry, NewContext(), markdown);

        Assert.Equal(markdown, result);
    }
}
