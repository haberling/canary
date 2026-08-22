# canary.json

Lives at a site's root. JSON, one config per environment — no dev/prod split, nav and routes are always derived from `content.root` plus the generated manifest, never redeclared here.

```
{
  "site": { "name": "...", "baseUrl": "https://..." },
  "content": { "root": "content" },
  "output": { "dir": "docs" },
  "renderMode": "hybrid",
  "nav": { "depth": 1 },
  "serve": { "port": 6913 },
  "theme": { "shell": "shell.html", "base": "css/framework.css", "theme": "css/theme.css" },
  "widgets": { "copyDefaultsOnInit": true, "preferBuiltIn": false },
  "tools": { "breadcrumb": "tools/breadcrumb.sh" },
  "publish": "git add docs && git commit -m Publish && git push"
}
```

## `site`

- `name` — required. Your site's display name, used in the page chrome and `<title>`.
- `baseUrl` — required. Your site's real public URL, used for sitemap generation.

## `content`

- `root` — required. The directory (relative to `canary.json`) holding your markdown content.

## `output`

- `dir` — defaults to `docs`, matching GitHub Pages' "serve from `/docs` on `main`" convention. Everything `canary build` produces goes here.

## `renderMode`

`hybrid` or `static`. See [Guide → Render Modes](../guide/render-modes). Defaults to `hybrid`.

## `nav`

- `depth` — how many directory levels deep the generated nav menu recurses. Defaults to `1`. A page deeper than this limit still exists and gets a real URL either way — this only controls how deep the nav *menu* itself goes, not what's reachable. `0` or negative means unlimited depth.

## `serve`

- `port` — the port `canary serve` binds to when `--port` isn't passed on the command line. Defaults to `6913`. `canary init` generates a fresh random port per project instead of always writing this fixed default, so two sibling projects don't collide when both are served at once.

## `theme`

- `shell` — path to the shell HTML template (page chrome).
- `base` — path to the base framework stylesheet.
- `theme` — path to your own theme stylesheet, loaded after `base`.

## `widgets`

- `copyDefaultsOnInit` — defaults to `true`. Whether `canary init` copies the built-in widgets into your project's own `widgets/` folder as an editable starting point.
- `preferBuiltIn` — defaults to `false`. When `true`, always use Canary's built-in `downloads`/`slideshow` even if your project has its own local copies, without deleting them.

See [Guide → Widgets](../guide/widgets).

## `tools`

A name-to-shell-command map, the central registry a `.toolchain.json` in any content directory can reference by name. See [Guide → Content Toolchain](../guide/toolchain).

## `publish`

Optional. A single arbitrary shell command, run by `canary publish` after a fresh build. Not set by default. See [Guide → Publishing](../guide/publishing).

## `initialized`

Written by `canary init` on a successful scaffold; not something you should hand-write or hand-edit. Lets a later `init` run against the same directory detect it's already a Canary project and refuse without `--force`.
