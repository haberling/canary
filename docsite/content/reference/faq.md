# FAQ

## A publish or toolchain command that should work just silently fails on Windows

Check whether the command is a bare filename with no path separator (e.g. `publish.cmd` instead of `tools/publish.cmd` or `./publish.cmd`). On a machine with the `NoDefaultCurrentDirectoryInExePath` Windows security setting enabled, a bare filename silently fails to resolve. A path with a separator bypasses that lookup entirely and always works. Always write local script commands with an explicit path.

## I edited canary.jsonc while `canary serve` was running and nothing changed

Expected — `canary serve` treats the config as fixed for the lifetime of the session and doesn't re-read it on every rebuild. Restart the server after editing `canary.jsonc`.

## My widget's `url`/`src` value is broken on any page except the home page

You need the explicit `!url` YAML tag on that value (`url: !url "content/games/manual.pdf"`) — see [Guide → Widgets](guide/widgets). A plain value with no tag is used exactly as written, which only works if it's already an absolute `https://` URL.

## Can I use markdown tables?

Not yet — Canary's markdown renderer is a small hand-rolled implementation (see [Why Canary](guide/why-canary)) covering headings, paragraphs, bold/italic, inline code, links/images, fenced code blocks, single-level unordered/ordered lists, blockquotes, and horizontal rules. No tables, and list items are single-line only (no nested lists, no multi-paragraph list items). Work around a table with a bulleted list instead.

## How do I show a `downloads`/`slideshow` fence block as a literal example in my own content?

A widget is invoked by its name as the fence's info string, and there's no escape syntax for "show this fence as text instead of running it" — writing a literal `` ```downloads `` fence always runs the downloads widget. Use the built-in `code` widget instead: it renders its `lines` back out as an escaped, verbatim block, so wrapping the example one line at a time inside it displays as text rather than executing. See [Guide → Widgets](guide/widgets) for the exact shape — this documentation's own Widgets page uses it for exactly this.

## I deleted a page and the old URL still works

Expected — `canary build` is purely additive. Pass `canary build --clean` to wipe `output.dir` after confirmation and rebuild from scratch. See [Incremental Builds](guide/incremental-builds).

## I set `preferBuiltIn: true` and nothing changed

Not wired up yet. The field is written by `canary init` and stored in config, but local `widgets/` copies always win on a name collision. See [Widgets](guide/widgets).

## Do I need the .NET SDK?

Windows MSI: no. `canary` from the installer is self-contained. From source (`dotnet run --project src/Canary -- …`): yes, the .NET 10 SDK. See [Getting Started](getting-started).

## `canary widget slideshow` says the command doesn't exist

The singular `widget` command is gone. It's `canary widgets slideshow` (list with no name, print one widget's usage example with a name).
