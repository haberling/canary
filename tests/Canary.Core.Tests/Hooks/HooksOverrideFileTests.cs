using Canary.Core.Hooks;

namespace Canary.Core.Tests.Hooks;

public class HooksOverrideFileTests : IDisposable
{
    private readonly string _dir;

    public HooksOverrideFileTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "canary-hooksoverride-tests-" + Guid.NewGuid());
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
    public void EnsureFilesExist_CreatesEmptyHooksFile()
    {
        HooksOverrideFile.EnsureFilesExist([_dir]);

        var path = Path.Combine(_dir, ".hooks.json");
        Assert.True(File.Exists(path));
        Assert.Empty(HooksOverrideFile.ResolveForDirectory(_dir));
    }

    [Fact]
    public void EnsureFilesExist_DoesNotOverwriteExistingFile()
    {
        var path = Path.Combine(_dir, ".hooks.json");
        File.WriteAllText(path, """{ "hooks": ["breadcrumb"] }""");

        HooksOverrideFile.EnsureFilesExist([_dir]);

        Assert.Equal(["breadcrumb"], HooksOverrideFile.ResolveForDirectory(_dir));
    }

    [Fact]
    public void EnsureFilesExist_HandlesMultipleDirectoriesAtAnyDepth()
    {
        var nested = Path.Combine(_dir, "blog", "2026");
        Directory.CreateDirectory(nested);

        HooksOverrideFile.EnsureFilesExist([_dir, nested]);

        Assert.True(File.Exists(Path.Combine(_dir, ".hooks.json")));
        Assert.True(File.Exists(Path.Combine(nested, ".hooks.json")));
    }

    [Fact]
    public void ResolveForDirectory_ReturnsEmpty_WhenFileMissing()
    {
        Assert.Empty(HooksOverrideFile.ResolveForDirectory(_dir));
    }

    [Fact]
    public void ResolveForDirectory_ReturnsDeclaredHooksInOrder()
    {
        File.WriteAllText(Path.Combine(_dir, ".hooks.json"), """{ "hooks": ["breadcrumb", "return-to-blog"] }""");

        Assert.Equal(["breadcrumb", "return-to-blog"], HooksOverrideFile.ResolveForDirectory(_dir));
    }

    [Fact]
    public void ResolveForDirectory_DoesNotInheritFromParent()
    {
        var child = Path.Combine(_dir, "characters");
        Directory.CreateDirectory(child);
        File.WriteAllText(Path.Combine(_dir, ".hooks.json"), """{ "hooks": ["breadcrumb"] }""");

        Assert.Empty(HooksOverrideFile.ResolveForDirectory(child));
    }
}
