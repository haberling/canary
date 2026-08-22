# CLI

Run from a checkout as `dotnet run --project src/Canary -- <command> [args]`. Every command except `init` and `docs` takes `--config <path>` (default `canary.json` in the current directory) to point at a specific site.

## `canary init [path] [--config <path>] [--force]`

Scaffold a new site into `path` (default `.`). With no `--config`, prompts interactively for each field, showing its default. With `--config <path>`, pulls core values (site name, base URL, render mode, content root, output dir, nav depth, widgets flags) from that existing `canary.json` instead — theme paths, the tools registry, and the serve port are always the fresh scaffold's own, never copied from the source. Refuses to touch a directory that already has a `canary.json`, regardless of whether it looks like a Canary project or not, unless `--force` is passed.

## `canary build [--config <path>]`

Full build: manifest generation, chrome re-stamp, content render for every page, sitemap/robots generation, all written to `output.dir`. See [Incremental Builds](../guide/incremental-builds) for what "every page, every time" actually costs (not much — the expensive part, the disk write, is conditional).

## `canary serve [--config <path>] [--port <n>]`

Local dev server. Builds once, then watches for changes and rebuilds (a targeted single-page rebuild for a plain markdown edit, a full rebuild otherwise). Binds to `--port` if given, otherwise `canary.json`'s `serve.port`. Does not re-read `canary.json` if you edit it mid-session — restart the server to pick up config changes.

## `canary publish [--config <path>]`

Builds, then runs `canary.json`'s `publish` command, streaming its output live. Fails with a clear message if `publish` isn't configured. See [Publishing](../guide/publishing).

## `canary docs [--force]`

Opens this documentation in your default browser, served locally on a random unused port in `[9000, 9999]`. No `--config` — it's Canary's own bundled docs, not a site you own. Resolved the same way `templates/default`/`runtime/dist` are (a walk up from the running `canary` binary's own location), so it works from any full checkout without a separate build step, as long as `docs/` has already been built at least once (`canary build --config docsite/canary.json`).

Only one instance runs at a time: starting it writes a small lock file (process id + port) to your `ApplicationData` folder; running `canary docs` again while that process is still alive just prints `docs already open at http://localhost:<port>/` and exits, rather than opening a second one on a second port. Pass `--force` to close the existing one first (a plain process kill, best-effort — "try to close," not guaranteed) and start fresh; if the previous process is already gone (a crash, not a clean exit), the next `canary docs` cleans up the stale lock on its own and starts normally, no `--force` needed for that case.

## `canary widgets [--config <path>]`

Lists every widget name Canary can currently find for the site at `--config` (built-in plus site-authored under `widgets/`).

## `canary widget <name> [--config <path>]`

Prints that widget's ready-to-paste usage example. See [Widgets](../guide/widgets).
