# Project Layout

What `canary init` writes into a fresh project, and why each piece exists.

- **`canary.json`** — the project's config. See [Reference → canary.json](../reference/config) for every field. Gets an `"initialized": true` marker so a later `init` run against the same directory knows to refuse (unless you pass `--force`).
- **`shell.html`** — the page chrome (`<head>`, header/nav, `<main id="app">`, footer) every generated page is stamped into. Copied from Canary's own generic starter — edit it to change the site's structure, not just its colors.
- **`css/framework.css`** — generic layout only (header, nav dropdown, footer, `#app`). Not meant to need per-site edits; brand decisions live in `theme.css` instead, as CSS custom properties (`--bg`, `--accent`, etc.) framework.css already reads.
- **`css/theme.css`** — your actual branding: colors, and (if you want a different logo/title treatment) the `.site-logo`/`.site-title` rules. Starts as a copy of framework.css's own default values, so it's immediately usable, not an empty file.
- **`tools/example.cs`** — a working, do-nothing content-toolchain tool (echoes stdin to stdout unchanged) registered in `canary.json`'s `tools` map but not applied anywhere yet. Copy or rename it as a starting point once you want a real one — see [Guide → Content Toolchain](../guide/toolchain).
- **`content/index.md`** — your home page. This is the one file `init` never overwrites, even with `--force` — it's real content, not framework scaffold.
- **`widgets/`** — if `copyDefaultsOnInit` was left on (the default), editable local copies of the built-in `downloads` and `slideshow` widgets. Site-authored widgets win over built-in ones on a name collision, so these become the active versions immediately. See [Guide → Widgets](../guide/widgets).

Everything except `content/index.md` is overwritten on every `init` run against the same directory (with `--force`), by design — these files are meant to track Canary's current defaults, not freeze at whatever version you first scaffolded against.
