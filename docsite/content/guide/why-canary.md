# Why Canary

## The problem it started from

Canary began as a plan to split a hand-rolled TypeScript site runtime — a router, a markdown renderer, nav generation from a manifest — out of one specific site's own content and branding. That site wanted to stay a genuinely hand-built, no-framework markdown wiki, but also wanted to be crawlable by search engines and shareable with real link previews, neither of which a pure client-side-rendered SPA gives you for free. GitHub Pages, the intended host, only serves static files — no server-side rendering, no rewrites. Hash routing was the original workaround (the fragment after `#` never even reaches the server, so client JS alone decides what renders), but hash routing and real crawlability are in tension with each other.

Canary's actual answer: prerender every route to real static HTML at build time, so a cold load — crawler, direct link, social-preview scraper — gets full content immediately with zero JavaScript required, while a warm in-app navigation still gets a router smart enough to feel instant (see [Render Modes](render-modes)). Markdown never reaches the browser in either case.

## Hand-rolled, on purpose

Canary doesn't wrap Jekyll, Hugo, Eleventy, or a JS meta-framework — the markdown renderer, the templating (a real Mustache implementation), the YAML parsing widgets use, the client router, are all written from scratch, in this repo, readable start to finish. That's a real cost (more surface area to build and maintain) traded for a real benefit: nothing here is a black box you have to go read someone else's docs to understand, and nothing is pulled in as a dependency just because reimplementing it seemed like more work up front. When a genuinely standard, language-agnostic format already exists for something — YAML, Mustache — Canary uses the real syntax rather than inventing its own shorthand, so prior experience with either transfers directly, even though the parser/templater implementing them is a one-off.

## Explicit over implicit

A recurring decision throughout Canary's design: when something *could* be inferred automatically, prefer requiring the author to say so explicitly instead. A widget's `url`/`src` field only gets rewritten to a site-relative path if you tag it `!url` — nothing resolves just because a field happens to be named that. A content directory's toolchain tools are exactly what its own `.toolchain.json` declares, never inherited from a parent directory. `canary init` refuses to touch a directory that already has a `canary.jsonc`, rather than guessing at intent from what's already there. The pattern shows up again and again because it was chosen deliberately, not stumbled into: a little more typing, in exchange for a page's actual behavior always being visible from the file that defines it.

## Dogfooding, all the way down

This documentation site is itself a Canary site — its own `canary.jsonc`, its own markdown content, built with the exact same `canary build` command a real project would run, source readable in [docsite/](https://github.com/haberling/canary/tree/master/docsite) in the repo. Not a metaphor: if a real bug exists in nav generation, page rendering, or the CSS framework layer, it will show up here first, the same way it showed up first in the [Pirates of Penzance dogfood site](https://github.com/haberling/canary/tree/master/workspace) used to exercise Canary end-to-end during development. A framework whose own documentation can't be built by the framework isn't trustworthy yet.
