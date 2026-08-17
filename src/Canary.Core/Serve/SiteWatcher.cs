namespace Canary.Core.Serve;

// Watches a site's source tree and invokes a debounced callback when
// something changes. Knows nothing about what the callback does
// (SiteBuilder, CanaryConfig) -- only "something changed, wait a beat in
// case more changes are coming, then tell the caller once."
//
// The watcher pauses itself while the callback runs (and for a short grace
// period after), rather than trying to know which paths a rebuild itself
// writes to. Excluding just output.dir isn't enough: Canary.Core.Manifest.
// ManifestBuilder unconditionally rewrites content/manifest.json into the
// SOURCE tree on every single build, so without this guard, every rebuild
// re-triggers the watcher, which triggers another rebuild, forever (found
// by actually running `canary serve` against the workspace/ dogfood site --
// see PLAN.md). Pausing around the callback fixes this in general, without
// SiteWatcher needing to know SiteBuilder's internals.
public sealed class SiteWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounceTimer;
    private readonly Action _onChanged;
    private readonly TimeSpan _resumeGrace;

    public SiteWatcher(string siteRoot, Action onChanged, TimeSpan? debounce = null, TimeSpan? resumeGrace = null)
    {
        _onChanged = onChanged;
        _resumeGrace = resumeGrace ?? TimeSpan.FromMilliseconds(250);

        _debounceTimer = new System.Timers.Timer((debounce ?? TimeSpan.FromMilliseconds(300)).TotalMilliseconds)
        {
            AutoReset = false,
        };
        _debounceTimer.Elapsed += (_, _) => RunGuarded();

        _watcher = new FileSystemWatcher(siteRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
        };

        void OnEvent(object sender, FileSystemEventArgs e)
        {
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        _watcher.Changed += OnEvent;
        _watcher.Created += OnEvent;
        _watcher.Deleted += OnEvent;
        _watcher.Renamed += OnEvent;
    }

    private void RunGuarded()
    {
        _watcher.EnableRaisingEvents = false;
        try
        {
            _onChanged();
        }
        finally
        {
            // Re-enabled after a grace period, not immediately: filesystem
            // change notifications from the build's own writes can arrive
            // slightly after the writes themselves complete, and would
            // otherwise sneak in just after re-enabling and restart the loop.
            _ = ResumeAfterGraceAsync();
        }
    }

    private async Task ResumeAfterGraceAsync()
    {
        await Task.Delay(_resumeGrace);
        _watcher.EnableRaisingEvents = true;
    }

    public void Start() => _watcher.EnableRaisingEvents = true;

    public void Dispose()
    {
        _watcher.Dispose();
        _debounceTimer.Dispose();
    }
}
