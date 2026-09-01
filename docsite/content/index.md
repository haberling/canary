# Canary

Markdown in, website out. Pre-rendered HTML on cold load, with a small TypeScript runtime for in-site navigation. The C# CLI generates static HTML that's crawlable and social-link preview-friendly. Widgets add site features with fenced YAML. A toolchain can modify markdown before rendering, keeping source files clean.

A "Canary site" is a directory holding a `canary.jsonc` config plus markdown content and theme assets. `canary build`/`canary serve` work on that directory the same way for any site that adopts it — including this one: this documentation is itself a Canary site, built by the same `canary build` command a real project uses, from source you can read in `docsite/` in the repo.

## Where to start

- New to Canary? Start with [Getting Started](getting-started) — scaffold a project, serve it locally, make your first edit.
- Already have a project and want a specific answer? Jump to [Reference](reference) for `canary.jsonc` and the CLI.
- Want the concepts, not just the commands? See [Guide](guide) for render modes, widgets, the content toolchain, and publishing.
- Curious why Canary exists at all instead of reaching for an existing static site generator? See [Why Canary](guide/why-canary).

## Status

Alpha. A Windows MSI for v0.2.0 is in [Releases](https://github.com/haberling/canary/releases) — no .NET SDK. The two named dogfood sites are [consoland.net](https://consoland.net) and [canary.consoland.net](https://canary.consoland.net) (this documentation). See [Getting Started](getting-started) to install and scaffold, or [CLI](reference/cli) for every command.
