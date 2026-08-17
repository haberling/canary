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
        using var watcher = new SiteWatcher(
            _root,
            onChanged: () =>
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
        File.WriteAllText(Path.Combine(_root, "page.md"), "# Hello");

        // Long enough for: debounce (50ms) + callback + resume grace (50ms)
        // + however many self-trigger cycles would have happened if the
        // bug were still present.
        await Task.Delay(1000);

        Assert.Equal(1, callCount);
    }
}
