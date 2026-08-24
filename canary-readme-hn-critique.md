# HN-style teardown: Canary README

> Critique of the [haberling/canary](https://github.com/haberling/canary) README  
> (as of ~2026-08-23), written in the voice of a cold Hacker News reader.  
> Use as a revision checklist before Show HN or a broader public push.

**Related surfaces reviewed:**

- GitHub README (`master`)
- https://canary.consoland.net (docs, Getting Started, Why Canary, CLI)
- https://consoland.net (dogfood site)

---

## Summary

The README is too thin to stand alone. It defers almost everything to https://canary.consoland.net, understates the .NET requirement, oversells “no-framework” / “alpha live,” and never shows a quick start, config sample, or concrete “why not Hugo.” The engine may be fine; the front door is not ready for a cold HN audience.

A Show HN with the current README mostly tests whether people will be kind about an unfinished front door—not whether Canary is interesting.

---

## Full critique

### README is doing almost nothing

Six sentences, a status line with a grammar error, and a license. Everything that would let a stranger decide whether to care is off-site at `canary.consoland.net`. If that docs domain is down, misconfigured, or the reader is on a train with bad signal, the GitHub page is a brochure with no product.

READMEs that say “see the docs” without even a minimal install path in the repo are a recurring failure mode. Put the 60-second path *in the repo*.

**Current README (paraphrased structure):**

1. Logo  
2. One-paragraph description  
3. “Split out of a private site… see the docs site”  
4. Status: Alpha is Live + vague production claim  
5. MIT license  

That is not enough for a cold audience.

---

### “No-framework static site engine” is a contradiction used as branding

Canary *is* a framework: client router, widget runtime, CLI, config schema, render modes, toolchain hooks. Calling it “no-framework” because it isn’t React is the same move every other SSG made in 2016. HN has seen that sentence a hundred times.

Say what it *is*:

- Markdown in  
- Build-time static HTML out  
- Small History API router for in-site navigation  
- Optional widgets via explicit fenced blocks  

That is fine. The slogan is not doing useful work.

---

### Status is inconsistent and soft

| Surface   | Claim |
|-----------|--------|
| README    | “Alpha is Live, and more importantly have websites in prod running it. More changes and improvements to come.” |
| Docs site | “Pre-alpha. Canary is run from source today — build and run `dotnet run --project src/Canary` from a checkout. There's no published package yet (winget is the intended eventual distribution channel).” |

Pick one.

- “Alpha” + run-from-source + “winget eventually” is not “live” in any sense a stranger cares about.  
- “Have websites in prod” without names or URLs reads as padding. If the answer is consoland.net and the docs site, **say that**.

Grammar nit on the same line: “more importantly have websites” → “has.”

---

### No install, no hello-world, no screenshot

A stranger should not need a second site to learn that the entry point looks like:

```text
dotnet run --project src/Canary -- init
dotnet run --project src/Canary -- serve --config my-site/canary.json
```

That belongs in the README under `## Quick start`, assuming a machine with the .NET SDK.

Right now the README does not even admit you need .NET. That is the single most important filter for a large part of the audience, and it is buried in the docs.

---

### Windows / .NET is a feature or a limitation — state it above the fold

C# CLI + MSI + winget ambition is a *positioning* choice. The README pretends to be a generic static engine.

- People scanning for “another Node SSG” bounce for the wrong reason.  
- People who would *like* a sane .NET-native toolchain never get the signal.

Own the niche in the first screen of the README: Windows-first / .NET SDK required / works on … (list other OS if true).

---

### Why not Hugo / Eleventy / Astro / Jekyll?

There is a real answer (hash routing vs crawlability on GitHub Pages, explicit-over-implicit widgets, dogfooding). It lives on the Why Canary page, not in the README.

Without a short “when to use this instead,” the project looks like a personal CMS extracted for public consumption—which is exactly what the first paragraph admits. Extraction is fine. **Unpositioned** extraction gets closed.

---

### “Hand-rolled” is not a selling point by itself

On HN it means either:

- “I understand every line” (good), or  
- “I reimplemented `mkdir` and a markdown parser” (neutral-to-bad).

Couple it with one concrete technical claim the README currently lacks, for example:

- Hybrid prerender + client nav without a Node toolchain  
- Widgets as explicit fenced blocks instead of magic shortcodes  
- Designed for hosts with no rewrite rules (GitHub Pages)

Right now “hand-rolled” is atmosphere.

---

### Nits that still cost trust

- **Grammar:** “more importantly have websites” → “has.”  
- **Logo path:** `src="docsite/img/logo.svg"` is fragile depending on how GitHub resolves relative paths; prefer a stable absolute/raw URL or hosted asset.  
- **No code sample** in the whole README—not even a `canary.json` stub or a markdown fence widget example. The interesting part of the system (widgets, `!url`, hybrid mode) is invisible.  
- **No badges / requirements line:** e.g. “Requires .NET 8” (or whatever you target).  
- **No release summary** on the README.  
- **Claude as repo contributor** (visible in GitHub UI): fine if intentional; some HN threads will fixate on that harder than the architecture. Be ready, or make human ownership obvious in the README.

---

## What would survive the first 20 HN comments

A README that, **without leaving GitHub**, includes:

1. One tight paragraph of what it does  
2. **Requires:** .NET SDK x.y; Windows-first / also works on …  
3. **Quick start:** three commands + expected URL  
4. Minimal `canary.json` + one page of markdown (and optionally one widget fence)  
5. Link to consoland.net / docs as *further* reading, not the only reading  
6. **Honest status:** pre-alpha, dogfooded on N public sites (**named**), no package yet  
7. One sentence (or three bullets) on why not Hugo for *your* constraints  

---

## Suggested README skeleton

Copy/adapt as needed. Replace SDK version, OS notes, and examples with whatever is accurate.

```markdown
# Canary

Markdown sites with real static HTML at build time and optional SPA-style
navigation via the History API. C# CLI + small TypeScript client runtime.
Built for GitHub Pages–style hosts (no server rewrites).

**Status:** Pre-alpha. Run from source. Dogfooded on:

- https://consoland.net
- https://canary.consoland.net

**Requires:** .NET SDK x.y (Windows-first; [note other OS support if true])

## Quick start

```bash
git clone https://github.com/haberling/canary
cd canary
dotnet run --project src/Canary -- init my-site
dotnet run --project src/Canary -- serve --config my-site/canary.json
```

Open the printed localhost URL. Edit `my-site/content/index.md` and save;
the dev server rebuilds.

## What it is (and isn’t)

- Prerenders every route to static HTML (crawlers and link previews work with JS off)
- Optional hybrid mode: full HTML on cold load, client router on in-site navigation
- Widgets via explicit Markdown fences (not magic shortcodes)
- Not a React/Vue app framework; not a hosted CMS

## Why not Hugo / Eleventy / Astro?

[One short paragraph or three bullets on your actual constraints—e.g. GitHub Pages
without rewrites, .NET-native toolchain, explicit widget contracts, dogfooding
a hand-built site that still needs crawlability.]

## Minimal example

**canary.json** (illustrative—match real schema):

```json
{
  "siteName": "Example",
  "baseUrl": "https://example.com",
  "renderMode": "hybrid",
  "contentRoot": "content",
  "outputDir": "docs"
}
```

**content/index.md** (illustrative):

```markdown
# Hello

This page is markdown. The build emits real HTML for this route.
```

## Docs

Guides and reference: https://canary.consoland.net

## License

MIT
```

---

## Priority order for fixes

1. **Quick start + .NET requirement** in the README (highest leverage)  
2. **Align status language** between README and docs (pre-alpha vs alpha live)  
3. **Name dogfood sites** instead of “websites in prod”  
4. **Tiny config + markdown example** in-repo  
5. **Positioning** vs Hugo / Eleventy / Astro (three bullets is enough)  
6. **Polish:** grammar, logo URL, optional widget sample, requirements line  

---

## Optional: one-line pitch variants

Use on X, Show HN title, or GitHub description—not all at once.

- “Markdown → static HTML + optional History API nav; C# CLI; built for GitHub Pages.”  
- “A small .NET-native static site engine that prerenders every route and still feels like a SPA in-site.”  
- “Hand-rolled SSG extracted from a real site: crawlable HTML, hybrid router, explicit widgets.”

---

## Note on Show HN timing

You do not need to be an HN regular first. You do need a README a hostile-but-fair stranger can follow in five minutes, honest alpha framing, and to be online for a few hours after posting to answer real questions without defensiveness.

If the install path is still “clone and read PLAN.md,” skip Show HN until the front door matches the product.
```
