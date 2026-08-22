# Canary

A hand-rolled, no-framework static site engine: a small TypeScript client runtime (real paths via the History API, nav population, content widgets) paired with a C# CLI that prerenders every route to real static HTML — crawlable by default, sitemap-friendly, GitHub Pages-friendly.

A "Canary site" is a directory holding a `canary.json` config plus markdown content and theme assets. `canary build`/`canary serve` work on that directory the same way for any site that adopts it — including this one: this documentation is itself a Canary site, built by the same `canary build` command a real project uses, from source you can read in `docsite/` in the repo.

## Where to start

- New to Canary? Start with [Getting Started](getting-started) — scaffold a project, serve it locally, make your first edit.
- Already have a project and want a specific answer? Jump to [Reference](reference) for `canary.json` and the CLI.
- Want the concepts, not just the commands? See [Guide](guide) for render modes, widgets, the content toolchain, and publishing.
- Curious why Canary exists at all instead of reaching for an existing static site generator? See [Why Canary](guide/why-canary).

## Status

Pre-alpha. Canary is run from source today — build and run `dotnet run --project src/Canary` from a checkout. There's no published package yet (winget is the intended eventual distribution channel); see [Reference → CLI](reference/cli) for the commands as they exist right now.
