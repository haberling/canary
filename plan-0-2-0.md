# Canary 0.2.0 — release plan

Rebranded from an initial "0.1.1 patch plan": most of what's here (`--clean`, `canary tools build`, `canary explore`) is new CLI surface, not just fixes, which calls for a minor version bump rather than a patch under normal semver conventions.

## Bug: widget/shell template doc-comments leak into every rendered page

Found while migrating consolandWebsite to Canary 0.1.0 (see that repo's own migration). `content/games/Tesselate.md` uses both built-in widgets; rendering it and reading the generated `docs/games/Tesselate/index.html` showed the *entire* explanatory `<!-- ... -->` header comment from `runtime/dist/widgets/downloads.html` and `slideshow.html` — the "Standard widget contract... A site-root-relative url:..." prose, verbatim, twice — sitting in the page's actual output, once per widget instance on the page.

It's invisible to a visitor (browsers don't render HTML comments), so no site is visually broken by this. **Severity, sized up:** low — no functional or security impact, nothing a normal visitor ever sees. The real cost is page weight proportional to widget usage per page, plus a fixed cost on *every* page via `shell.html` (unconditionally re-stamped on every build), and — the sharper part — it ships raw internal engineering commentary (class names, historical asides like "this bit the shell.html chrome template earlier in the project the same way") into the public HTML source of anyone else's site built with Canary. For a framework whose whole product is the pages it generates, that's a polish/credibility paper cut worth fixing given how cheap the fix is, but not urgent.

Confirmed still current against the 0.1.0 code as of this writing: both `TemplateWidgetRenderer.Render` and `SiteBuilder.LoadShellTemplate` still read raw file text straight into templating/substitution with zero preprocessing, and `downloads.html`/`slideshow.html`/`shell.html` still carry the exact header-comment prose described above, caveat included.

### Root cause

Neither template-loading path strips comments before handing the raw file text to the templater:

- `Canary.Core/Widgets/TemplateWidgetRenderer.cs:23` — `File.ReadAllText(_templatePath)` piped straight into `MustacheTemplate.Render`, no preprocessing.
- `Canary.Core/Build/SiteBuilder.cs`'s `LoadShellTemplate` — same pattern for `shell.html`, consumed by `PageBuilder.BuildPage`'s `.Replace(...)` chain.

Every one of Canary's own template files — `runtime/dist/widgets/{downloads,slideshow}.html`, `templates/default/shell.html`, and this project's own `docsite/shell.html` — carries exactly this kind of leading doc-comment, specifically *because* their own comments warn "don't write a literal `{{...}}` or placeholder tag in here, it'll get executed/replaced for real." That warning is itself the symptom: comments are treated as live template body, not stripped documentation.

So this isn't a downloads/slideshow-only bug — it's systemic to the whole "just read the file and template-substitute" path, and it will bite every site consuming these templates, not just consoland's.

**Second instance of the same bug, not in the original report:** `slideshow.html` (and presumably `downloads.html`) also carries a `<!--clipboard ... -->` block — a machine-readable usage example `WidgetClipboardExample.Extract` (`Canary.Core/Widgets/WidgetClipboardExample.cs`) pulls out for the `canary widget <name>` command. It's just another `<!-- -->` span to the templater, so it's leaking into rendered output today too, via the exact same code path. Two things fall out of that:
- It confirms the fix should strip unconditionally (already this doc's leaning answer to the open question below) — there's no comment block in these templates an author actually wants surviving into output, not even the one with a real programmatic purpose.
- No conflict with `canary widget`: `WidgetClipboardExample.Extract` does its own independent `File.ReadAllText(templatePath)` directly from disk, never routed through `TemplateWidgetRenderer.Render`. Stripping comments at render time doesn't touch it — it keeps reading the un-stripped file straight off disk, same as always.

### Fix

Strip HTML comments (`<!--[\s\S]*?-->`, non-greedy) from a template's raw text once, right after it's read from disk and before any placeholder/Mustache substitution runs. One shared helper, not duplicated logic — `Canary.Core/Templating/` is the natural home (already holds `MustacheTemplate.cs` and `UrlResolver.cs`, both template-adjacent transforms), e.g. a small static `TemplateComments.Strip(string)`:

- In `TemplateWidgetRenderer.Render`, between `File.ReadAllText` and `MustacheTemplate.Render`.
- In `SiteBuilder.LoadShellTemplate` (or wherever the shell template string is produced), before `PageBuilder` does its `.Replace` chain.

Comment-stripping in one place, applied uniformly to both template kinds — same "no special-casing" principle the widget system already holds itself to (see `PLAN.md`'s widget-controversy section).

**Side benefit:** once comments never reach the templater, the "don't write literal `{{tag}}` syntax inside this comment" caveat in `downloads.html`, `slideshow.html`, and `shell.html`'s own doc comments becomes moot — those comments can say whatever they want, including the literal syntax they're currently dancing around not writing. Worth simplifying those doc comments as a follow-up once the strip lands, not blocking the fix itself.

**Open question, not blocking:** should comment-stripping be unconditional, or should a template be able to opt out (e.g. a site author who genuinely wants an HTML comment preserved in their own `shell.html`)? Leaning toward unconditional — these are build-time templates, not authored page content, and nothing about the current design gives a template author a way to say "keep this one." Revisit only if a real case shows up.

### Verification

- Rebuild consolandWebsite (`canary build` from that repo) and confirm `docs/games/Tesselate/index.html` no longer contains the leaked comment text, while the slideshow/downloads widgets still render and function identically.
- Rebuild Canary's own `docsite` (`canary build --config docsite/canary.json`) and confirm its `WIDGETS.md`-derived pages (which demo `downloads`/`slideshow`/`code`) lose the same leaked comments.
- Spot-check a widget that legitimately needs `{{`/`{{#}}`/`{{^}}` syntax outside of a comment still renders correctly — comment-stripping must only remove `<!-- -->` spans, not touch real Mustache tags living in the template body.
- `canary widget slideshow` (and `downloads`) still prints its clipboard example correctly post-fix — confirms `WidgetClipboardExample.Extract`'s independent read path was in fact unaffected, not just assumed to be.

## Bug: build never prunes stale output for removed/renamed content

Found while reviewing consolandWebsite's homepage changes: `content/games/draft-idea.md` was deleted from source, but `docs/games/draft-idea/index.html` stayed on disk — no nav entry, no sitemap entry, no source file, yet still a live, publicly reachable page until someone notices and deletes it by hand. Same pattern would hit a renamed slug (old path's output orphaned, new path's output added alongside it).

### Root cause

`SiteBuilder.Build` (`Canary.Core/Build/SiteBuilder.cs:32-33`) only ever creates the output directory (`Directory.CreateDirectory(outputRoot)`) and writes/overwrites files for whatever content currently exists — there's no step that enumerates existing output and removes entries with no corresponding source page. The builder is purely additive.

### Requirement (supersedes the auto-diff fix originally sketched here)

Automatic diff-and-prune (compute the route set, delete anything under `outputRoot` not in it) was the first idea, but it requires a solid definition of "builder-owned output" to avoid silently deleting something the builder doesn't own — and it's implicit: it changes what a plain `canary build` does with no signal to the author it happened. Going with an explicit, opt-in flag instead, consistent with how Canary already prefers author-opt-in over inferred behavior elsewhere:

- New `--clean` flag on `canary build` (parsed the same way `init`/`docs` parse `--force`, via `HasFlag(args, "--clean")` in `Program.cs`'s `Main`, threaded into `RunBuild`).
- When passed, before building: recursively delete the *entire* `outputRoot` (`Path.Combine(siteRoot, config.Output.Dir)`), then proceed with a normal build, which recreates it from scratch via the existing `Directory.CreateDirectory(outputRoot)` call in `SiteBuilder.Build`. Whole-directory wipe, not selective pruning — simpler, and it's what "clean" means for most build tools (cargo clean, npm's rimraf dist, etc.).
- **Must prompt for confirmation before deleting anything**, reusing the existing `PromptYesNo` helper already in `Program.cs` (used by `init`'s prompts). Explainer text should name the exact path about to be wiped, e.g.:
  `About to delete <outputRoot> and everything in it before rebuilding. Continue?`
- **Default answer is No.** This is a destructive, irreversible-by-Canary operation (no undo, no recycle-bin semantics), so on a non-interactive invocation (piped/no TTY, where `Console.ReadLine()` returns null and `PromptYesNo` falls back to its default) it must *not* proceed silently. This is the opposite default from `init`'s prompts, which default toward convenience, not caution.
- **If the user answers No, cancel the build entirely** — don't fall back to a normal (non-clean) build. `RunBuild` returns early (same shape as its existing config-load-failure early return), no `SiteBuilder.Build` call happens at all, exit code should reflect a cancelled/non-zero run distinctly from a genuine build failure if that distinction is worth making (open question below).

### Open questions, not blocking

- Should `--clean` also apply to `canary serve` (which calls `RunOneBuild` on startup) or `canary publish` (which always builds first)? Leaning toward build-only for now — `serve` rebuilds continuously on file changes, so a clean-then-serve doesn't fit the same "one-shot, confirm, proceed" shape.
- Does a non-interactive/CI caller need a way to pre-confirm (e.g. a `--yes` flag, or `--clean` auto-confirming when stdin isn't a TTY)? Not requested — deliberately left out until someone actually needs `--clean` in a non-interactive pipeline.
- Exit code on user-declined cancellation: same `1` as other refusals (`RunInit`'s early refusal, config-load failure) or a distinct code to let scripts tell "you said no" apart from "the build broke"? Leaning toward reusing `1` unless a caller shows up that needs to tell them apart.

### Verification

- `canary build --clean` on a site with existing output prompts with the exact output path, answering `y` deletes and rebuilds cleanly.
- Answering `n` (or plain Enter, given the No default) leaves the existing output directory completely untouched and prints/exits without building.
- `canary build` (no `--clean`) behaves exactly as it does today — no prompt, no deletion, purely additive as before.
- Delete a content page, run `canary build --clean` and confirm, verify its old output directory is gone (this is still the underlying scenario `--clean` exists to let an author fix, just manually invoked rather than automatic).
- Non-interactive invocation (stdin redirected from `/dev/null` or closed) with `--clean` does not delete anything and does not build.

## Experiment: persistent toolchain-tool workers, to cut per-page process-spawn overhead

**Not a commitment — flagged explicitly as an experiment to try, not a fix to ship.** Surfaced while investigating why consolandWebsite's build times had grown noticeably since checksum-gating came out (Act 13): a full `canary build` there now costs ~9-10s warm, ~17s with a cold `dotnet run` cache, for a 13-route site. `ToolchainRunner.Execute` (`Canary.Core/Toolchain/ToolchainRunner.cs`) spawns a brand-new OS process via `Process.Start` for every tool name in a page's `.toolchain.json` chain, on every page, on every build — never reused, never batched. Timed directly against this repo's own tools, a single warm `dotnet run tools/clear-metadata.cs` costs ~350-450ms just to start/JIT/exit, before the tool does any real work. `content/blog/blog-archive/.toolchain.json` chains three tools, so that's three fresh process spawns per archived blog page, every build.

**Hypothesis:** most of that cost is pure process-startup overhead, not the tool's actual logic (these tools are trivially small — `curtain.cs`/`example.cs` are six and seven lines). If a tool's process were started once per `canary build` and kept alive across every page that uses it, instead of once per page-tool-pair, that startup cost gets paid once instead of N times.

### Recommended near-term direction: precompiled C#-only tools with an advisory staleness warning

Settled on after weighing this against the alternatives below (persistent workers, batching, and an in-process-DLL option that was considered and rejected — see those sections). This is deliberately the smallest change that addresses the actual measured cost, and it's scoped to C# only for now (F# was considered and dropped — see "Options considered and rejected" below).

**Why not an automatic rebuild-if-stale cache instead of a warning:** this project has already run that experiment once, on a different subsystem, and pulled it back out. `devJournal.md`'s Act 13 covers Canary's old per-page checksum-gating (a content hash embedded in output HTML, used to skip re-rendering unchanged pages) being removed project-wide after becoming, in the user's own words at the time, *"an edge-case monster"* — replaced with `PageBuilder`'s current approach of always redoing the work and comparing actual output against disk. An mtime-based "recompile if stale" cache for tool binaries is the same shape of mechanism (validity depends on correctly enumerating every input that could invalidate it) applied to a different subsystem, and mtime is if anything a shakier signal than the content-checksum that already lost this argument — git checkouts and CI clones routinely stamp files with a checkout time unrelated to actual edit history, and a change to a tool's *referenced* code (a shared helper, a bumped package) wouldn't touch the tool's own source file's mtime at all, so a binary could look fresh while silently running stale logic with no equivalent of `PageBuilder`'s "compare rendered output to reality" safety net. So: **advisory only, never authoritative** — Canary always runs whatever binary is currently on disk; it can tell an author it looks stale, but it never decides that for them. Matches this project's own stated throughline (explicit beats implicit, `devJournal.md`'s retrospective section) better than a smarter cache would.

**Schema:** `CanaryConfig.Tools` (`Canary.Core/Config/CanaryConfig.cs:17`) is `Dictionary<string, string>` today — name maps straight to a bare command string, and that stays fully supported unchanged (still the right shape for a manually-run tool, including a plain `dotnet run tools/x.cs` entry someone doesn't want to opt into any of this for — nothing here removes or discourages that path). A registry entry can *additionally* be an object instead of a bare string: `{ "command": "tools/bin/clear-metadata.exe", "source": "tools/clear-metadata.cs" }`. `command` is resolved and run exactly as today, unchanged — `ToolchainRunner.Execute` never becomes aware of how the command came to exist, so the "arbitrary external command" execution path stays exactly as language-agnostic as it is now. `source`, when present, is what opts a tool into the two new behaviors below; a tool author who doesn't add it gets neither, with zero behavior change. (Needs a custom `JsonConverter` for the tool-entry value type, registered against the source-generated `CanaryJsonContext`, so both the bare-string and object shapes deserialize correctly — a real schema change, not just an internal refactor. If a persistent-worker mode is ever pursued later, its `mode: "worker"` field belongs on this same object shape alongside `command`/`source`, not as a second, incompatible object form.)

**`canary tools build [<name>]`:** new command. Walks `config.Tools`, and for every entry that's the object form with a `source` ending in `.cs`, runs `dotnet publish <source>` (targeting .NET 10's file-based-apps feature — a bare `.cs` file, no `.csproj` needed, which is what this repo's own tools already are) with output directed to wherever that entry's own `command` path points — reusing `command` as the single source of truth for output location rather than inventing a second directory convention to keep in sync. Entries that are plain strings, or object entries with no `.cs` source, are skipped — nothing to build. `<name>` optional, to build one tool instead of every buildable one; omitted builds all of them. This command is the one place the "no special-casing by language" principle is deliberately, visibly set aside — but only here: it's an opt-in developer-convenience command for authors of buildable (today: C#-only) tools, and it has no bearing on how `ToolchainRunner` runs anything during a real `canary build`.

**Missing binary:** if `command`'s path doesn't exist at all (fresh clone, `canary tools build` never run), that's not a staleness case — `ToolchainRunner.Execute`'s `Process.Start` already fails this today as a hard `InvalidOperationException`, which is the right behavior (a build shouldn't silently continue without a tool it needs). Only the message needs improving: when the failing entry has a `source` field, name `canary tools build <name>` in the error instead of just reporting the generic process-start failure.

**Staleness warning:** once per `canary build` (not once per page — checked up front against every buildable registry entry right after config load, independent of route count, so a tool used on 50 pages doesn't print 50 copies of the same warning), compare `File.GetLastWriteTimeUtc(source)` against the resolved binary's. If source is newer, print a non-blocking warning naming the tool and suggesting `canary tools build`, then proceed using whatever's currently on disk, unmodified.

### Options considered and rejected

**F# tools, dropped for now.** F# is a plausible fit for this job on paper — these tools are pure text-in/text-out transforms over markdown/YAML, which suits F#'s pipeline-and-pattern-matching style, and it's a full member of the dotnet family so `dotnet publish`/Native AOT apply equally. But it doesn't have C#'s bare-file story: .NET's file-based-apps feature (`dotnet run`/`publish app.cs`, no project file) is C#-only through .NET 11 Preview 3, with F# support only an open community proposal (`fsharp/fslang-suggestions` #1442), not shipped. A buildable F# tool would need a real `.fsproj`, not a lone script — a second, non-uniform "source" shape to support (a file for C#, a project for F#) for a language nobody's asked to use here yet. `dotnet fsi script.fsx` remains F#'s "just run it manually" option and needs nothing from Canary to keep working (any plain string command already works) — but it's worth noting FSI is a REPL host even in non-interactive script mode, likely *more* per-invocation overhead than `dotnet run tools/x.cs`, not less, so it's not a lower-friction fallback than what C# already has. Dropped from scope; revisit only if an actual F# tool shows up.

**In-process tool DLLs, rejected outright.** The fastest option on paper: build a tool as a `.dll`, load it into Canary's own process (`Assembly.LoadFrom` + a defined interface), call it directly with no OS process at all — zero spawn cost, and JIT gets amortized across a whole build the same way a persistent worker would, without needing a framing protocol. Rejected for three compounding reasons, the first of which is close to disqualifying on its own: it's in direct conflict with `Canary.csproj`'s `PublishAot=true` (`win-x64` native distribution) — a fully-AOT'd process carries no JIT, so there's no way to dynamically load and execute arbitrary IL at runtime in the real distributed build; it would only work under `dotnet run`, silently missing from the packaged install. It also collapses process isolation (a crashing/hung in-process tool can take down the whole host — for `canary serve` that's the entire dev server, not just one rebuild, unlike today where a bad tool only ever kills its own subprocess). And it's categorically .NET-only in a way even the F#-vs-C# split isn't — it forks the toolchain into "real" external-command tools in any language and a fast-path in-process club only C# can join, a bigger philosophical break from "arbitrary external command, no special-casing by language" than anything else considered here.

**Why persistent workers still need more thought before committing (see below):** the narrower fix above needs zero changes to Canary itself and directly targets what was actually measured (process/CLR-startup overhead), but only because the tools happen to be written in C# — it does nothing for a hypothetical slow-starting Python or Node tool, and leaves the toolchain's actual execution model exactly as lopsided as it is today. A persistent-worker model would fix the cost symmetrically for any language, which is the more principled fix given the toolchain's core "arbitrary external command, no special-casing by language" stance (see PLAN's history in `devJournal.md`, Acts 6/14/17) — but it comes at a real cost to that same stance's other virtue: today a tool is "read all of stdin, write all of stdout, exit," trivially simple and stateless by construction. A persistent worker needs:

- A framing protocol over stdin/stdout so the worker knows where one page's request ends and the next's response begins (length-prefixed or delimited — something LSP-shaped).
- Tool authors to actively avoid leaking state between pages, instead of getting that for free from process exit.
- A defined failure mode for a worker crashing or misbehaving mid-build. Today's blast radius is already "the rest of the build" — `SiteBuilder.BuildPrerendered`'s route loop has no per-route try/catch, so a tool's non-zero exit on page 9 already aborts pages 10+ for that run, same as a worker model would. What a worker model adds is *harder to attribute*, not a wider halt: a crash can depend on state accumulated from earlier pages in the same process (a leak, an unbounded cache) rather than being reproducible standalone from page 9's markdown alone the way a fresh-process failure always is; a hang can become load-dependent (surfaces after N requests) instead of input-dependent; and a bug in the request/response framing itself can silently misattribute or corrupt one page's output using another's, with no crash or error raised at all — a failure class that doesn't exist today, since a one-shot process's entire stdout *is* the page's response by construction.

**Approach to prototype:** an opt-in per-tool flag in the `tools` registry (e.g. a tool entry becomes a `{ command, mode: "worker" }` object instead of a bare string, defaulting to today's one-shot behavior for anything that doesn't opt in) — so this never becomes mandatory complexity for a tool author who just wants the current dead-simple contract. `ToolchainRunner` would spawn a `mode: "worker"` tool once at the start of a build, keep the process handle for the build's lifetime, and send each applicable page's markdown as one framed request, reading back one framed response, instead of spawning fresh each time.

Also flagged, not yet resolved: `canary serve` calls `RunOneBuild` → a fresh `SiteBuilder().Build(...)` on every file-change rebuild. Does a worker respawn every rebuild (losing most of the benefit for exactly the iterative-dev workflow where build speed matters most), or persist across rebuilds within one `serve` session (worsening the state-leak risk above — now leaking across whole rebuilds, not just pages within one build, and needing its own "restart if source changed" logic)? Needs an answer before this moves past "experiment."

### Verification, for the recommended near-term direction

- `canary tools build` on a site with a `source`-bearing entry produces the binary at the entry's `command` path; `canary tools build clear-metadata` builds only that one.
- Edit a tool's `.cs` source, run `canary build` without rebuilding it — confirm the advisory warning appears exactly once (not once per page using that tool) and the build still completes using the old binary.
- Run `canary tools build`, then `canary build` again — confirm the warning is gone.
- Delete a tool's compiled binary entirely and run `canary build` — confirm a hard failure naming `canary tools build <name>`, not a silent skip or a generic process-start error.
- A plain-string tool entry (e.g. a manual `dotnet run tools/x.cs`) — confirm zero behavior change: no warning, no build-command involvement, runs exactly as it does today.

**Verification, if the worker-mode experiment above is prototyped:**
- Re-run the same before/after timing test already done against consolandWebsite (`canary build` warm / cache-cleared / re-warmed) with a worker-mode `clear-metadata` and/or `blog-list-generator`, and compare wall-clock against the current one-shot numbers (~9-10s warm baseline).
- Confirm a worker-mode tool crashing mid-build produces a clear, attributable error (which page, which tool) rather than a silent hang or a misattributed/corrupted response on an unrelated page — deliberately test this by forcing a crash partway through a multi-page run, not just on the first request, so cross-request state effects have a chance to show up.
- Confirm a one-shot tool and a worker-mode tool can coexist in the same `.toolchain.json` chain with no behavior change to the one-shot tool.

## Feature: usage/help output wraps badly, even on a reasonably wide console

`PrintUsage()` (`src/Canary/Program.cs:701-713`) is what a user sees any time the CLI can't parse what they gave it — no args at all, or an unrecognized command (`Main`'s `default:` case both call it before returning 1). Right now several of its lines are hardcoded single `Console.WriteLine` strings well over 150-200 characters (the `init` and `docs` lines especially), so they wrap mid-word on essentially any real terminal, not just narrow ones. Because the wrap points are wherever the terminal happens to break, not chosen, the "column" alignment between a command's usage syntax and its description (currently faked with manually-counted spaces) falls apart the moment a line wraps — the result reads as a ragged block, not a table.

### Root cause

Each command's help line is one hand-formatted string: invocation syntax, manual space-padding to fake a column, then a description, all concatenated with no wrapping logic. It assumes the whole line fits on one row regardless of actual console width, and the padding is brittle (recount-by-hand any time a command name or its syntax changes length).

### Fix

Restructure `PrintUsage` around structured data instead of hand-formatted strings:
- A small list of `(usage, description)` pairs, one per command, instead of one pre-baked `Console.WriteLine` string each.
- Compute the usage-column width from the longest `usage` entry, so every command's description lines up in a real column instead of eyeballed spaces.
- Word-wrap each `description` to fit the remaining width (`Console.WindowWidth - usageColumnWidth`), breaking on word boundaries, with continuation lines indented to the description column so a wrapped multi-line description still reads as one aligned block.
- `Console.WindowWidth` throws/isn't meaningful when output isn't an interactive console (redirected to a file, piped, CI) — fall back to a fixed width (80) in that case rather than crashing or producing a zero/garbage wrap width.

### Open questions, not blocking

- Should this become a general "wrap any Canary console output to terminal width" helper, given other commands (`RunInit`'s warnings, error messages) could hit the same problem? Scoping to just `PrintUsage` for 0.1.1 unless a second real case shows up.
- Worth trimming the descriptions themselves (shorter text) in addition to wrapping better? Leaning toward wrapping first and seeing if the result is actually readable before also rewriting copy.

### Verification

- Run `canary` with no args, and `canary bogus-command`, in a normal terminal (~120 cols) — confirm no mid-word wraps and the usage/description columns visibly align.
- Resize the terminal narrower and re-run — confirm wrapping adapts rather than producing the same fixed broken layout regardless of width.
- Redirect output to a file (`canary > out.txt`) or run under a non-interactive/CI shell — confirm it doesn't throw on `Console.WindowWidth` and produces a sane fixed-width fallback.

## Feature: `canary explore` — interactive tree view of nav structure and toolchain assignment

Motivation: today, seeing "what tools run on this folder" or "what does the nav tree actually look like" means either reading `.toolchain.json`/`.nav.json` files by hand across the content tree, or inferring it from a full build's output. For a site with any real depth (consolandWebsite's `content/games/`, `content/blog/blog-archive/`, etc.), there's no quick way to walk down through folders and see either structure without opening files one at a time. `canary explore toolchain` / `canary explore nav` (or `navigation`) should give an interactive, drill-down console tree for each.

### Two distinct trees, not one

These are two different data shapes already in the codebase, not two views of the same tree, and the explorer needs to build/walk them separately rather than pretend they're one thing wearing two hats:

- **`nav`** — `SiteManifest.Nav` (`Canary.Core.Manifest.ManifestBuilder.Build`, `NavItem.Title`/`Path`/`Children`). This is the *curated* tree: capped at `config.Nav.Depth`, entries can be hidden via a directory's `.nav.json` (`nonav`, `allow`/`deny`), and a directory with no landing page and no visible children is omitted entirely. It's what a site visitor's nav menu shows, not what's on disk.
- **`toolchain`** — no existing tree type; needs to be built fresh by walking every content directory recursively (unbounded by `Nav.Depth` — `Canary.Core.Toolchain.ToolchainOverrideFile` and `.toolchain.json` are explicitly non-recursive and apply "at any depth the content tree reaches," per `ManifestBuilder`'s own doc comment on the nav-depth-vs-toolchain distinction), and at each directory that has `.md` files directly inside it, resolving `ToolchainOverrideFile.ResolveForDirectory(dir)` for the tool list that applies there. This deliberately does **not** stop at `Nav.Depth` — the whole point raised is seeing tool assignment "at every folder level," including folders nav wouldn't surface at all (below nav depth, or `nonav`-flagged).

### CLI shape

- `canary explore nav [--config <path>]` (accept `navigation` as a synonym for `nav`, since the user asked for both spellings to work)
- `canary explore toolchain [--config <path>]`
- Bare `canary explore` with no recognized subcommand: print usage for just this command (which trees are available), same spirit as the merged `canary widgets` command's own per-argument usage checks below, not the whole top-level `PrintUsage()`.

### Interactive behavior

A single-screen, keyboard-driven tree, redrawn in place (not scrolling console history) — closest existing precedent in this codebase is none; this is new terminal-UI surface for Canary, built by hand against `System.Console` (`Console.ReadKey(intercept: true)`, `Console.SetCursorPosition`), not a third-party TUI package — consistent with this project's existing preference for owning small infra itself (`StaticFileServer` on a bare `HttpListener` rather than pulling in a web framework) and with `PublishAot`/`win-x64` native packaging, which a dependency would need to prove out AOT-compatibility for first.

- Up/Down (or `j`/`k`): move selection between currently-visible nodes.
- Right/Enter: expand the selected folder node.
- Left: collapse it, or if already collapsed/a leaf, move selection to its parent.
- For `toolchain`: each folder node's label shows its resolved tool list inline (e.g. `blog-archive/  [clear-metadata, blog-list-generator]`, or a visibly-distinct empty marker like `blog-archive/  (no tools)` for a directory with an auto-created-but-empty `.toolchain.json` — don't just show nothing, which reads as "not loaded yet" rather than "genuinely empty").
- For `nav`: each node shows its title and, for a leaf, the nav path it links to; a dropdown-only node with no landing page (path `null`) should read visibly differently from a clickable one (e.g. dim, or a distinct marker) rather than looking identical to a real link.
- `q`/Esc: quit, restoring the terminal to a normal scrolling state (no leftover cursor-positioning weirdness after exit).

### Non-interactive fallback

`Console.ReadKey`/cursor control need a real interactive console. When stdout is redirected or there's no TTY (same condition the `--clean` confirmation prompt and the usage-wrapping feature above both already have to account for), an interactive screen can't run at all — fall back to printing the whole tree flat (full depth, indentation-based, one line per node) and exit, rather than hanging on a `ReadKey` that will never receive input.

### Open questions, not blocking

- Should `explore toolchain` let you drill into *why* a tool is unresolvable (e.g. a name in `.toolchain.json` with no matching `canary.json` registry entry) inline, or just show the raw tool names and let `canary build`'s existing error surface real registry problems? Leaning toward raw names only — keep the explorer read-only and simple, don't duplicate `ToolchainRunner.ResolveCommand`'s validation.
- Worth a third mode that overlays both (nav-visible folders annotated with their toolchain tools in one tree), given they share the same directory backbone? Explicitly starting with two separate flat modes per the ask ("toolchain or navigation/nav") — a combined view is a plausible follow-up, not in scope now.
- Any value in a non-interactive `--print`/`--json` escape hatch for scripting against either tree, independent of the redirected-output fallback above? Not requested; flagging since the fallback above already produces most of a `--print` mode's output for free.

### Verification

- `canary explore nav` on a site with nested content (something like consolandWebsite's `content/games/`) — confirm the displayed tree matches `manifest.json`'s actual `nav` structure, including a dropdown-only entry with no landing page rendering distinctly.
- `canary explore toolchain` on the same site — confirm a folder several levels deeper than `config.Nav.Depth` still appears (proving it isn't nav-depth-limited), and that its `.toolchain.json` tool list matches the file on disk.
- Confirm `canary explore navigation` behaves identically to `canary explore nav`.
- Redirect `canary explore toolchain`'s output to a file — confirm it prints the full flat tree and exits cleanly instead of hanging on keyboard input.
- Confirm `q`/Esc leaves the terminal in a normal, usable state afterward (cursor visible, no stray positioning left over).

## Feature: merge `widgets`/`widget` into one `canary widgets [<name>]` command

Two separate top-level commands for one concept is needless surface area and a naming trap: `canary widgets` (no arg — lists discovered widgets, `RunWidgetsList`) versus `canary widget <name>` (singular — prints one widget's clipboard example, `RunWidgetShow`). Easy to type the wrong one, and nothing about the names themselves signals which one takes an argument.

### Fix

Collapse to one command: `canary widgets [<name>] [--config <path>]`.

- No name → today's list behavior, unchanged (`RunWidgetsList`).
- A name → today's clipboard-example-show behavior, unchanged (`RunWidgetShow`).
- Positional-arg detection reuses the convention `RunInit` already establishes for its own optional positional `targetDir` (`Program.cs:62`): `args.Length > 1 && !args[1].StartsWith("--")` treats `args[1]` as the name rather than a flag. Not a new parsing idiom — the same shape already exists in this file for exactly this "optional positional arg, then flags" case.

Remove the `widget` (singular) case from `Main`'s switch entirely — no backward-compatible alias kept. Pre-1.0, and matches this project's own stated stance on compat shims (`devJournal.md`, Act 13: *"we are still in pre-release, so I appreciate the compatibility advice but I dont think its necessary"*). Update `PrintUsage`'s `widgets`/`widget` lines (`Program.cs:711-712`) to the single merged form.

**Housekeeping:** this doc's own `canary explore` section above cites `widget`'s early-return usage check as CLI-shape precedent (`Program.cs:43-48`) — that citation will need updating once this lands, since the command it points at won't exist under that name anymore.

### Verification

- `canary widgets` with no args still lists all discovered widgets, same output as today.
- `canary widgets slideshow` prints the clipboard example, same output as today's `canary widget slideshow`.
- `canary widget slideshow` (old singular form) now falls through to "Unknown command" — confirms the removal actually took effect rather than silently keeping both names working.
- `canary widgets --config <path>` (no name) still works — confirms flag-vs-positional-name detection doesn't mistake `--config` for a widget name.
- `canary widgets nonexistent-widget` still errors clearly ("No widget named...'"), same as today.

## Docs: rewrite `README.md` for a cold public audience

Canary is public/OSS now (`project status: Alpha is Live`), and the current `README.md` (13 lines: logo, one paragraph, a "split out of a private site" note, a status line, a license) doesn't hold up to a stranger's first look — confirmed unchanged against the existing critique doc below as of this writing.

### Source of truth: `canary-readme-hn-critique.md`

Already sitting in the repo root (untracked) — a full HN-teardown-style critique with line-by-line issues, a suggested skeleton, and a priority-ordered fix list. Not duplicating its content here; the fix is "work through it," not "re-derive README advice from scratch." Its top complaints, confirmed still true against the live `README.md`:

- No install/quick-start path *in the repo* — everything defers to `canary.consoland.net`, so the GitHub page alone can't answer "should I care."
- "No-framework" as a tagline is contradicted by what Canary actually is (client router, widget runtime, CLI, config schema, render modes, toolchain) — the critique's suggested fix is describing what it does, not branding around what it isn't.
- Status language conflicts across surfaces: README says "Alpha is Live... have websites in prod" (also a grammar error: "have" → "has"); the docs site says "Pre-alpha... run from source... no published package yet." The critique's take: pick one and be specific (name the dogfood sites — consoland.net, canary.consoland.net — instead of "websites in prod").
- No code sample anywhere — not even a `canary.json` stub or one markdown-fence widget example.
- Fragile logo path (`src="docsite/img/logo.svg"`, relative-path-dependent on GitHub's rendering).

### Fix

Follow the critique's own priority order (its "Priority order for fixes" section): quick start + .NET requirement first (highest leverage), then align status language across README/docs site, then name the dogfood sites explicitly, then a minimal `canary.json` + markdown example in-repo, then a short "why not Hugo/Eleventy/Astro" positioning paragraph, then the smaller polish items (grammar, logo URL, requirements badge).

**Open question that's genuinely not mine to decide:** the actual status claim. "Alpha is Live" (current README) and "Pre-alpha, run from source" (docs site, per the critique) are contradictory, and only the project owner knows which is factually true right now — not something to silently pick one of while rewriting. Needs a real answer before the new README ships, not a guess.

### Verification

- Someone unfamiliar with the project can follow the README's quick-start commands verbatim, without visiting `canary.consoland.net`, and get a running local site.
- Status language matches between `README.md` and the docs site's Getting Started/Why Canary pages — no contradiction between "live" and "pre-alpha, run from source."
- Dogfood sites are named with real URLs, not "websites in prod."
- Logo renders correctly from a fresh clone view on GitHub (not just locally, where a broken relative path can go unnoticed).
