using Canary.Core.Hooks;

namespace Canary.Core.Tests.Hooks;

public class HookRunnerTests : IDisposable
{
    private readonly string _siteRoot;

    public HookRunnerTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "canary-hookrunner-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_siteRoot, "tools"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_siteRoot))
        {
            Directory.Delete(_siteRoot, recursive: true);
        }
    }

    private HookContext NewContext(string routePath = "games/tesselate", string manifestPath = "manifest.json") =>
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
    public void Run_SingleHook_TransformsMarkdown()
    {
        var script = WriteMarkerScript("tools/marker-a.cmd", "MARKER-A");
        var registry = new Dictionary<string, string> { ["marker-a"] = script };

        var result = HookRunner.Run(["marker-a"], registry, NewContext(), "# Hello");

        Assert.Contains("# Hello", result);
        Assert.Contains("MARKER-A", result);
    }

    [Fact]
    public void Run_MultipleHooks_ChainsInDeclaredOrder()
    {
        var scriptA = WriteMarkerScript("tools/marker-a.cmd", "MARKER-A");
        var scriptB = WriteMarkerScript("tools/marker-b.cmd", "MARKER-B");
        var registry = new Dictionary<string, string> { ["a"] = scriptA, ["b"] = scriptB };

        var result = HookRunner.Run(["a", "b"], registry, NewContext(), "SOURCE");

        var indexA = result.IndexOf("MARKER-A", StringComparison.Ordinal);
        var indexB = result.IndexOf("MARKER-B", StringComparison.Ordinal);
        Assert.True(indexA >= 0 && indexB >= 0 && indexA < indexB,
            "MARKER-A (from the first hook) must appear before MARKER-B (from the second) -- proves each hook's stdout became the next hook's stdin, in declared order.");
    }

    [Fact]
    public void Run_NonZeroExit_ThrowsAndFailsTheBuild()
    {
        var registry = new Dictionary<string, string> { ["fail"] = "exit /b 1" };

        var ex = Assert.Throws<InvalidOperationException>(() => HookRunner.Run(["fail"], registry, NewContext(), "source"));
        Assert.Contains("exit /b 1", ex.Message);
    }

    [Fact]
    public void Run_UnknownHookName_Throws()
    {
        var registry = new Dictionary<string, string>();

        var ex = Assert.Throws<InvalidOperationException>(() => HookRunner.Run(["nonexistent"], registry, NewContext(), "source"));
        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public void Run_HookCanReadRoutePathAndManifestPathFromEnvironment()
    {
        var scriptPath = Path.Combine(_siteRoot, "tools", "env-echo.cmd");
        File.WriteAllText(scriptPath,
            $"@echo off{Environment.NewLine}findstr \"^\"{Environment.NewLine}" +
            $"echo ROUTE=%CANARY_ROUTE_PATH%{Environment.NewLine}" +
            $"echo MANIFEST=%CANARY_MANIFEST_PATH%{Environment.NewLine}");
        var registry = new Dictionary<string, string> { ["env-echo"] = "tools/env-echo.cmd" };
        var manifestPath = Path.Combine(_siteRoot, "content", "manifest.json");
        var context = new HookContext(_siteRoot, "games/tesselate", manifestPath);

        var result = HookRunner.Run(["env-echo"], registry, context, "# Hello");

        Assert.Contains("ROUTE=games/tesselate", result);
        Assert.Contains($"MANIFEST={manifestPath}", result);
    }

    [Fact]
    public void Run_HookSeesEmptyRoutePathForSiteRoot()
    {
        var scriptPath = Path.Combine(_siteRoot, "tools", "env-echo.cmd");
        File.WriteAllText(scriptPath,
            $"@echo off{Environment.NewLine}findstr \"^\"{Environment.NewLine}echo ROUTE=[%CANARY_ROUTE_PATH%]{Environment.NewLine}");
        var registry = new Dictionary<string, string> { ["env-echo"] = "tools/env-echo.cmd" };
        var context = new HookContext(_siteRoot, "", Path.Combine(_siteRoot, "manifest.json"));

        var result = HookRunner.Run(["env-echo"], registry, context, "# Home");

        Assert.Contains("ROUTE=[]", result);
    }
}
