# canary.jsonc

Lives at a site's root. JSONC — `//` comments and trailing commas are allowed; unquoted keys and single-quoted strings are not. One config per environment — no dev/prod split, nav and routes are always derived from `content.root` plus the generated manifest, never redeclared here. `canary init` scaffolds a commented `canary tools build` example into the file.

```
{
  "site": { "name": "...", "baseUrl": "https://..." },
  "content": { "root": "content" },
  "output": { "dir": "docs" },
  "renderMode": "hybrid",
  "nav": { "depth": 1 },
  "serve": { "port": 6913 },
  "theme": {
    "shell": "shell.html",
    "base": "css/framework.css",
    "theme": "css/theme.css",
    "logo": "img/logo.svg",
    "favicon": "img/logo.svg"
  },
  "widgets": { "copyDefaultsOnInit": true, "preferBuiltIn": false },
  "tools": { "breadcrumb": "tools/breadcrumb.sh" },
  "publish": "git add docs && git commit -m Publish && git push"
}
```

## `site`

- `name` — required. Your site's display name, used in the page chrome and `<title>`.
- `baseUrl` — required. Your site's real public URL, used for sitemap and robots generation. Local `canary serve` / `canary docs` never read it for page, nav, CSS, or JS URLs.

## `content`

- `root` — required. The directory (relative to `canary.jsonc`) holding your markdown content.

## `output`

- `dir` — defaults to `docs`, matching GitHub Pages' "serve from `/docs` on `main`" convention. Everything `canary build` produces goes here.

Files that must land at the output root under a fixed name (GitHub Pages' `CNAME`, `.nojekyll`, a `robots.txt`/`favicon.ico` override) go in `root-copy/` next to `canary.jsonc`, not here. See [Project Layout](getting-started/project-layout).

## `renderMode`

`hybrid` or `static`. See [Guide → Render Modes](guide/render-modes). Defaults to `hybrid`.

## `nav`

- `depth` — how many directory levels deep the generated nav menu recurses. Defaults to `1`. A page deeper than this limit still exists and gets a real URL either way — this only controls how deep the nav *menu* itself goes, not what's reachable. `0` or negative means unlimited depth.

Per-directory hide/allow/deny, sort order, and `nonav` live in that directory's `.nav.json`, not here. Depth and `.nav.json` together are the nav story. See [Nav and `.nav.json`](guide/nav).

## `serve`

- `port` — the port `canary serve` binds to when `--port` isn't passed on the command line. Defaults to `6913`. `canary init` generates a fresh random port per project instead of always writing this fixed default, so two sibling projects don't collide when both are served at once.

## `theme`

- `shell` — path to the shell HTML template (page chrome).
- `base` — path to the base framework stylesheet.
- `theme` — path to your own theme stylesheet, loaded after `base`.
- `logo` — optional path, relative to the site root, to an image used in the header. Unset (including a fresh `canary init`) means no logo, not Canary's branding.
- `favicon` — optional path, relative to the site root, to the favicon. Unset means no favicon.

This documentation site sets both to `img/logo.svg`.

## `widgets`

- `copyDefaultsOnInit` — defaults to `true`. Whether `canary init` copies the built-in widgets into your project's own `widgets/` folder as an editable starting point.
- `preferBuiltIn` — written by `canary init` and stored in config, but not applied yet. Local `widgets/` copies always win on a name collision regardless of this value.

See [Guide → Widgets](guide/widgets).

## `tools`

A name-to-command map, the central registry a `.toolchain.json` in any content directory can reference by name. A value is either a bare shell command string (`"breadcrumb": "tools/breadcrumb.sh"`), or an object opting a C# tool into precompilation: `"curtain": { "command": "tools/bin/curtain.exe", "source": "tools/curtain.cs" }`. See [Guide → Content Toolchain](guide/toolchain) for the full picture, including `canary tools build`.

## `publish`

Optional. A single arbitrary shell command, run by `canary publish` after a fresh build. Not set by default. See [Guide → Publishing](guide/publishing).

## `initialized`

Written by `canary init` on a successful scaffold; not something you should hand-write or hand-edit. Lets a later `init` run against the same directory detect it's already a Canary project and refuse without `--force`.
