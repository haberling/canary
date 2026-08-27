namespace Canary.Core.Build;

// Optional progress sink threaded through SiteBuilder.Build so a caller (the
// CLI) can render live status -- a spinner, a per-page tool chain, phase
// timing -- without Canary.Core knowing anything about terminals or ANSI
// codes. Every method is a plain notification; SiteBuilder.Build behaves
// identically whether or not one is given (defaults to NullBuildProgress),
// so nothing in Canary.Core, and no existing test, depends on there being a
// live listener.
public interface IBuildProgress
{
    void PhaseStarted(string phase);
    void PhaseFinished(string phase, TimeSpan elapsed);

    // One page's full pipeline, known upfront (toolchain resolution happens
    // before any tool runs): chain[0] is the source file's name, chain[^1]
    // is the rendered output's display name, and anything between is a
    // toolchain tool name in declared order -- e.g.
    // ["bin-packing.md", "blog-list-generator", "clear-metadata",
    // "bin-packing.html"]. activeIndex is whichever stage is currently
    // executing (a tool name while that tool's process runs, chain[^1]
    // while markdown is being rendered/written). Fired once per stage
    // transition, not on a timer -- a live renderer ticks its own spinner
    // frame independently between calls.
    void RenderStageChanged(IReadOnlyList<string> chain, int activeIndex);

    void PageFinished(string outputDisplayName, bool written);
}

public sealed class NullBuildProgress : IBuildProgress
{
    public static readonly NullBuildProgress Instance = new();
    private NullBuildProgress() { }

    public void PhaseStarted(string phase) { }
    public void PhaseFinished(string phase, TimeSpan elapsed) { }
    public void RenderStageChanged(IReadOnlyList<string> chain, int activeIndex) { }
    public void PageFinished(string outputDisplayName, bool written) { }
}
