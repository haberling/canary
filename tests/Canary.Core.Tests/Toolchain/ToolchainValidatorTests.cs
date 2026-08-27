using Canary.Core.Toolchain;

namespace Canary.Core.Tests.Toolchain;

public class ToolchainValidatorTests : IDisposable
{
    private readonly string _siteRoot;

    public ToolchainValidatorTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "canary-toolchainvalidator-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_siteRoot, "tools"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_siteRoot))
        {
            Directory.Delete(_siteRoot, recursive: true);
        }
    }

    private ToolchainContext NewContext() =>
        new(_siteRoot, "games/tesselate", Path.Combine(_siteRoot, "manifest.json"));

    [Fact]
    public void ValidateOne_ToolThatHandlesUtf8Correctly_Passes()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "tools", "good.cs"), """
            using System.Text;
            Console.InputEncoding = new UTF8Encoding(false);
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.Out.Write(Console.In.ReadToEnd());
            """);

        var result = ToolchainValidator.ValidateOne("good", "dotnet run tools/good.cs", NewContext());

        Assert.True(result.Passed, result.Detail);
    }

    // Deliberately forces Encoding.ASCII (lossy for anything outside 0-127)
    // instead of relying on the host's ambient console codepage happening
    // to be non-UTF-8 -- that's what a real broken tool looks like in
    // practice (Console.In/Out defaulting to the wrong codepage), but
    // pinning the exact wrong encoding here keeps this test deterministic
    // across machines/CI instead of depending on what codepage the runner
    // happens to be in.
    [Fact]
    public void ValidateOne_ToolThatManglesNonAscii_Fails()
    {
        File.WriteAllText(Path.Combine(_siteRoot, "tools", "bad.cs"), """
            Console.InputEncoding = System.Text.Encoding.ASCII;
            Console.OutputEncoding = System.Text.Encoding.ASCII;
            Console.Out.Write(Console.In.ReadToEnd());
            """);

        var result = ToolchainValidator.ValidateOne("bad", "dotnet run tools/bad.cs", NewContext());

        Assert.False(result.Passed);
        Assert.Contains("did not survive", result.Detail);
    }

    [Fact]
    public void ValidateOne_ToolThatExitsNonZero_FailsWithMessage()
    {
        var registry = new Dictionary<string, string> { ["fail"] = "exit /b 1" };

        var result = ToolchainValidator.ValidateOne("fail", registry["fail"], NewContext());

        Assert.False(result.Passed);
        Assert.Contains("failed to run", result.Detail);
    }
}
