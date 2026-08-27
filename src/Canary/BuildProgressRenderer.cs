using System.Threading;
using Canary.Core.Build;

namespace Canary;

// Live terminal renderer for a build: consumes Canary.Core.Build.
// IBuildProgress events and draws a single self-overwriting status line --
// the current phase, or (while rendering) a page's full pipeline
// ("post.md -> tool1 -> tool2 -> post.html") with whichever stage is
// currently executing bracketed -- plus a spinner that keeps animating
// between events. Needed because the actual build work is synchronous and
// blocking (file I/O, waiting on a tool's process) and wouldn't otherwise
// move on screen between one progress event and the next.
//
// Falls back to plain sequential lines -- no spinner, no carriage-return
// redraw -- when stdout isn't an interactive terminal (see
// Console.IsOutputRedirected): a `> build.log` redirect or a CI runner
// needs readable text, not raw \r characters mid-line.
internal sealed class BuildProgressRenderer : IBuildProgress, IDisposable
{
    private static readonly char[] SpinnerFrames = ['|', '/', '-', '\\'];

    private readonly bool _live;
    private readonly Timer? _timer;
    private readonly object _gate = new();
    private readonly List<(string Phase, TimeSpan Elapsed)> _phaseTimings = [];

    private string _status = "Starting...";
    private int _frame;
    private int _lastLineLength;
    private bool _disposed;

    public IReadOnlyList<(string Phase, TimeSpan Elapsed)> PhaseTimings => _phaseTimings;

    public BuildProgressRenderer()
    {
        _live = !Console.IsOutputRedirected;
        if (_live)
        {
            _timer = new Timer(_ => Redraw(), null, 0, 90);
        }
    }

    public void PhaseStarted(string phase)
    {
        SetStatus($"{phase} ...");
        if (!_live)
        {
            Console.WriteLine($"{phase} ...");
        }
    }

    // Printed as part of the final summary (see RunOneBuild), not here --
    // the live line only ever shows what's happening RIGHT NOW, not a
    // scrolling history of finished phases.
    public void PhaseFinished(string phase, TimeSpan elapsed)
    {
        lock (_gate)
        {
            _phaseTimings.Add((phase, elapsed));
        }
    }

    public void RenderStageChanged(IReadOnlyList<string> chain, int activeIndex)
    {
        var rendered = string.Join(" -> ", chain.Select((s, i) => i == activeIndex ? $"[{s}]" : s));
        SetStatus(rendered);
        if (!_live)
        {
            Console.WriteLine($"  {rendered}");
        }
    }

    public void PageFinished(string outputDisplayName, bool written)
    {
        // Live mode already showed this page's chain as it rendered (see
        // RenderStageChanged); this only adds a line in non-live/log mode,
        // and only for pages that actually changed -- an unchanged page
        // (PageBuilder's own content-diff short-circuit) isn't news.
        if (!_live && written)
        {
            Console.WriteLine($"  wrote {outputDisplayName}");
        }
    }

    private void SetStatus(string status)
    {
        lock (_gate)
        {
            _status = status;
        }
        if (_live)
        {
            Redraw();
        }
    }

    private void Redraw()
    {
        string status;
        char frame;
        lock (_gate)
        {
            status = _status;
            frame = SpinnerFrames[_frame++ % SpinnerFrames.Length];
        }

        var line = $"{frame} {status}";
        var padded = line.Length < _lastLineLength ? line.PadRight(_lastLineLength) : line;
        Console.Write("\r" + padded);
        _lastLineLength = line.Length;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer?.Dispose();
        if (_live && _lastLineLength > 0)
        {
            Console.Write("\r" + new string(' ', _lastLineLength) + "\r");
        }
    }
}
