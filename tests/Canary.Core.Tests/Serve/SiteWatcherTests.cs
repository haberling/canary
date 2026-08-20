using Canary.Core.Serve;

namespace Canary.Core.Tests.Serve;

public class SiteWatcherTests : IDisposable
{
    private readonly string _root;

    public SiteWatcherTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "canary-site-watcher-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Callback_WritingIntoWatchedTree_DoesNotRetriggerItself()
    {
        // Regression test: found by actually running `canary serve` against
        // a real site (see PLAN.md) -- Canary.Core.Manifest.ManifestBuilder
        // rewrites content/manifest.json into the SOURCE tree on every
        // build, and without pausing the watcher around the callback, that
        // write re-triggers the watcher, which triggers another rebuild,
        // forever.
        var callCount = 0;
        var pageMd = Path.Combine(_root, "page.md");
        File.WriteAllText(pageMd, "# Hello");

        using var watcher = new SiteWatcher(
            _root,
            onChanged: _ =>
            {
                Interlocked.Increment(ref callCount);
                // Simulates a build writing back into the watched tree
                // (e.g. manifest.json), the exact behavior that caused the
                // original infinite loop.
                File.WriteAllText(Path.Combine(_root, "manifest.json"), "{}");
            },
            debounce: TimeSpan.FromMilliseconds(50),
            resumeGrace: TimeSpan.FromMilliseconds(50));

        watcher.Start();

        // One real external change.
        File.WriteAllText(pageMd, "# Hello again");

        // Long enough for: debounce (50ms) + callback + resume grace (50ms)
        // + however many self-trigger cycles would have happened if the
        // bug were still present.
        await Task.Delay(1000);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Callback_MultipleEditsInOneWindow_ReceivesOneSetWithBothPaths()
    {
        var pageA = Path.Combine(_root, "a.md");
        var pageB = Path.Combine(_root, "b.md");
        File.WriteAllText(pageA, "# A");
        File.WriteAllText(pageB, "# B");

        IReadOnlySet<string>? received = null;
        var callCount = 0;
        using var watcher = new SiteWatcher(
            _root,
            onChanged: changed =>
            {
                Interlocked.Increment(ref callCount);
                received = changed;
            },
            debounce: TimeSpan.FromMilliseconds(150),
            resumeGrace: TimeSpan.FromMilliseconds(50));

        watcher.Start();

        File.WriteAllText(pageA, "# A changed");
        await Task.Delay(30);
        File.WriteAllText(pageB, "# B changed");

        await Task.Delay(1000);

        Assert.Equal(1, callCount);
        Assert.NotNull(received);
        Assert.Contains(Path.GetFullPath(pageA), received!, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.GetFullPath(pageB), received!, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Callback_CreatedFileInSameWindow_ForcesNullChangedPaths()
    {
        var pageA = Path.Combine(_root, "a.md");
        File.WriteAllText(pageA, "# A");

        IReadOnlySet<string>? received = new HashSet<string> { "sentinel-should-be-overwritten" };
        var callCount = 0;
        using var watcher = new SiteWatcher(
            _root,
            onChanged: changed =>
            {
                Interlocked.Increment(ref callCount);
                received = changed;
            },
            debounce: TimeSpan.FromMilliseconds(150),
            resumeGrace: TimeSpan.FromMilliseconds(50));

        watcher.Start();

        File.WriteAllText(pageA, "# A changed");
        await Task.Delay(30);
        File.WriteAllText(Path.Combine(_root, "new-page.md"), "# New");

        await Task.Delay(1000);

        Assert.Equal(1, callCount);
        Assert.Null(received);
    }
}
