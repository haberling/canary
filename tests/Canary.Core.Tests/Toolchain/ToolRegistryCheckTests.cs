using Canary.Core.Config;
using Canary.Core.Toolchain;

namespace Canary.Core.Tests.Toolchain;

public class ToolRegistryCheckTests : IDisposable
{
    private readonly string _siteRoot;

    public ToolRegistryCheckTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "canary-toolregistrycheck-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_siteRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_siteRoot))
        {
            Directory.Delete(_siteRoot, recursive: true);
        }
    }

    private void WriteFile(string relativePath, string content = "")
    {
        var fullPath = Path.Combine(_siteRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    [Fact]
    public void Run_SourceEntryWithMissingBinary_ThrowsNamingToolsBuild()
    {
        WriteFile("tools/curtain.cs");
        var tools = new Dictionary<string, ToolEntry>
        {
            ["curtain"] = new ToolEntry("tools/bin/curtain.exe", "tools/curtain.cs"),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ToolRegistryCheck.Run(tools, _siteRoot));

        Assert.Contains("canary tools build curtain", ex.Message);
    }

    [Fact]
    public void Run_SourceNewerThanBinary_WritesStalenessWarning()
    {
        WriteFile("tools/bin/curtain.exe");
        Thread.Sleep(50); // ensure a strictly later mtime, not just a not-earlier one
        WriteFile("tools/curtain.cs");
        var tools = new Dictionary<string, ToolEntry>
        {
            ["curtain"] = new ToolEntry("tools/bin/curtain.exe", "tools/curtain.cs"),
        };

        var originalError = Console.Error;
        try
        {
            using var capturedError = new StringWriter();
            Console.SetError(capturedError);

            ToolRegistryCheck.Run(tools, _siteRoot);

            Assert.Contains("curtain", capturedError.ToString());
            Assert.Contains("canary tools build curtain", capturedError.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Run_BinaryUpToDate_IsSilentAndDoesNotThrow()
    {
        WriteFile("tools/curtain.cs");
        Thread.Sleep(50);
        WriteFile("tools/bin/curtain.exe");
        var tools = new Dictionary<string, ToolEntry>
        {
            ["curtain"] = new ToolEntry("tools/bin/curtain.exe", "tools/curtain.cs"),
        };

        var originalError = Console.Error;
        try
        {
            using var capturedError = new StringWriter();
            Console.SetError(capturedError);

            var ex = Record.Exception(() => ToolRegistryCheck.Run(tools, _siteRoot));

            Assert.Null(ex);
            Assert.Equal(string.Empty, capturedError.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Run_PlainStringEntry_NeverCheckedEvenIfCommandDoesNotResolve()
    {
        // A plain-string entry never opted into precompilation -- its
        // "command" can be an arbitrary shell command line, not a file
        // path, so it must never be File.Exists-checked.
        var tools = new Dictionary<string, ToolEntry>
        {
            ["reading-time"] = new ToolEntry("powershell -File tools/reading-time.ps1"),
        };

        var ex = Record.Exception(() => ToolRegistryCheck.Run(tools, _siteRoot));

        Assert.Null(ex);
    }
}
