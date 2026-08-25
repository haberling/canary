using Canary.Core.Toolchain;

namespace Canary.Core.Tests.Toolchain;

// Real end-to-end tests -- each one actually shells out to `dotnet
// publish -p:PublishAot=true`, ~5-10s of native codegen per case, far
// slower than the rest of this suite. Deliberately kept to two cases
// (success, compile failure) rather than an exhaustive matrix -- see the
// 0.2.0 plan's "persistent toolchain-tool workers" section. Requires a
// working Native AOT toolchain (a reachable vswhere.exe/link.exe) on
// whatever machine runs `dotnet test` -- same requirement `canary tools
// build` itself has, not a new category of dependency.
public class ToolsBuildRunnerTests : IDisposable
{
    private readonly string _siteRoot;

    public ToolsBuildRunnerTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "canary-toolsbuildrunner-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_siteRoot, "tools"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_siteRoot))
        {
            Directory.Delete(_siteRoot, recursive: true);
        }
    }

    [Fact]
    public void Build_ValidSource_ProducesRunnableBinaryAtCommandPath()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "tools", "echo-tool.cs"), """
            string? line;
            while ((line = Console.In.ReadLine()) is not null)
            {
                Console.Out.WriteLine(line);
            }
            Console.Out.WriteLine("BUILT-OK");
            """);

        ToolsBuildRunner.Build("tools/echo-tool.cs", "tools/bin/echo-tool.exe", _siteRoot);

        var commandPath = Path.Combine(_siteRoot, "tools", "bin", "echo-tool.exe");
        Assert.True(File.Exists(commandPath));

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = commandPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var process = System.Diagnostics.Process.Start(psi)!;
        process.StandardInput.Write("hello");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Assert.Contains("hello", output);
        Assert.Contains("BUILT-OK", output);
    }

    [Fact]
    public void Build_SourceWithCompileError_ThrowsInvalidOperationException()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "tools", "broken.cs"), "this is not valid C#;;;");

        Assert.Throws<InvalidOperationException>(
            () => ToolsBuildRunner.Build("tools/broken.cs", "tools/bin/broken.exe", _siteRoot));
    }

    // Regression coverage for a real bug found while verifying this
    // feature manually: a precompiled tool's Command is always exactly
    // the shape that tripped cmd.exe's quoting (a single-token, forward-
    // slash path to a non-batch .exe) -- Build_ValidSource... above
    // invokes the published exe directly via Process.Start, which never
    // exercises Shell.ShellCommand/cmd.exe at all, so it didn't catch
    // this. Running the compiled tool through ToolchainRunner.Run (the
    // actual code path `canary build` uses) is what would have failed
    // before the ShellCommand.Resolve fix. See ShellCommandTests for the
    // narrower, fast, string-only assertions of that fix.
    [Fact]
    public void Build_ValidSource_ProducedBinaryRunsThroughToolchainRunner()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "tools", "chain-tool.cs"), """
            string? line;
            while ((line = Console.In.ReadLine()) is not null)
            {
                Console.Out.WriteLine(line);
            }
            Console.Out.WriteLine("CHAINED-OK");
            """);
        ToolsBuildRunner.Build("tools/chain-tool.cs", "tools/bin/chain-tool.exe", _siteRoot);

        var registry = new Dictionary<string, string> { ["chain-tool"] = "tools/bin/chain-tool.exe" };
        var context = new ToolchainContext(_siteRoot, "", Path.Combine(_siteRoot, "manifest.json"));

        var result = ToolchainRunner.Run(["chain-tool"], registry, context, "page markdown");

        Assert.Contains("page markdown", result);
        Assert.Contains("CHAINED-OK", result);
    }
}
