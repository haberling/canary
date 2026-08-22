# Content Toolchain

The problem this solves: functionality that should apply to *many* pages — a breadcrumb at the top, a "reading time" estimate, a "return to blog" link — without copy-pasting a widget block into every markdown file, and without mixing site behavior into content data. A **tool** is an external command that receives a page's raw markdown on stdin and returns transformed markdown on stdout, run once per page at build time, before that markdown reaches the renderer.

Tools only ever run at build time, never in the browser — a tool is an arbitrary external command, and there's no browser equivalent for that. Every page is always fully re-rendered on every build anyway (see [Incremental Builds](incremental-builds)), so there's no separate "did the tool's output change" cache to keep in sync.

## Two separate pieces

**Definition** — a `tools` map in `canary.json` (name to shell command), one central registry:

```
"tools": {
  "breadcrumb": "tools/breadcrumb.sh",
  "reading-time": "tools/reading-time.ps1"
}
```

**Application** — a `.toolchain.json` file in each content directory that wants tools applied, listing which registered tool names run there, in order:

```
{ "tools": ["breadcrumb", "reading-time"] }
```

Canary auto-creates an empty, self-documenting `.toolchain.json` in every content directory that has at least one `.md` file directly inside it (at any depth), if one doesn't already exist — so the file to edit is always there, you just have to add names to it.

**There is no cascading.** A directory's tool list is exactly what its own `.toolchain.json` says — nothing is inherited from a parent directory. A page's behavior should be visible from where the page actually lives, not depend on something declared three directories up. The tradeoff is real repetition if you want a tool applied broadly (re-declaring it in every directory that wants it), accepted deliberately.

## Execution

A page's applicable tools run in their declared array order, chained — one tool's stdout becomes the next tool's stdin, and the final result is what the markdown renderer sees. A tool that exits non-zero fails the whole build; there's no silent partial-output fallback.

## Environment variables

Every tool's process gets two:

- `CANARY_ROUTE_PATH` — the current page's own nav-tree path (`""` for the site root, `"games/tesselate"` for a nested page), matching `manifest.json`'s own path strings exactly.
- `CANARY_MANIFEST_PATH` — the absolute path to the site's already-generated `manifest.json`, so a tool can consult the full nav tree (for something like "add a note to every page under `games/`") rather than only ever seeing its own page's markdown in isolation.

## Writing a script

A tool can be any executable command — the example scaffolded by `canary init` (`tools/example.cs`, run via `dotnet run tools/example.cs`) is a working, do-nothing passthrough: it reads stdin, writes it back to stdout unchanged, and writes the two environment variables above to stderr just to demonstrate they're there. Copy or rename it as a real starting point.

**Always give a local script command an explicit path** (`tools/foo.cmd`, not a bare `foo.cmd`) — a bare filename with no path separator can silently fail to resolve on Windows machines with the `NoDefaultCurrentDirectoryInExePath` security setting enabled. Every tool command Canary itself ships already follows this rule.
