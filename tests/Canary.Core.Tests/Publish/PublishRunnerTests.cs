using Canary.Core.Publish;

namespace Canary.Core.Tests.Publish;

public class PublishRunnerTests : IDisposable
{
    private readonly string _siteRoot;

    public PublishRunnerTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "canary-publishrunner-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(_siteRoot, "publish"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_siteRoot))
        {
            Directory.Delete(_siteRoot, recursive: true);
        }
    }

    // PublishRunner deliberately doesn't capture the child's stdout (it
    // streams it live instead -- see PublishRunner's own comment) -- so
    // tests prove effects by having the fixture script write to a file
    // instead of relying on captured output.
    //
    // Scripts live under publish/ (a subdirectory), never as a bare
    // filename directly in _siteRoot -- found via a real failure that a
    // bare filename with no path separator (e.g. "publish.cmd") silently
    // fails to resolve under `call` on a machine with
    // NoDefaultCurrentDirectoryInExePath set (a real Windows security
    // hardening setting, not an artifact of this test environment): that
    // setting disables implicit current-directory search for a bare name,
    // and a relative path with a separator bypasses that search
    // algorithm entirely. See ShellCommand's own note on this.
    private string WriteProofScript(string fileName, string body)
    {
        var relativePath = Path.Combine("publish", fileName);
        File.WriteAllText(Path.Combine(_siteRoot, relativePath), $"@echo off{Environment.NewLine}{body}");
        return relativePath;
    }

    [Fact]
    public void Run_SuccessfulCommand_DoesNotThrow()
    {
        var script = WriteProofScript("publish.cmd", "exit /b 0");

        var ex = Record.Exception(() => PublishRunner.Run(script, _siteRoot, "docs"));

        Assert.Null(ex);
    }

    [Fact]
    public void Run_NonZeroExit_Throws()
    {
        var script = WriteProofScript("publish.cmd", "exit /b 1");

        var ex = Assert.Throws<InvalidOperationException>(() => PublishRunner.Run(script, _siteRoot, "docs"));
        Assert.Contains(script, ex.Message);
    }

    [Fact]
    public void Run_SetsCanaryOutputDirToAbsoluteOutputPath()
    {
        var proofPath = Path.Combine(_siteRoot, "proof.txt");
        var script = WriteProofScript("publish.cmd", $"echo %CANARY_OUTPUT_DIR% > \"{proofPath}\"");

        PublishRunner.Run(script, _siteRoot, "docs");

        var expected = Path.GetFullPath(Path.Combine(_siteRoot, "docs"));
        Assert.Equal(expected, File.ReadAllText(proofPath).Trim());
    }

    [Fact]
    public void Run_UsesSiteRootAsWorkingDirectory()
    {
        var proofPath = Path.Combine(_siteRoot, "proof.txt");
        var script = WriteProofScript("publish.cmd", $"echo %CD% > \"{proofPath}\"");

        PublishRunner.Run(script, _siteRoot, "docs");

        Assert.Equal(Path.GetFullPath(_siteRoot), File.ReadAllText(proofPath).Trim());
    }
}
