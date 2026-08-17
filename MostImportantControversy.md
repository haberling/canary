# The Widget Controversy

Captured 2026-08-16, mid-conversation, before any decision was made. Not a plan, not resolved — a record of the disagreement so it doesn't get lost.

**Resolved 2026-08-17, in two rounds — see PLAN.md's "Widget system — resolved controversy" section for the full writeup.**

Round 1: neither of the two options below. Widgets became plain `.js` files (not TypeScript) executed via an embedded JS interpreter (Jint), one implementation instead of two, discovery by scanning a folder — sidestepping the Roslyn-vs-DLL question entirely.

Round 1 turned out to be incomplete: it fixed "two implementations" but not the actual complaint, since `render(title, body)` was still real procedural code building HTML via string concatenation. The user caught this directly ("it looks like how to build knowledge still sits inside the renderer"). Round 2 made widgets fully declarative on both sides: an `.html` file (real Mustache-syntax template, hand-rolled parser/templater but real standard syntax) plus an optional `.js` file for shared, event-delegated client behavior (no more per-instance generated code) — with the fence body as real YAML instead of ad-hoc shorthand. Jint is gone again, this time because there's no widget code left to execute at all. `SlideshowWidgetRenderer.cs`/`DownloadsWidgetRenderer.cs` (round 1's Jint-based versions too) are deleted. Question 2 below is now answered by round 2's design; question 3 (config-file `widgets` field) still wasn't needed.

## How this started

Asked to `cat` out `SlideshowWidgetRenderer.cs` (the C# build-time port of `slideshow.ts`) for review. Rereading it turned up a real bug: `RenderSlide` builds the `<img src="...">` attribute via `EscapeAttr(slide.Src)` directly — it never calls `MarkdownRenderer.ResolveUrl`, the root-relative-path fix added earlier in the session. A slideshow slide written as `content/games/images/x.png` renders with no leading slash, which breaks exactly like the original page-relative-path bug once the page lives at a nested prerendered path (`games/tesselate/`). `DownloadsWidgetRenderer`'s `href` has the identical gap.

It slipped through unnoticed because:
- the Tesselate smoke-test content was only ever inspected as raw HTML text, never actually loaded in a browser
- the dogfood site's own slideshow used absolute `https://` URLs, which don't need resolving at all, so the browser-testing pass never exercised this code path either

## The actual objection

Not really about that specific bug. The user's words: **"I meant the whole method behind widgets, I dont like it, a widget needs to be something a lot more readable than this in its own right."**

The vision: a clear, defined **interface** for widgets, such that a site author can write their own and just **drop it into the `widgets/` folder** alongside `slideshow`/`downloads` — no editing Canary's own source — and if a piece of content actually references it, it's automatically included in the build. Confirmed understanding was requested and given.

## What's already true, and what regressed

The **TypeScript side already works exactly like this.** `markdown.ts`'s widget dispatch does `import(`./widgets/${name}.js`)` by convention — no registry, no hardcoded list. Its own existing comment says so directly: *"Adding a new widget is just adding a new file there with a `render(title, body)` export — this file never needs to change."*

The **C# port lost that property.** `SlideshowWidgetRenderer` and `DownloadsWidgetRenderer` are hardcoded into a dictionary literal in `SiteBuilder.DefaultWidgets()`. A site author can't add a third widget without editing Canary's own source and recompiling it. This was an unintentional regression introduced during the TS→C# port, not a deliberate design choice made along the way.

## Why this isn't a simple fix — the real tension

Getting the C# (build-time) side to genuinely match "drop a `.cs` file in, it just works" requires **compiling/running arbitrary C# at build time**. That is, architecturally, the same mechanism as the build-time plugin system that was explicitly **cut from scope** earlier in this project (Roslyn scripting, in-process execution of a site-supplied `.cs` file — see `PLAN.md`'s "Plugin system — cut from scope" section). The difference now: back then it was speculative, no concrete use case. Now there's a real, motivated one (custom widgets) — but reviving the mechanism should be a deliberate decision, not something slipped in sideways under a different name.

Two real options on the table, not yet chosen between:

1. **Revive Roslyn scripting, scoped specifically to widgets.** True parity with how the TS side already works — drop a raw `.cs` file, no separate build step, no project file. Same tradeoffs as before: recompiles every build, no sandboxing (acceptable when the widget author is the site owner, per the original plugin-system reasoning).
2. **Precompiled DLL + reflection scan.** Site author builds a small class library implementing `IWidgetRenderer`, drops the compiled DLL into a `widgets/` folder; Canary reflection-scans that folder at build time and registers whatever it finds. Avoids reopening "run arbitrary source at build time" at all, but requires the widget author to have a .NET SDK and know how to build a class library — a real step, not just authoring a file.

## A second, separable problem: readability of a widget itself

Independent of how discovery works: the current implementation pattern — hand-building raw JS as strings via helper functions like `ApplyIndex`/`GotoScript`/`StartTimerScript` that return snippets of code-as-text — is genuinely hard to read and easy to get subtly wrong (see: the bug that started this whole conversation). That's a property of the *shape* a widget's implementation takes, not of how it gets discovered. Fixing discovery alone doesn't fix this.

Both TS and C# versions use inline `onclick`/`onload`/`onkeydown` attributes rather than real `<script>` + `addEventListener`, deliberately: content injected via `innerHTML` (spa mode's markdown-rendered swaps, hybrid mode's fragment-fetch swaps) never executes `<script>` tags, so inline attributes were chosen to behave identically regardless of how a page's content got onto the screen (cold server-rendered load vs. warm client-side swap). Whether that constraint is worth keeping, or whether it's worth solving "re-execute/hydrate scripts after an innerHTML swap" properly instead, is itself an open question.

## Open questions, unresolved as of this save

1. Roslyn-scripting revival (scoped to widgets) vs. DLL+reflection — which discovery mechanism?
2. Redesign how a widget *authors* its interactivity (something better than inline-JS string concatenation) now, alongside the discovery fix — or leave that alone and fix discovery only?
3. Not yet asked, but adjacent: does this same "drop a file in, it's auto-included" convention belong on the config-file side too (a `widgets` field currently doesn't exist in the schema at all — right now inclusion is 100% implicit-by-reference-in-content, mirroring the TS side's existing convention)?
