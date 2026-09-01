# Getting Started

Alpha. Windows users can install the [v0.2.0 MSI](https://github.com/haberling/canary/releases/download/v0.2.0/CanaryInstaller.msi) and run `canary` from a new terminal — no .NET SDK. Linux, macOS, and Windows without the MSI build from source (the .NET 10 SDK). Winget is the long-term channel, not what's installable today.

## 1. Scaffold a project

```
canary init my-site
```

With no `--from` flag, `init` prompts you interactively, one field at a time, showing each default in brackets — press Enter to accept it:

```
Site name:
Base URL:
Render mode (hybrid/static) [hybrid]:
Content root [content]:
Output dir [docs]:
Nav depth [1]:
Serve port [6742]:
Copy default widgets into widgets/ on init? [Y/n]:
Prefer Canary's built-in widgets over local copies? [y/N]:
```

Site name and Base URL are the only two required fields — everything else has a sensible default. The serve port default is randomly generated per run (in `[6500, 7000)`) specifically so two projects scaffolded close together don't collide the moment both are served at once.

`--from <path>` pulls core values (site name, base URL, render mode, content root, output dir, nav depth, widgets flags) from an existing `canary.jsonc` instead of prompting. Theme paths, the tools registry, and the serve port are always the fresh scaffold's own.

`init` refuses to touch a directory that already has a `canary.jsonc` in it, unless you pass `--force`. See [Project Layout](getting-started/project-layout) for exactly what gets written.

## 2. Serve it locally

```
canary serve --config my-site/canary.jsonc
```

This builds once, starts a local dev server (`http://localhost:<port>/`, the port from step 1 or `canary.jsonc`'s `serve.port`, overridable with `--port`), and watches for changes. Editing a page's own markdown triggers a fast, targeted rebuild of just that page; anything else (a new/deleted page, a widget, a theme file) falls back to a full rebuild.

## 3. Make an edit

Open `my-site/content/index.md` in an editor and change something — the running `serve` picks it up automatically. Add more `.md` files alongside it for more pages; a new top-level directory under `content/` becomes a nav dropdown automatically.

## 4. Build for real, and publish

```
canary build --config my-site/canary.jsonc
```

writes the finished static site to `output.dir` (`docs/` by default — GitHub Pages' "serve from `/docs` on `main`" convention). For a git-served host, committing and pushing that folder *is* the deploy step. If you'd rather Canary run that step for you, see [Publishing](guide/publishing).

Deleted or renamed pages stay in `output.dir` until you pass `canary build --clean`. See [Incremental Builds](guide/incremental-builds).

## From source (Linux, macOS, or Windows without the MSI)

This needs the .NET 10 SDK and a checkout of the [Canary repo](https://github.com/haberling/canary):

```
git clone https://github.com/haberling/canary
cd canary
dotnet run --project src/Canary -- init my-site
dotnet run --project src/Canary -- serve --config my-site/canary.jsonc
```

Every command on this page is the same with `dotnet run --project src/Canary --` in front of `canary`.

**Next:** [Project Layout](getting-started/project-layout) for what `init` scaffolds, or [Reference → canary.jsonc](reference/config) for every config field.
