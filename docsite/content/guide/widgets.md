# Widgets

A widget is a fenced code block in markdown whose body is YAML, rendered through a template at build time — not a shortcode with ad-hoc syntax, real YAML filling a real Mustache template. Three ship built in: `downloads` (a list of download links and/or copy-paste install commands), `slideshow` (an image slideshow with captions), and `code` (an escaped, verbatim code block — the one used throughout this page to show the other two).

Run `canary widgets <name>` against a project to print any widget's ready-to-paste usage example straight from its own source — the canonical copy, not a doc snippet that can drift out of sync. `canary widgets` with no name lists every widget Canary can currently find for your project (built-in plus your own).

## `downloads`

```code
lang: yaml
lines:
  - text: "```downloads"
  - text: "title: Optional Title"
  - text: "items:"
  - text: '  - label: "Windows Installer"'
  - text: '    url: "https://example.com/file.msi"'
  - text: '  - label: "User Manual"'
  - text: '    url: !url "content/games/manual.pdf"'
  - text: "  - copy: true"
  - text: '    label: "Install via PowerShell"'
  - text: '    command: "msiexec /i ... /quiet /norestart"'
  - text: "```"
```

- `title` — optional heading; defaults to "Downloads" if omitted.
- `items` — a list of entries, each either a plain link (`label`, `url`) or a copy-paste command row (`copy: true`, `label`, `command`).

## `slideshow`

```code
lang: yaml
lines:
  - text: "```slideshow"
  - text: "title: Optional Title"
  - text: "slides:"
  - text: '  - src: "https://example.com/shot1.png"'
  - text: '    caption: "Optional caption"'
  - text: '  - src: !url "content/games/images/shot2.png"'
  - text: "```"
```

- `title` — optional heading.
- `slides` — a list of entries, each with `src` and an optional `caption`.

## `code`

What renders the two boxes above. `lang` is an optional cosmetic label (no syntax highlighting is implemented — it just shows the name and sets a `language-<lang>` class as a hook for later); `lines` is a list of entries, each a single `text` field holding one line of the example, in order.

It exists specifically because a widget is invoked by its own name as a fence's info string, with no escape sequence — a literal `` ```downloads `` fence always runs the downloads widget, it can never just *display* as text. Wrapping the example one line at a time inside `code` sidesteps that: the outer fence's info string is `code`, so it renders instead of running.

**A real gotcha found writing this page, worth repeating:** `YamlParser` strips a scalar's matching outer quote pair but never unescapes a backslash inside one — writing a line's `text` as `"...\"...\""` leaves literal backslashes in the output instead of the quote you meant. Use single quotes as the line's own delimiter instead whenever it contains a literal double quote, as several lines above do (`'  - label: "Windows Installer"'`).

## The `!url` tag

A site-root-relative path (e.g. `content/games/manual.pdf`, matching how markdown links/images are already written) needs the explicit `!url` YAML tag on that value to be rewritten into a root-relative URL that works regardless of how deep the current page is nested. This is deliberate, not automatic: nothing gets resolved just because a field happens to be named `url` or `src` — you opt in per value, on `downloads`' `url` field and `slideshow`'s `src` field. A plain value with no tag is left exactly as written, which is correct for an already-absolute `https://` URL and broken for a relative one on any page that isn't the site root.

## Writing your own

Drop a `name.html` file into your project's `widgets/` folder — a plain Mustache template (`{{var}}` sections, inverted sections) filled from the fence block's YAML body. No registration needed: reference that name as a fence's info string in content, and if a matching template exists, it's used. Add a matching `.js` file alongside it for shared interactive behavior (referenced once per page, not regenerated per widget instance — use event delegation so it keeps working after a `hybrid`-mode fragment swap), and/or a matching `.css` file for its styling. A site-authored widget always wins over a built-in one of the same name.

## `copyDefaultsOnInit` / `preferBuiltIn`

`canary init` copies the built-in widgets into your project's own `widgets/` folder by default (`copyDefaultsOnInit: true`) — an "eject"-style starting point you can edit freely, rather than leaving them invisible inside Canary's own install. Since local copies win on a name collision, editing them takes effect immediately. If you'd rather always track Canary's current built-in versions instead of your local edits, set `preferBuiltIn: true` — a single site-wide switch, not per-widget.
