namespace Canary.Core.Serve;

// Watches a site's source tree and invokes a debounced callback when
// something changes, passing along which file(s) changed so the caller can
// decide whether a targeted rebuild is possible (see SiteBuilder's
// changedPaths parameter and PLAN.md's "Incremental builds" section).
//
// Only a plain edit (FileSystemWatcher's Changed event) to an existing file
// contributes to that set -- a Created/Deleted/Renamed event anywhere in the
// same debounce window means something structural happened (a page added or
// removed, a directory renamed, etc.) that could change more than just one
// route, so the callback receives null instead ("do a full rebuild") rather
// than SiteWatcher trying to reason about what a structural change actually
// affects. Knows nothing about what the callback does (SiteBuilder,
// CanaryConfig) beyond that.
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
    private readonly Action<IReadOnlySet<string>?> _onChanged;
    private readonly TimeSpan _resumeGrace;
    private readonly object _pendingLock = new();
    private readonly HashSet<string> _pendingChangedPaths = new(StringComparer.OrdinalIgnoreCase);
    private bool _pendingForcesFullRebuild;

    public SiteWatcher(string siteRoot, Action<IReadOnlySet<string>?> onChanged, TimeSpan? debounce = null, TimeSpan? resumeGrace = null)
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

        void OnChangedEvent(object sender, FileSystemEventArgs e)
        {
            lock (_pendingLock)
            {
                _pendingChangedPaths.Add(e.FullPath);
            }
            RestartDebounce();
        }

        void OnStructuralEvent(object sender, FileSystemEventArgs e)
        {
            lock (_pendingLock)
            {
                _pendingForcesFullRebuild = true;
            }
            RestartDebounce();
        }

        _watcher.Changed += OnChangedEvent;
        _watcher.Created += OnStructuralEvent;
        _watcher.Deleted += OnStructuralEvent;
        _watcher.Renamed += OnStructuralEvent;
    }

    private void RestartDebounce()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private void RunGuarded()
    {
        _watcher.EnableRaisingEvents = false;
        try
        {
            IReadOnlySet<string>? changedPaths;
            lock (_pendingLock)
            {
                changedPaths = _pendingForcesFullRebuild ? null : new HashSet<string>(_pendingChangedPaths, StringComparer.OrdinalIgnoreCase);
                _pendingChangedPaths.Clear();
                _pendingForcesFullRebuild = false;
            }
            _onChanged(changedPaths);
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
