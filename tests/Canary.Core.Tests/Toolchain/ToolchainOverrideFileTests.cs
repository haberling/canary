using Canary.Core.Toolchain;

namespace Canary.Core.Tests.Toolchain;

public class ToolchainOverrideFileTests : IDisposable
{
    private readonly string _dir;

    public ToolchainOverrideFileTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "canary-toolchainoverride-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void EnsureFilesExist_CreatesEmptyToolchainFile()
    {
        ToolchainOverrideFile.EnsureFilesExist([_dir]);

        var path = Path.Combine(_dir, ".toolchain.json");
        Assert.True(File.Exists(path));
        Assert.Empty(ToolchainOverrideFile.ResolveForDirectory(_dir));
    }

    [Fact]
    public void EnsureFilesExist_DoesNotOverwriteExistingFile()
    {
        var path = Path.Combine(_dir, ".toolchain.json");
        File.WriteAllText(path, """{ "tools": ["breadcrumb"] }""");

        ToolchainOverrideFile.EnsureFilesExist([_dir]);

        Assert.Equal(["breadcrumb"], ToolchainOverrideFile.ResolveForDirectory(_dir));
    }

    [Fact]
    public void EnsureFilesExist_HandlesMultipleDirectoriesAtAnyDepth()
    {
        var nested = Path.Combine(_dir, "blog", "2026");
        Directory.CreateDirectory(nested);

        ToolchainOverrideFile.EnsureFilesExist([_dir, nested]);

        Assert.True(File.Exists(Path.Combine(_dir, ".toolchain.json")));
        Assert.True(File.Exists(Path.Combine(nested, ".toolchain.json")));
    }

    [Fact]
    public void ResolveForDirectory_ReturnsEmpty_WhenFileMissing()
    {
        Assert.Empty(ToolchainOverrideFile.ResolveForDirectory(_dir));
    }

    [Fact]
    public void ResolveForDirectory_ReturnsDeclaredToolsInOrder()
    {
        File.WriteAllText(Path.Combine(_dir, ".toolchain.json"), """{ "tools": ["breadcrumb", "return-to-blog"] }""");

        Assert.Equal(["breadcrumb", "return-to-blog"], ToolchainOverrideFile.ResolveForDirectory(_dir));
    }

    [Fact]
    public void ResolveForDirectory_DoesNotInheritFromParent()
    {
        var child = Path.Combine(_dir, "characters");
        Directory.CreateDirectory(child);
        File.WriteAllText(Path.Combine(_dir, ".toolchain.json"), """{ "tools": ["breadcrumb"] }""");

        Assert.Empty(ToolchainOverrideFile.ResolveForDirectory(child));
    }
}
