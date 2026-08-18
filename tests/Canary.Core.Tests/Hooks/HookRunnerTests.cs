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

        var result = HookRunner.Run(["marker-a"], registry, _siteRoot, "# Hello");

        Assert.Contains("# Hello", result);
        Assert.Contains("MARKER-A", result);
    }

    [Fact]
    public void Run_MultipleHooks_ChainsInDeclaredOrder()
    {
        var scriptA = WriteMarkerScript("tools/marker-a.cmd", "MARKER-A");
        var scriptB = WriteMarkerScript("tools/marker-b.cmd", "MARKER-B");
        var registry = new Dictionary<string, string> { ["a"] = scriptA, ["b"] = scriptB };

        var result = HookRunner.Run(["a", "b"], registry, _siteRoot, "SOURCE");

        var indexA = result.IndexOf("MARKER-A", StringComparison.Ordinal);
        var indexB = result.IndexOf("MARKER-B", StringComparison.Ordinal);
        Assert.True(indexA >= 0 && indexB >= 0 && indexA < indexB,
            "MARKER-A (from the first hook) must appear before MARKER-B (from the second) -- proves each hook's stdout became the next hook's stdin, in declared order.");
    }

    [Fact]
    public void Run_NonZeroExit_ThrowsAndFailsTheBuild()
    {
        var registry = new Dictionary<string, string> { ["fail"] = "exit /b 1" };

        var ex = Assert.Throws<InvalidOperationException>(() => HookRunner.Run(["fail"], registry, _siteRoot, "source"));
        Assert.Contains("exit /b 1", ex.Message);
    }

    [Fact]
    public void Run_UnknownHookName_Throws()
    {
        var registry = new Dictionary<string, string>();

        var ex = Assert.Throws<InvalidOperationException>(() => HookRunner.Run(["nonexistent"], registry, _siteRoot, "source"));
        Assert.Contains("nonexistent", ex.Message);
    }

    [Fact]
    public void ChecksumSeed_UnknownHookName_Throws()
    {
        var registry = new Dictionary<string, string>();

        Assert.Throws<InvalidOperationException>(() => HookRunner.ChecksumSeed(["nonexistent"], registry, _siteRoot));
    }

    [Fact]
    public void ChecksumSeed_ChangesWhenReferencedScriptContentChanges()
    {
        var scriptPath = Path.Combine(_siteRoot, "tools", "hook.cmd");
        File.WriteAllText(scriptPath, "@echo off\r\nfindstr \"^\"\r\n");
        var registry = new Dictionary<string, string> { ["h"] = "tools/hook.cmd" };

        var before = HookRunner.ChecksumSeed(["h"], registry, _siteRoot);
        File.WriteAllText(scriptPath, "@echo off\r\nfindstr \"^\"\r\necho CHANGED\r\n");
        var after = HookRunner.ChecksumSeed(["h"], registry, _siteRoot);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void ChecksumSeed_ChangesWhenCommandStringChanges()
    {
        var before = HookRunner.ChecksumSeed(["h"], new Dictionary<string, string> { ["h"] = "echo one" }, _siteRoot);
        var after = HookRunner.ChecksumSeed(["h"], new Dictionary<string, string> { ["h"] = "echo two" }, _siteRoot);

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void ChecksumSeed_DoesNotActuallyRunTheHook()
    {
        // A command that would fail if executed -- ChecksumSeed must never
        // invoke it, only look at its registered command string / referenced
        // file content. Proves the "cheap, no process execution" contract.
        var registry = new Dictionary<string, string> { ["fail"] = "exit /b 1" };

        var seed = HookRunner.ChecksumSeed(["fail"], registry, _siteRoot);

        Assert.Contains("exit /b 1", seed);
    }
}
