# Canary — Development Journal

Raw material for a blog post, not the blog post itself. Chronological, quote-heavy, and honest about the parts that went sideways before they went right. Assembled 2026-08-18 from git history (both `canary` and its parent project `consolandWebsite`), `PLAN.md`, `MostImportantControversy.md`, and the actual development conversation.

---

## Act 0: Consoland, before any of this was a problem (Aug 14–16)

Canary didn't start as a framework. It started as a personal site.

`consolandWebsite`'s original plan (`consolandWebsite/PLAN.md`, written 2026-08-14) is refreshingly small in scope:

> The user wants a personal site to publish CLI utilities/games (mostly served as downloadable MSI installers) as a lightweight, hand-rolled wiki: drop a markdown file in a folder, it becomes a page.

Three decisions, locked in from day one and never really revisited until Canary existed: hand-written TypeScript for the browser (no framework, no markdown library), a C# console app for everything else (scan content, compile TS, assemble output, `git add/commit/push`), and GitHub Pages serving a `docs/` folder. "No framework" wasn't a slogan — it was load-bearing. The whole point was a hand-built wiki, not an app.

Over the next two days, consoland actually shipped, one commit at a time:

```
fbbd2e2  Got logo and site skeleton done
d71ba18  added domain registration instructions
d76d954  great progress
8582640  Create CNAME
9e7fec0  Test site is live
13c233c  Merge branch 'main' of github.com:haberling/consolandWebsite
f308bcd  The old blog posts are here now.
28243f4  Imaged added in to blog post
66d964f  Updated the build-manifest tool to be universally aplicable
a13d598  tessalate page added to content / not site
565c20a  Time to launch tesselate
cad7a40  Added windows 10 disclaimer to Tesselate. Removed game landing page as unnecessary
5db2ff4  Slideshow added to tesselate
13d524e  removed last example page, added google analytics tag
```

By the last commit (Aug 16, 18:56), consoland was a real, live, small site: old blog posts migrated over, a Tesselate game page with a slideshow, Google Analytics wired up. The entire hand-rolled stack — router, markdown renderer, the two content widgets, the C# manifest/deploy tooling — was 866 lines across five files. Genuinely tiny. Genuinely "no framework."

It used **hash routing**: `consoland.net/#/games/tesselate`. Client-side JS fetches the right markdown file and renders it into `#app`. Zero server config needed — GitHub Pages just serves `index.html` for every request, and the fragment after `#` never even reaches the server. That property is *why* hash routing got chosen in the first place: no rewrite rules, no server-side anything, works identically whether GitHub Pages or literally any static host serves the files.

It also meant the site was **functionally invisible to search engines**. A crawler doesn't execute the hash router; it never sees anything past `index.html`. A sitemap of hash URLs would list N entries that all resolve to the exact same document — worse than useless, actively misleading to a crawler.

That's the wall the project hit on the evening of Aug 16, less than three hours after the last consoland commit.

## Act 1: The pivot (Aug 16, evening)

The fix everyone reaches for here is prerendering — generate a real static HTML file per route at build time, so a sitemap's `<loc>` points at something a crawler can actually fetch and read. That much wasn't controversial. What *was* a real decision: **do this as a patch to consoland, or pull the reusable parts out into their own thing first?**

The call was to extract early. Canary's very first commit message says it plainly:

> Bootstraps the Canary repo with the design plan carried over from the consolandWebsite split-out discussion, plus baseline open-source scaffolding.

And the opening line of that first `PLAN.md`, written before a single line of prerendering code existed:

> Canary is the site-engine being split out of `consolandWebsite`: the hand-rolled TypeScript runtime (hash router + markdown renderer + nav-from-manifest) and the C# build/deploy tooling, minus that site's actual content and branding. It was born out of a design discussion about making the markdown-wiki approach crawlable by search engines without losing the "no framework" hand-built character of the original site.

That last clause matters as much as the SEO problem itself: the fix had to preserve the *character* of consoland, not just its function. Bolting a heavyweight SSG onto a deliberately tiny hand-rolled site would have solved the crawler problem while killing the actual point of the project. Canary's whole design constraint from minute one was "generalize this, don't replace it."

The original scope was narrow and specific — not "build a framework," but five concrete mechanisms:

1. Don't point a sitemap at raw `.md` files or hash-fragment URLs — a sitemap `loc` has to be a real, directly-browsable page.
2. Prerender to real static paths, but *keep* the hash-router SPA for in-app navigation once JS loads — additive, not a rewrite.
3. Header/nav/footer chrome is an identically-referenced skeleton, not duplicated content.
4. Chrome is unconditionally re-stamped every build; only content-rendering is checksum-gated (cheap to redo the shell every time, expensive and diff-noisy to redo the render).
5. Incremental build via a per-page source checksum embedded directly in the output HTML.

Everything else — the render-mode system, the widget architecture, hooks, the whole idea of "a framework" rather than "a build script" — grew out from that narrow seed over the following two days.

## Act 2: Bootstrap (Aug 16, 19:54 → 21:29)

Ninety minutes after the initial commit, "Skeleton added" landed: the actual solution structure (`Canary.slnx`, `src/Canary`, `src/Canary.Core`, `tests/Canary.Core.Tests`), the config loader and its schema, and — tellingly — the *entire consoland TS runtime ported over verbatim first* (`main.ts`, `markdown.ts`, `router.ts`, both widgets, `framework.css` split off from consoland's `style.css`). 1,660 lines added in one commit.

This is the "extraction" phase doing exactly what extraction means: get the working thing copied over and compiling clean *before* changing anything about how it works. Nothing architecturally new yet — just consoland's guts, minus consoland's branding, sitting in a new home.

## Act 3: The marathon (Aug 16, 21:29 → Aug 17, 02:15)

The single biggest commit in the whole project's history — "all but canary init" — landed at 2:15 AM. Nearly five hours, one sitting, and by the diff stat it's not close: `SiteBuilder`, `PageBuilder`, `ContentScanner`, the whole `Canary.Core.Manifest` namespace, the markdown renderer, the YAML parser, the Mustache templater, the static file server and dev-mode watcher, sitemap/robots generation, and — the part that took the longest and detoured the hardest — the entire widget system, twice.

### The bug that wasn't really about the bug

It started as a routine code review. Asked to look at `SlideshowWidgetRenderer.cs` (the just-ported C# build-time widget renderer), a real bug turned up fast: `RenderSlide` built its `<img src="...">` attribute directly from the slide data, never routing it through `ResolveUrl` — the root-relative-path fix that had *already* been added for ordinary markdown links earlier that same session. A slideshow image written as `content/games/images/x.png` would render exactly as typed, no leading slash, and break the instant the page lived anywhere but the site root. `DownloadsWidgetRenderer` had the identical gap.

It had slipped through for a boring, honest reason: the smoke-test content was only ever inspected as raw HTML text, never actually loaded in a browser, and the dogfood site's own slideshow happened to use absolute `https://` URLs that never needed resolving in the first place. Nothing exercised the code path.

That would have been a two-line fix. It wasn't the point. `MostImportantControversy.md` — written live, mid-argument, specifically so the disagreement wouldn't get lost — records the actual objection in the user's own words:

> "I meant the whole method behind widgets, I dont like it, a widget needs to be something a lot more readable than this in its own right."

The vision underneath that: a site author should be able to write their own widget, drop it in a `widgets/` folder next to their content, reference it from a markdown fence block, and have it just work — no editing Canary's own source, no recompiling Canary itself. And the TypeScript side already *had* this property. `markdown.ts`'s widget dispatch was already `import(\`./widgets/${name}.js\`)` by pure filename convention, no registry — its own comment said so directly: *"Adding a new widget is just adding a new file there... this file never needs to change."*

The C# port had quietly regressed that. `SlideshowWidgetRenderer`/`DownloadsWidgetRenderer` were hardcoded into a dictionary literal — a site author couldn't add a third widget without editing Canary's own source and recompiling it. Not a deliberate choice. Just a thing that fell out of porting TS to C# without carrying the *property*, only the *behavior*, across the language boundary.

### Why the obvious fix wasn't available

Getting true "drop a `.cs` file in, it just works" parity on the C# side means compiling and running arbitrary C# at build time. That's architecturally identical to a build-time plugin system — which had *already* been explicitly cut from scope earlier in the same session, on the grounds that it was speculative, no concrete use case yet. Reviving that exact mechanism to solve widgets, quietly, under a different name, would have been exactly the kind of sideways scope-creep the project was trying to avoid on purpose.

Two real options went on the table instead: revive Roslyn scripting scoped specifically to widgets (true TS-side parity, no sandboxing, same tradeoffs as the original cut plugin idea), or a precompiled-DLL-plus-reflection-scan approach (avoids re-opening "run arbitrary source at build time" at all, but now a widget author needs the .NET SDK and knows how to build a class library — a real barrier, not just authoring a file).

### Round 1 — fixed the wrong half

The first resolution: widgets as plain `.js` files (not TypeScript), executed at build time via **Jint**, an embedded JS interpreter. Discovery by scanning a folder, no registry. This sidestepped the Roslyn-vs-DLL question entirely by not running C# at all — and it genuinely fixed "one implementation instead of two," since now both the browser and the build-time renderer executed the exact same widget code.

It didn't fix the actual complaint. `render(title, body)` still *was* real procedural code — string concatenation building up HTML, conditional branches for the link-vs-copy-command split. Moving that code from C# to JS didn't make it stop being code. The user caught it directly: *"it looks like how to build knowledge still sits inside the renderer."*

### Round 2 — declarative on both sides

The real fix, and the one that stuck: a widget is an `.html` file — a genuine Mustache-syntax template, not a Canary-specific format — plus an optional sibling `.js` file for shared client-side behavior. The fence block's body became real YAML instead of an ad-hoc `label | url` shorthand. Both parsers are hand-rolled (no external dependency, matching the "no framework" ethos everywhere else in this project), but they implement *real, standard, portable syntax* — anyone who's touched Mustache or YAML before can read or write a Canary widget with zero Canary-specific knowledge.

`Canary.Core.Widgets.TemplateWidgetRenderer` became the entire "renderer," full stop — parse the YAML, fill the template, done. Jint got ripped back out, this time for good, because there was no widget-authored *code* left anywhere to execute.

Interactivity moved with it: instead of regenerating an entire click/keydown/autoplay script as an inline string on every single widget occurrence (visible in the raw HTML as sprawling `onclick=` attributes), a widget's `.js` file — when it has one — gets copied once and referenced once per page via `<script defer>`, using real `document.addEventListener` event delegation instead of string-templated code.

Two more real bugs turned up building this, both found by actually running things:

- **The doc-comment trap.** `downloads.html`'s own explanatory comment used the literal syntax `{{#items}}` to describe what the section did — and the templater has no concept of "this text is just documentation." It executed the comment as a real tag, corrupting the output. (This had already bitten the shell chrome template once before this; it bit a widget's own doc comment the exact same way.) Fixed by describing the syntax in prose from then on, never writing the literal characters in a comment.
- **The `!url` question, and a real, sharp design line drawn in response.** With no widget-specific code left anywhere, nothing applied the root-relative-path fix to a YAML `url:`/`src:` field anymore — the exact bug this whole detour started from was technically still unfixed. The instinct was to auto-resolve any field literally named `url` or `src`. The user's reaction, direct and immediate:

  > "im just not sure thats a problem" ... "im not that interested in non url fields being resolved by an automated system... a syntactical rule must be made in how arguments are passed to widgets."

  The objection wasn't the bug fix — it was *guessing* based on a field's name. What if a field happens to be called `url` but genuinely shouldn't be resolved? What if a field means a URL but isn't named that? The resolution: a real YAML tag, `!url`, that an author writes explicitly on the one value that needs it (`url: !url "content/games/x.png"`). Standard YAML syntax (`!name` tags are real — `!Ref` in CloudFormation, `!include` elsewhere), opt-in rather than inferred. This exact incident became a standing principle for the rest of the project: when a design has to choose between guessing from context and requiring an explicit marker, default to the explicit marker, every time, even when guessing would be less typing.

By the end of that five-hour commit: 103 tests passing (up from 8 at the very start of the day), full render pipeline across three modes, `canary serve`, sitemap/robots generation, and a widget system that had been fought over, gotten wrong once, and rebuilt properly.

## Act 4: Dogfooding for real (Aug 17)

Up to this point almost everything had been verified as generated files on disk, never actually *looked at*. That changed. A real dogfood site got built — an informational site about Gilbert & Sullivan's *The Pirates of Penzance*, home page with a slideshow of real Wikimedia Commons production posters, a synopsis page, a `characters/` section with dropdown nav — and driven through an actual Chrome browser via the claude-in-chrome extension for the first time.

It found a real bug immediately: landing cold on a real prerendered URL (typed directly, or reached via a full-page in-content link) has no hash fragment at all. `hybrid-router.ts` only ever read `location.hash` to know the current route, so it silently misread every such cold load as the site root — highlighting "Home" in the nav instead of the actual page. Content was never wrong (the initial-load branch never touched `#app`), only the nav highlight — but it was a real, user-visible bug that file-level inspection had completely missed. Fixed by seeding the router's notion of "current path" from `window.location.pathname` before the hash-derived value (meaningless on a cold load) could stomp it.

`canary serve` found its own bug the same way, and it was a nastier one: the file watcher initially watched everything except `output.dir` — but `ManifestBuilder` unconditionally rewrites `content/manifest.json` back into the *source* tree on every build. Every rebuild retriggered the watcher, which triggered another rebuild, forever. This wasn't a hypothetical — it became an actual runaway process that had to be `taskkill`ed. The fix generalized past the specific case: the watcher now pauses itself while its own callback runs (plus a short grace period after, for filesystem notifications that arrive slightly late), so it can never react to its own writes regardless of where in the tree they land.

The `canary widgets`/`canary widget <name>` CLI commands landed the same day — each built-in widget got a `<!--clipboard ... -->` comment block holding a ready-to-paste usage example, extracted at request from the widget's own `.html` file, so looking up correct syntax never requires reading the template source. Print-only, deliberately not real OS clipboard access — no cross-platform clipboard API exists in .NET without a Windows-only dependency or per-OS shelling out, and piping the output yourself gets nearly all the value for none of the cost.

A user review of the live dogfood site (not automated testing) caught one more real bug: the downloads widget's copy-command row had lost its wrapper `<div>` during the Round 2 rewrite, so the code block and Copy button stacked vertically instead of sitting side by side. Small fix — but re-verifying it surfaced something much bigger: after the fix, `canary build` reported "reused unchanged = 6" — the incremental-build checksum only ever hashed the *markdown source*, never the widget files a page's content actually depended on. Editing a widget silently did nothing until a full clean rebuild. Logged as a known bug, deliberately not fixed on the spot — it would matter more once there was a second thing (beyond widgets) that could invalidate a page's cache, and fixing both gaps in one pass beat touching that code twice.

## Act 5: spa/static dogfooding, and the bug that foreshadowed the ending (Aug 17)

The remaining two render modes got the same real-browser treatment. `static` came back clean. `spa` didn't: clicking "Characters" in nav showed a 404-style "Not found" page instead of the real content. Root cause: `spa-router.ts`'s route-to-file mapping only ever special-cased the site's root route; every other route just joined its path segments, which works for a leaf page but not for a *directory's own landing page* (`characters/index.md`, which the build-time convention already knew to treat specially, but the client-side router never had that memo). Fixed with a leaf-then-directory-index fallback fetch, verified live in the browser.

It's worth noting in hindsight: this was the second time in two days that *the exact same convention* had to be independently reimplemented on the client side and had drifted out of sync with the server side. That pattern was about to matter a lot more than one bug fix.

## Act 6: "lets have a conversation so you know what I mean" — designing hooks (Aug 17–18)

The next feature request came in deliberately unhurried: *"lets have a conversation so you know what I mean"* about "toolchaining." What followed was a real back-and-forth, not a spec dump — narrowing from "toolchaining" down to something concrete: a way to apply functionality (a breadcrumb, a "Return to Blog" link) across *many* pages, without copy-pasting a widget fence block into every markdown file by hand. That would defeat the entire point of separating site behavior from content data.

The design that emerged, refined turn by turn:

- A **hook** is an arbitrary external command. A page's raw markdown goes in on stdin, transformed markdown comes back out on stdout. Run once per page, at build time, before the markdown reaches the renderer.
- **Definition and application are deliberately separate.** Hook *commands* live once, centrally, in a `hooks` dictionary in `config.json`. Hook *application* — which directories actually run which hooks — lives in a new, separate `.hooks.json` per content directory.
- Considered and explicitly rejected: folding hook application into the existing `.nav.json`. The reasoning traced straight back to a decision already made in Phase 3 (keeping `SitemapBuilder` independent of the nav tree, since nav visibility and crawler discoverability are orthogonal concerns) — hooks were a third orthogonal axis, and merging them in would mean `.nav.json`'s own name would quietly stop meaning what it said.
- Considered and explicitly rejected: letting a directory's hooks cascade down to its subdirectories. This is the same principle the `!url` tag decision established a day earlier, applied again: a page's behavior shouldn't depend on something declared three directories up that isn't visible from where the page actually lives. The tradeoff — real repetition if a hook needs to apply broadly — was accepted on purpose.

And then, mid-design, the conversation took the turn that mattered most:

> "so, ok, in a sense, as you im sure imagine, this sorta breaks the whole idea of SPA mode, because its now still pre-rendering, but I dont want to limit how hooks can be written to something that can somehow be run in the javascript, so maybe we need to rethink spa mode in general..."

Hooks are an arbitrary external command — there's no browser equivalent, full stop, for any language a hook author might reach for. That meant `spa` mode would either need its own special-cased client-side hook mechanism that fundamentally couldn't exist for an arbitrary command, or it would need to start prerendering too — at which point it stopped being meaningfully different from `hybrid` in how content actually got built, only in navigation policy.

Asked for a straight pro/con on dropping `spa` entirely, the honest tally leaned hard toward "drop it": its whole reason to exist (client-side rendering without a build step) had just evaporated, and keeping it around would mean re-solving "does this also need a separate JS implementation" for every single future feature, forever — as `spa`'s own directory-index bug from a day earlier had already previewed. The user's counter-offer was sharp: recover most of `spa`'s old feel later, if it's ever actually needed, as a lightweight redirect-to-hash flag on `hybrid`, rather than as a whole separate rendering pipeline. Logged as a genuine future consideration, not built.

Then, once that was settled:

> "Ok now that we have Spa recovery in future considerations, Its time for SPA to die."

## Act 7: The purge (Aug 17, 21:40 → Aug 18, 00:10)

`spa-router.ts` had gotten one more update at 21:40 (folding in the day's directory-structure fixes) before, less than three hours later, getting deleted along with everything else `spa`-specific: the `RenderMode.Spa` enum value, `SiteBuilder.BuildSpa`, `RuntimeAssetManifest`'s spa bundle entry, `runtime/ts/markdown.ts` (the client-side markdown renderer — the *only* thing that had ever run it in a browser was the mode that had just been deleted), and the spa-only test cases. The commit message doesn't pretend otherwise:

> `SPA mode removed, because render toolchain renders it, lol, pointless`

The deletion turned out to retire two other standing problems for free: the long-open "spa mode needs its own client-side widget renderer" gap (moot — every widget renders at build time now, in every remaining mode) and the "does `!url` need to work client-side too" question (same reason). 101 tests passing afterward, down from 103 — the two removed tests were deleted along with the feature, not broken.

## Act 8: Hooks, for real (Aug 18, 00:42)

With `spa` gone, hooks only ever needed the one prerendering path that already existed. Implementation landed less than an hour after the removal commit: a new `Canary.Core.Hooks` namespace (`HooksOverride`/`HooksOverrideFile` for the `.hooks.json` side, `HookRunner` for actually chaining and executing hooks via real subprocesses), wired into `SiteBuilder` — and, as planned a day earlier, the widget-and-hook checksum-gating fix landed in the exact same pass rather than as a second, separate change.

Two more real bugs, found by actually running the thing rather than by reading the code:

- **`cmd.exe`'s quoting is two bugs deep.** A hook command shaped exactly like the design's own example — `"tools/breadcrumb.sh"` — failed the very first time it actually ran. `cmd /c tools/breadcrumb.sh` gets misparsed, because cmd's switch scanner keeps hunting for more `/x`-shaped switches anywhere after `/c`, and a bare forward slash in an ordinary relative path reads as a second switch. The obvious fix — wrap the whole thing in quotes — *also* failed, for an even less obvious reason: cmd only preserves quotes under specific conditions, one of which requires whitespace *inside* the quoted string, and a bare path has none, so cmd silently stripped the quotes right back off. The actual fix — prefixing with `call ` — was verified empirically against real `cmd.exe` invocations, including confirming exit codes still propagated correctly, before it got trusted.
- **A stdin/stdout race.** A hook command that exits immediately without ever reading its input (an intentionally-failing test case, but a legitimate real-world shape too) could throw a broken-pipe exception that masked the actual, intended error message. Caught by an intermittently-failing test, fixed by treating a broken pipe on the write side as expected and letting the real exit-code check downstream produce the actual error — then verified by running the previously-flaky test five times in a row, since a single green run doesn't prove a timing bug is actually gone.

A real hook went straight into the dogfood site as proof, not just left as a unit test: a `curtain` script appending "— Curtain. —" to the bottom of the home and synopsis pages. The first version was almost too subtle to notice at a glance — a fair complaint — so it got a second hook alongside it: a PowerShell script computing a real per-page word count and reading-time estimate, inserted right under the page title where it can't be missed, chained together with `curtain` to prove multiple hooks compose in declared order. Both are still live in `workspace/` today, not reverted after verification.

## Act 9: "i gazed upon thine CSS" (Aug 18)

The last fix in this chronicle didn't come from a bug report or a design conversation — it came from a straight code review, delivered with its own sense of humor:

> "always reviewing your work, i gazed upon thine CSS. I like alot of it... but I noticed a negative, the base CSS seems to include some specific styling for widgets! but this seems to break are widget pattern, perhaps thine should have a seperate css file that can be imported in alongside the widgets for thine styling, what say you speak plainly and defend how thine mind reacts"

The critique held up completely under its own weight. `WidgetDiscovery` had always promised built-in and site-authored widgets were discovered *identically* — same folder scan, same filename lookup, no special-casing either direction. That symmetry was the entire point of the Round 2 widget redesign. But `downloads`/`slideshow`'s actual CSS had been hardcoded directly into the site's own `framework.css` since before that redesign even existed — a leftover from the very first "Skeleton added" commit, back when `framework.css` was split off from consoland's `style.css`, long before there was a real discoverable widget system to keep it symmetric with. Nobody had gone back and asked whether it still belonged there once that system existed. It didn't: a site author's own custom widget had nowhere to put CSS at all, while the two built-ins got free styling nobody else could get.

The fix reused machinery that already existed rather than inventing anything new: `WidgetDiscovery` was already generic over file pattern, so a third call with `"*.css"` cost nothing architecturally. `downloads.css`/`slideshow.css` got extracted out of `framework.css` verbatim (no rule changes — the extracted CSS references the same `--accent`/`--bg`/etc. tokens `framework.css` defines, which cascade globally regardless of which linked file references them), discovered and copied and linked exactly the way `.js` already was, via a new `{{widgetStyles}}` shell placeholder. Verified pixel-identical in a real browser afterward — the fix was entirely about *where* the CSS lived, never about what it said.

## Act 10: NavDepth, and a consoland assumption nobody had questioned in two days (Aug 18)

Asked what was left after the widget-CSS fix, the honest answer was "just Phase 4, `canary init`." That's not what came next. The next request landed with a sigh:

> "sigh there is something else that must change, and its pretty basic. we need to have a configurable NavDepth for going deeper, one level in when building a manifest was fine originally but that is a design choice for consoland, and is not a universal for canary, 'and wont work for the amazing interactive documentation canary will have where it serves up a doc site using the test server built in canary!'"

It held up immediately on inspection. `ManifestBuilder`'s nav-tree builder had been hardcoded to recurse exactly one level deep since the very first "all but canary init" commit — a direct, unexamined port of consoland's own `build-manifest.cs`, fine for a site that really was flat, silently wrong the moment a site wasn't. `ContentScanner` already found and built pages at *any* depth for real routing; the nav *menu* had just never been asked to look that deep. A page at `docs/guides/widgets.md` would build, get a real URL, and be completely invisible in nav — reachable only by a direct link, never surfaced.

The fix had two real halves, and only fixing one of them would have been a trap. The C# side was the straightforward part: a new `nav.depth` config field (default `1`, so every existing site's behavior stayed byte-for-byte identical; `<= 0` for unlimited), and `ManifestBuilder.BuildDirectoryNavItem` made properly recursive — a nested directory becomes a nav item the same way a top-level one always did, sorted alongside its sibling files in one dropdown. `.nav.json` auto-creation, which had the exact same "only ever looked one level deep" assumption baked in for the same unexamined reason, now reaches exactly as deep as the nav tree itself does.

The client side was the part that would have quietly broken everything if skipped: `nav.ts`'s renderer had never had a reason to be recursive, so it flattened every dropdown child straight into a plain `<a>` tag. A perfectly correct, deeply-nested `NavItem` tree coming out of the new C# code would have been silently truncated the moment it hit the browser — the exact same class of "the convention only got implemented on one side" bug that had already bitten this project twice (the widget-CSS split, the `spa` directory-index 404). Rewritten so a child with its own children renders as a nested flyout submenu instead, and `updateActiveNav` rewritten to walk up the DOM from wherever it actually matched, marking every ancestor level active — not just checking one level up, the way it always had.

Dogfooded immediately and for real, not as a synthetic test fixture: a new "Background" section went straight into `workspace/`, two genuine levels deep — `background/history/copyright-dispute.md`, `background/production/original-cast.md` — kept in permanently, same as every other feature this project has shipped. Driven in an actual browser: the nested flyout opened correctly on hover, and on the deepest page, all three ancestor levels — the top-level nav item, the nested subitem, the leaf link — lit up active together, in both `hybrid` and `static` modes. A stale copy of `workspace/css/framework.css` (never re-synced since the widget-CSS fix earlier that same day) got caught and fixed along the way, almost as an afterthought.

## Act 11: The chevron that took three tries (Aug 18)

The new flyout submenus needed some visual signal that a row expands further, so a small "❯" chevron went in next to any dropdown label with children. It shipped, got dogfooded, looked fine in a screenshot. Then:

> "well so long as you are updating the default template, I might say that the carrot you added being appended directly onto the text looks pretty bad"

What followed is worth recording in full, because the first two fixes both looked correct on inspection and neither one actually worked — a real, concrete lesson about verifying a claim against the DOM instead of a screenshot and a confident guess.

**Attempt 1:** the chevron was pinned to the far edge of its row via `justify-content: space-between`. Theory: a tightly auto-sized dropdown has no slack width for `space-between` to distribute, so the chevron sits wherever the content ends — right against the text. Fix: swap to a fixed `gap`. Declared fixed. It wasn't — "no not fixed, not verified," the very next message, after a hard refresh specifically to rule out caching. (One real, useful side effect: the dev server had been sending zero cache-control headers at all, a genuine gap for local dev regardless of whether it was the actual culprit here — fixed by sending `Cache-Control: no-store` on every response, so this entire class of confusion can't happen again going forward.)

**Attempt 2:** new theory — a CSS `::after` pseudo-element merges into the *same* anonymous flex item as the text it's attached to, since nothing separates them at the element level, so `gap` (which only inserts space *between* distinct flex items) had nothing to act on. Swapped the chevron to a real, separate `<span>`. Checked in a brand-new browser tab specifically to rule out any possible cache explanation. Still touching, confirmed by a tight zoomed screenshot.

**What actually fixed it:** giving up on reasoning about it and asking the page directly, via `getComputedStyle`. The real answer had nothing to do with either theory: `.nav-dropdown-label`'s own `display: flex` was never winning at all. A pre-existing, unrelated rule — `.nav-dropdown a { display: block; ... }` — has higher CSS specificity than a bare `.nav-dropdown-label` class selector, and had been silently overriding it the entire time. The element was never a flex container in the first place, in either attempt, which is exactly why neither fix could possibly have worked regardless of which one was "more correct" in isolation. Fixed by qualifying the selector to `.nav-dropdown .nav-dropdown-label`, specificity high enough to actually win. Verified this time by measuring the real rendered gap between the two elements' `getBoundingClientRect()`s — 8px, exactly the `0.5rem` the CSS asked for — not by looking at another screenshot and deciding it looked right.

## Where things stand

Two render modes, not three. A widget system that took two real rounds to get right, and is now symmetric across all three of its file types. A hooks system designed carefully enough in conversation that its implementation surfaced only real, fixable bugs — never a design flaw. A nav tree that finally recurses as deep as a site actually needs it to. 137 tests, up from 8 on day one. A dogfood site that's accumulated real, working, live examples of every feature as it landed, rather than a pile of synthetic test fixtures nobody ever actually looks at.

A few things kept showing up, project-wide, often enough to call them the actual throughline:

- **Real bugs came from actually running things, almost never from reading the code.** The slideshow URL bug, the doc-comment self-collision, the runaway file watcher, the cold-load nav-highlight bug, the spa directory-index 404, the `cmd.exe` quoting fiasco, the stdin race, the nav-depth chevron — every one of these was invisible on paper and obvious the moment something actually executed, in a browser or a real shell, against real input.
- **Explicit beat implicit, every time it came up.** The `!url` tag over guessing by field name. Non-cascading hooks over inheritance. A separate `.hooks.json` over folding into `.nav.json`. Each of these cost something (more typing, more repetition, more files) in exchange for never having to trace up a tree or guess at what the system inferred.
- **Fix it once, not twice.** The widget-checksum bug sat logged and unfixed for a full day specifically so it could be fixed together with the hooks-checksum gap that hadn't been built yet, rather than touching the same fragile code twice.
- **The dogfood site is not a demo.** It's a real, accumulating, live proof — every render mode, every widget, both hooks, deep nav, all still there, all still working, because leaving them in and rebuilding them is cheaper than pretending they were never real in the first place.
- **"It looks right" is not verification.** The chevron got "fixed" twice, confidently, before it actually was — both times because a plausible theory plus a glance at the result stood in for actually checking. The pattern that finally worked wasn't a better guess, it was asking the DOM directly (`getComputedStyle`, `getBoundingClientRect`) instead of trusting a screenshot and a story about why it should be right.
