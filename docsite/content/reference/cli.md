# CLI

Run as `canary <command> [args]` (Windows MSI) or `dotnet run --project src/Canary -- <command> [args]` from a checkout. Every command except `init` and `docs` takes `--config <path>` (default `canary.jsonc` in the current directory) to point at a specific site.

## `canary init [path] [--from <path>] [--force]`

Scaffold a new site into `path` (default `.`). With no `--from`, prompts interactively for each field, showing its default. With `--from <path>`, pulls core values (site name, base URL, render mode, content root, output dir, nav depth, widgets flags) from that existing `canary.jsonc` instead — theme paths, the tools registry, and the serve port are always the fresh scaffold's own, never copied from the source. Refuses to touch a directory that already has a `canary.jsonc`, regardless of whether it looks like a Canary project or not, unless `--force` is passed.

## `canary build [--config <path>] [--clean]`

Full build: manifest generation, chrome re-stamp, content render for every page, sitemap/robots generation, all written to `output.dir`. See [Incremental Builds](guide/incremental-builds) for what "every page, every time" actually costs (not much — the expensive part, the disk write, is conditional).

`--clean` deletes the entire `output.dir` after a yes/no prompt (default No) and rebuilds from scratch. Answering No cancels the build entirely. Without a TTY (piped or CI), `--clean` refuses: it does not delete and does not build. Build-only — not on `serve` or `publish`. The usual reason to pass it: a deleted or renamed source page still has output on disk, because a plain `canary build` is purely additive.

## `canary serve [--config <path>] [--port <n>]`

Local dev server. Builds once, then watches for changes and rebuilds (a targeted single-page rebuild for a plain markdown edit, a full rebuild otherwise). Binds to `--port` if given, otherwise `canary.jsonc`'s `serve.port`. Does not re-read `canary.jsonc` if you edit it mid-session — restart the server to pick up config changes.

## `canary publish [--config <path>]`

Builds, then runs `canary.jsonc`'s `publish` command, streaming its output live. Fails with a clear message if `publish` isn't configured. See [Publishing](guide/publishing).

## `canary docs [--force]`

Opens this documentation in your default browser, served locally on a random unused port in `[9000, 9999]`. No `--config` — it's Canary's own bundled docs, not a site you own.

The MSI install bundles `docs/` next to the exe, so this works with no checkout. From a source checkout, `docs/` has to have been built at least once (`canary build --config docsite/canary.jsonc`).

Only one instance runs at a time: starting it writes a small lock file (process id + port) to your `ApplicationData` folder; running `canary docs` again while that process is still alive just prints `docs already open at http://localhost:<port>/` and exits, rather than opening a second one on a second port. Pass `--force` to close the existing one first (a plain process kill, best-effort — "try to close," not guaranteed) and start fresh; if the previous process is already gone (a crash, not a clean exit), the next `canary docs` cleans up the stale lock on its own and starts normally, no `--force` needed for that case.

## `canary widgets [<name>] [--config <path>]`

With no `<name>`, lists every widget Canary can currently find for the site at `--config` (built-in plus site-authored under `widgets/`). With a `<name>`, prints that widget's ready-to-paste usage example instead. See [Widgets](guide/widgets).

## `canary tools build [<name>] [--config <path>]`

Precompiles every `tools` registry entry that has a `source` field (or just `<name>`'s, if given) via `dotnet publish`, targeting Native AOT. A registry entry with no `source` — the plain bare-string form — isn't touched; there's nothing to build for it. See [Content Toolchain → Precompiling a C# tool](guide/toolchain).

## `canary tools validate [<name>] [--config <path>]`

Round-trips a probe of non-ASCII text (math symbols, smart quotes, accented letters, CJK, emoji) through every `tools` registry entry (or just `<name>`'s, if given), via the same stdio pipe a real build uses, and fails (exit code 1) if any tool doesn't hand the probe back unchanged. Catches a tool whose own `Console.In`/`Console.Out` isn't pinned to UTF-8, which otherwise mangles non-ASCII markdown silently — often unnoticed until some page's actual content happens to hit it. See [Content Toolchain → "A tool's stdin/stdout is UTF-8"](guide/toolchain).

## `canary explore nav|navigation [--config <path>]`

Interactive tree of the curated nav menu — the same tree `nav.depth` and `.nav.json` produce. Arrow keys or `j`/`k` to move, Right or Enter to expand, Left to collapse, `q` or Esc to quit. Redirected stdout prints a flat tree and exits. See [Nav and `.nav.json`](guide/nav).

## `canary explore toolchain [--config <path>]`

Interactive tree of every content directory's toolchain assignment, unbounded by nav depth (folders below `nav.depth` or flagged `nonav` still appear). Same keys as `explore nav`. Redirected stdout prints a flat tree and exits. See [Content Toolchain](guide/toolchain).

Bare `canary explore` prints that command's own help.
