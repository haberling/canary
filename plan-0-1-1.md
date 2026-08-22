# Canary 0.1.1 — patch plan

## Bug: widget/shell template doc-comments leak into every rendered page

Found while migrating consolandWebsite to Canary 0.1.0 (see that repo's own migration). `content/games/Tesselate.md` uses both built-in widgets; rendering it and reading the generated `docs/games/Tesselate/index.html` showed the *entire* explanatory `<!-- ... -->` header comment from `runtime/dist/widgets/downloads.html` and `slideshow.html` — the "Standard widget contract... A site-root-relative url:..." prose, verbatim, twice — sitting in the page's actual output, once per widget instance on the page.

It's invisible to a visitor (browsers don't render HTML comments), so no site is visually broken by this. But it ships real, sizeable dead bytes in every page that uses a widget, and it's needless page-source noise for anyone who views source or scrapes it.

### Root cause

Neither template-loading path strips comments before handing the raw file text to the templater:

- `Canary.Core/Widgets/TemplateWidgetRenderer.cs:23` — `File.ReadAllText(_templatePath)` piped straight into `MustacheTemplate.Render`, no preprocessing.
- `Canary.Core/Build/SiteBuilder.cs`'s `LoadShellTemplate` — same pattern for `shell.html`, consumed by `PageBuilder.BuildPage`'s `.Replace(...)` chain.

Every one of Canary's own template files — `runtime/dist/widgets/{downloads,slideshow}.html`, `templates/default/shell.html`, and this project's own `docsite/shell.html` — carries exactly this kind of leading doc-comment, specifically *because* their own comments warn "don't write a literal `{{...}}` or placeholder tag in here, it'll get executed/replaced for real." That warning is itself the symptom: comments are treated as live template body, not stripped documentation.

So this isn't a downloads/slideshow-only bug — it's systemic to the whole "just read the file and template-substitute" path, and it will bite every site consuming these templates, not just consoland's.

### Fix

Strip HTML comments (`<!--[\s\S]*?-->`, non-greedy) from a template's raw text once, right after it's read from disk and before any placeholder/Mustache substitution runs:

- In `TemplateWidgetRenderer.Render`, between `File.ReadAllText` and `MustacheTemplate.Render`.
- In `SiteBuilder.LoadShellTemplate` (or wherever the shell template string is produced), before `PageBuilder` does its `.Replace` chain.

Comment-stripping in one place, applied uniformly to both template kinds — same "no special-casing" principle the widget system already holds itself to (see `PLAN.md`'s widget-controversy section).

**Side benefit:** once comments never reach the templater, the "don't write literal `{{tag}}` syntax inside this comment" caveat in `downloads.html`, `slideshow.html`, and `shell.html`'s own doc comments becomes moot — those comments can say whatever they want, including the literal syntax they're currently dancing around not writing. Worth simplifying those doc comments as a follow-up once the strip lands, not blocking the fix itself.

**Open question, not blocking:** should comment-stripping be unconditional, or should a template be able to opt out (e.g. a site author who genuinely wants an HTML comment preserved in their own `shell.html`)? Leaning toward unconditional — these are build-time templates, not authored page content, and nothing about the current design gives a template author a way to say "keep this one." Revisit only if a real case shows up.

### Verification

- Rebuild consolandWebsite (`canary build` from that repo) and confirm `docs/games/Tesselate/index.html` no longer contains the leaked comment text, while the slideshow/downloads widgets still render and function identically.
- Rebuild Canary's own `docsite` (`canary build --config docsite/canary.json`) and confirm its `WIDGETS.md`-derived pages (which demo `downloads`/`slideshow`/`code`) lose the same leaked comments.
- Spot-check a widget that legitimately needs `{{`/`{{#}}`/`{{^}}` syntax outside of a comment still renders correctly — comment-stripping must only remove `<!-- -->` spans, not touch real Mustache tags living in the template body.

## Bug: build never prunes stale output for removed/renamed content

Found while reviewing consolandWebsite's homepage changes: `content/games/draft-idea.md` was deleted from source, but `docs/games/draft-idea/index.html` stayed on disk — no nav entry, no sitemap entry, no source file, yet still a live, publicly reachable page until someone notices and deletes it by hand. Same pattern would hit a renamed slug (old path's output orphaned, new path's output added alongside it).

### Root cause

`SiteBuilder.Build` (`Canary.Core/Build/SiteBuilder.cs`) only ever creates the output directory (`Directory.CreateDirectory(outputRoot)`) and writes/overwrites files for whatever content currently exists — there's no step that enumerates existing output and removes entries with no corresponding source page. The builder is purely additive.

### Fix

Before (or after) writing the current build's pages, diff the output tree against the set of routes just generated and delete any page directory/file under `outputRoot` that no longer corresponds to a route — while leaving alone anything not owned by the builder (e.g. a user's own static assets copied into `docs/` outside the content pipeline, if that's a supported pattern). Needs a clear definition of "builder-owned output" to avoid deleting things it shouldn't.

### Verification

- Delete a content page, rebuild, confirm its old output directory is gone.
- Rename a content page's path, rebuild, confirm the old path's output is gone and the new path's output exists.
- Confirm non-generated files intentionally placed in the output directory survive a rebuild (if that's meant to be supported).
