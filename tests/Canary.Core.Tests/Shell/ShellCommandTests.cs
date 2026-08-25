using Canary.Core.Shell;

namespace Canary.Core.Tests.Shell;

public class ShellCommandTests
{
    // Locks in the fix for a real cmd.exe quoting bug found while building
    // `canary tools build`: a precompiled tool's "command" is always a
    // single token, forward-slash path to a non-batch .exe (e.g.
    // "tools/bin/curtain.exe", no arguments) -- `call`'s batch-file
    // handling tolerates an unquoted forward slash, but a plain external-
    // command dispatch (anything non-batch) re-trips cmd's "everything
    // after / looks like a switch" parsing. Only the first (program)
    // token gets quoted -- quoting the whole command breaks argument
    // splitting for every multi-word command instead.
    // Windows/cmd.exe-only, same as every other test in this suite that
    // shells out (PublishRunnerTests, ToolchainRunnerTests) -- this
    // project targets win-x64 only (see Canary.csproj), no cross-platform
    // CI to guard against here.
    [Fact]
    public void Resolve_SingleTokenForwardSlashPath_QuotesJustThatToken()
    {
        var (fileName, arguments) = ShellCommand.Resolve("tools/bin/curtain.exe");

        Assert.Equal("cmd.exe", fileName);
        Assert.Equal("/c \"call \"tools/bin/curtain.exe\"\"", arguments);
    }

    [Fact]
    public void Resolve_SingleTokenPathWithTrailingArgument_QuotesOnlyTheProgram()
    {
        var (_, arguments) = ShellCommand.Resolve("tools/bin/curtain.exe --verbose");

        Assert.Equal("/c \"call \"tools/bin/curtain.exe\" --verbose\"", arguments);
    }

    [Fact]
    public void Resolve_MultiWordPathResolvedCommand_QuotesOnlyTheFirstToken()
    {
        var (_, arguments) = ShellCommand.Resolve("powershell -NoProfile -ExecutionPolicy Bypass -File tools/reading-time.ps1");

        Assert.Equal("/c \"call \"powershell\" -NoProfile -ExecutionPolicy Bypass -File tools/reading-time.ps1\"", arguments);
    }
}
