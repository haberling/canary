# Getting Started

Canary is pre-alpha and run from source — there's no published package yet (see [Why Canary](../guide/why-canary) and the repo's `PLAN.md` for the eventual winget-based plan). You'll need a checkout of the [Canary repo](https://github.com/haberling/canary) and the .NET 10 SDK.

## 1. Scaffold a project

From a checkout, run `canary init` against an empty (or new) directory:

```
dotnet run --project src/Canary -- init my-site
```

With no `--config` flag, `init` prompts you interactively, one field at a time, showing each default in brackets — press Enter to accept it:

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

`init` refuses to touch a directory that already has a `canary.jsonc` in it, unless you pass `--force`. See [Project Layout](project-layout) for exactly what gets written.

## 2. Serve it locally

```
dotnet run --project src/Canary -- serve --config my-site/canary.jsonc
```

This builds once, starts a local dev server (`http://localhost:<port>/`, the port from step 1 or `canary.jsonc`'s `serve.port`, overridable with `--port`), and watches for changes. Editing a page's own markdown triggers a fast, targeted rebuild of just that page; anything else (a new/deleted page, a widget, a theme file) falls back to a full rebuild.

## 3. Make an edit

Open `my-site/content/index.md` in an editor and change something — the running `serve` picks it up automatically. Add more `.md` files alongside it for more pages; a new top-level directory under `content/` becomes a nav dropdown automatically.

## 4. Build for real, and publish

```
dotnet run --project src/Canary -- build --config my-site/canary.jsonc
```

writes the finished static site to `output.dir` (`docs/` by default — GitHub Pages' "serve from `/docs` on `main`" convention). For a git-served host, committing and pushing that folder *is* the deploy step. If you'd rather Canary run that step for you, see [Publishing](../guide/publishing).

**Next:** [Project Layout](project-layout) for what `init` scaffolds, or [Reference → canary.jsonc](../reference/config) for every config field.
