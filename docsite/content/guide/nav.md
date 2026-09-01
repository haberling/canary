# Nav and `.nav.json`

Two knobs control the generated nav menu: `nav.depth` in `canary.jsonc`, and a `.nav.json` in each content directory. They do not control which pages exist. Every markdown file still builds to a real URL and is listed in `sitemap.xml`, whether the nav menu shows it or not.

`canary explore nav` (or `navigation`) walks this curated tree. `canary explore toolchain` is a different tree — every content directory, unbounded by nav depth. See [CLI](reference/cli).

## `nav.depth`

How many directory levels the menu recurses. Defaults to `1` (top-level items, with one dropdown of children). A page deeper than this still has a URL; it just isn't a menu entry. `0` or negative means unlimited depth. See [canary.jsonc](reference/config).

## `.nav.json`

Canary auto-creates a self-documenting `.nav.json` in every content directory that has at least one `.md` file or a nested subdirectory, at any depth the nav tree actually reaches. Edit that file; don't invent a parallel config.

```
{
  "allow": null,
  "deny": null,
  "priority": 0,
  "nonav": false
}
```

- `allow` / `deny` — mutually exclusive filename lists (e.g. `["secret.md"]`). `allow` is a whitelist of which sibling `.md` files become dropdown children; `deny` is a blacklist. Specifying both fails the build. Neither applies to the directory's own `index.md` landing page.
- `nonav` — hide this directory from the nav menu. The pages still build, still have URLs, and still appear in the sitemap. Nav visibility and crawler discoverability are independent.
- `priority` — integer, default `0`. Lower sorts earlier among siblings. Home (`content/index.md`) is always pinned first, regardless of this value.

Home is always titled "Home" and always first. Everything else sorts by `priority`, then by title.
