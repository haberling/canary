# Content Toolchain

The problem this solves: functionality that should apply to *many* pages — a breadcrumb at the top, a "reading time" estimate, a "return to blog" link — without copy-pasting a widget block into every markdown file, and without mixing site behavior into content data. A **tool** is an external command that receives a page's raw markdown on stdin and returns transformed markdown on stdout, run once per page at build time, before that markdown reaches the renderer.

Tools only ever run at build time, never in the browser — a tool is an arbitrary external command, and there's no browser equivalent for that. Every page is always fully re-rendered on every build anyway (see [Incremental Builds](incremental-builds)), so there's no separate "did the tool's output change" cache to keep in sync.

## Two separate pieces

**Definition** — a `tools` map in `canary.jsonc` (name to shell command), one central registry:

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

A tool can be any executable command — `canary init` scaffolds two working examples to make that concrete, not just claim it. `tools/curtain.cs` (C#, run via `dotnet run tools/curtain.cs`) is a do-nothing passthrough: it reads stdin, writes it back to stdout unchanged, and writes the two environment variables above to stderr just to demonstrate they're there. `tools/reading-time.ps1` (PowerShell, run via `powershell -NoProfile -ExecutionPolicy Bypass -File tools/reading-time.ps1`) is a real, non-trivial one -- it counts words and inserts a reading-time badge after the page's first heading. Unlike C#, PowerShell (specifically `powershell.exe`, not `pwsh`) ships with every supported Windows install, so it needs nothing installed beyond Windows itself -- worth knowing if you want a tool that doesn't depend on whoever clones the repo having a .NET SDK. Copy or rename either as a real starting point.

**Always give a local script command an explicit path** (`tools/foo.cmd`, not a bare `foo.cmd`) — a bare filename with no path separator can silently fail to resolve on Windows machines with the `NoDefaultCurrentDirectoryInExePath` security setting enabled. Every tool command Canary itself ships already follows this rule.

## Precompiling a C# tool

A C# tool registered the normal way (`"curtain": "dotnet run tools/curtain.cs"`) pays the full cost of starting the CLR and JIT-compiling the script on *every single page* it runs on, every build. For a tool used across many pages, that adds up. Opt a tool into precompilation by switching its registry entry from a string to an object:

```
"tools": {
  "reading-time": "tools/reading-time.ps1",
  "curtain": { "command": "tools/bin/curtain.exe", "source": "tools/curtain.cs" }
}
```

`command` means exactly what it always meant — the thing that actually runs, unchanged. `source` is new: the `.cs` file `command` gets built from. Nothing else changes about how you write the tool itself — it's still a plain file-based C# app (top-level statements, no `.csproj`) reading stdin and writing stdout, the same contract every tool already follows. `command`'s filename doesn't need to match `source`'s — name it whatever you want.

Run `canary tools build` to compile every tool with a `source` field (or `canary tools build <name>` for just one). This runs `dotnet publish` targeting Native AOT — not a framework-dependent build — so the result is a single standalone native executable with no .NET runtime dependency at the machine running `canary build` later, and none of the CLR startup/JIT cost on every invocation. Needs a working Native AOT toolchain (the same one publishing Canary itself needs) on whatever machine runs `canary tools build`.

If a tool needs a NuGet package, use .NET's file-based-app `#:package Name@Version` directive at the top of the `.cs` file — `dotnet publish` already understands it, nothing Canary-specific required.

**AOT trims aggressively and has no runtime reflection.** A tool using reflection-based `System.Text.Json` (not source-generated) or similar could behave differently precompiled than it did under `dotnet run`. Not a concern for the kind of small, straightforward text transform a toolchain tool is meant to be, but worth knowing if something works unbuilt and breaks once precompiled.

`canary build` checks every precompiled tool once per run: if `command`'s binary is missing entirely, the build fails with a message telling you to run `canary tools build <name>`. If `source` has been edited more recently than `command` was last built, the build prints a warning and proceeds anyway using the existing binary — never a hard stop, just a heads-up that you probably meant to rebuild.
