# Incremental Builds

Canary used to gate re-rendering behind an embedded checksum comment per page — skip re-rendering if nothing that could affect the page had changed. It needed repeated bug-fix passes (a missed widget file here, a missed tool script there) because "everything that could make a cached result stale" turned out to be a moving target. That approach is gone, replaced by two smaller mechanisms that don't need any invalidation-tracking at all.

## Every page is always fully re-rendered

There's no cache to hit or miss — markdown rendering and any applicable toolchain tools run unconditionally, every build, for every page. What's actually conditional is the disk **write**: since rendering is deterministic, the freshly-rendered HTML is compared against whatever's already on disk at that path, and a file is only written when it actually differs. If you commit `output.dir` to git, this keeps your diffs small and meaningful — they reflect real content changes, not "the build ran again."

## `canary serve` gets its speed a different way

Full re-rendering on every save would make local dev noticeably slower on a large site, so `canary serve` takes a different path: it tracks which file(s) changed during its debounce window. If every changed file is a plain edit to an existing page's own markdown source, only that page gets re-rendered. Anything else — a page created, deleted, or renamed, or a non-content change (a widget, a tool script, a theme file, `canary.jsonc` itself) — falls back to rendering every page, since those could affect more than one page and Canary isn't going to guess which ones.

Site-wide bookkeeping (the manifest, `.toolchain.json` auto-creation, sitemap/robots, asset copies) always runs in full regardless of which path was taken — it's cheap, and skipping it risks the nav tree or sitemap going stale.

## Reading the build summary

`canary build`/`canary serve` report how many pages were written versus left unchanged. "Written" means the file on disk actually changed; "unchanged" means it was computed fresh but happened to come out identical to what was already there — a distinction that, by construction, can never disagree with what `git status` would show you.

## Stale output stays until `--clean`

A plain `canary build` is purely additive. If you delete or rename a source page, its old HTML stays in `output.dir` — the build will not prune it. `canary build --clean` wipes `output.dir` after a confirmation prompt and rebuilds from scratch. See [CLI](reference/cli) for the prompt (default No), the cancel-on-No behavior, and the non-interactive refusal. `--clean` is build-only; it is not on `serve` or `publish`.
