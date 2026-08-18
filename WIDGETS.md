# Authoring a Canary widget

A widget is a reusable piece of markdown-embeddable content — a slideshow, a downloads box, whatever — that a page author invokes with a fenced code block instead of hand-writing HTML. This doc is a how-to for building one. For the design history and *why* it works this way, see `PLAN.md`'s "Widget system" section — this doc only covers the *how*.

## The core idea

A widget is **declarative on both sides, never code**:

- An `.html` file: a real [Mustache](https://mustache.github.io/)-syntax template.
- An optional sibling `.js` file: shared client-side behavior, only needed if the widget is interactive.
- An optional sibling `.css` file: styling for the widget, only needed if it needs any beyond what falls out of the page's own theme.

There is no fourth piece, no C# class to write, no registration step. `Canary.Core.Widgets.TemplateWidgetRenderer` is the entire build-time "renderer" for every widget, built-in or site-authored — it parses a markdown fence block's body as YAML and fills the matching `.html` template with it. That's it.

**All three file types are discovered and shipped identically, built-in or site-authored — no widget ever gets special-cased treatment another widget can't also have.** This wasn't always true: the built-in `downloads`/`slideshow` widgets' CSS used to be hardcoded directly into the site's own `framework.css`, which meant a site-authored widget had no equivalent place to put its styling. Fixed by giving `.css` the same discovery/copy/link treatment `.js` already had — see the `.css` section below.

## Step by step: adding a new widget

1. **Pick a name** — this becomes both the filename and the fence tag. Say you're building a `callout` widget.
2. **Create `widgets/callout.html`** in your site root (next to `content/`, `config.json`, etc.) — create the `widgets/` folder if it doesn't exist yet. Write it as a plain Mustache template (see syntax below).
3. **(Optional) create `widgets/callout.js`** alongside it, if the widget needs any client-side interactivity (click handlers, etc.).
4. **(Optional) create `widgets/callout.css`** alongside it, if the widget needs its own styling.
5. **Use it in content** — any `.md` file can now write:
   ````markdown
   ```callout
   title: Heads up
   body: This is a callout.
   ```
   ````
   No config entry, no import, nothing to wire up. `Canary.Core.Widgets.WidgetDiscovery` finds `callout.html`/`callout.js`/`callout.css` by filename alone (case-insensitive) the moment `canary build` runs.
6. **Rebuild and check the output.**

That's the whole workflow. The rest of this doc is reference material for steps 2–4.

## Discovery rules (where widget files live, and precedence)

`WidgetDiscovery.Discover` looks in two places, one file extension at a time (`*.html` for templates, `*.js` for behavior scripts, `*.css` for styling):

- **Built-in**: `runtime/widgets/` in the Canary repo itself (source), copied unchanged to `runtime/dist/widgets/` as part of Canary's own build — `downloads` and `slideshow` (each with an `.html`, `.js`, and `.css`) live here today.
- **Site-authored**: `<siteRoot>/widgets/` — a `widgets/` folder next to your site's `config.json`.

**Site-authored wins on a filename collision.** If your site has its own `widgets/downloads.html`, it's used instead of the built-in one, no config flag needed. (The `widgets.preferBuiltIn` config field is meant to flip that precedence back — it's schema-only right now, not wired up to `WidgetDiscovery` yet.)

Filenames are matched **case-insensitively** and **without extension** — `Downloads.html` and `downloads.html` are the same widget as far as a ` ```downloads ` fence tag is concerned.

## The fence block syntax (what a content author writes)

````markdown
```<widgetname>
<YAML body>
```
````

The fence *info string* is just the widget's name — nothing else. There used to be a `name:title` suffix convention; it's gone. **If a widget wants a title, it's just a `title` field in the YAML body**, like any other field:

```yaml
title: Optional Title
items:
  - label: "Windows Installer"
    url: "https://example.com/file.msi"
```

A template then handles a missing title with `{{^title}}Downloads{{/title}}{{title}}` (see `downloads.html` for the real version of this).

### The YAML subset

`Canary.Core.Templating.YamlParser` is a hand-rolled parser for a **practical subset** of real YAML — real syntax, not a bespoke format, but deliberately not the whole spec:

**Supported:**
- Block-style mappings (`key: value`)
- Lists of mappings or scalars (`- key: value` / `- bare value`)
- Nested maps/lists via indentation
- Quoted (`"..."`/`'...'`) or bare scalars
- `true`/`false` as scalar text (a widget template treats `"false"` as falsy — see Mustache section)
- `#`-prefixed comment lines

**Not supported** (don't reach for these): flow style (`{...}`/`[...]`), anchors/aliases, multi-line block scalars (`|`/`>`), multi-document streams.

### The `!url` tag

Any relative path an author writes (`content/games/manual.pdf`) needs to become root-relative (`/content/games/manual.pdf`) to actually resolve once a page is served from a nested route. This is **opt-in and explicit**, not automatic — Canary does not guess based on a field being named `url` or `src`. Tag the specific scalar value that needs it:

```yaml
url: !url "content/games/manual.pdf"
```

vs. a plain URL that's already fine as-is:

```yaml
url: "https://example.com/file.msi"
```

`!url` works on a top-level scalar or one inside a list item. The resolution rule (`Canary.Core.Templating.UrlResolver.Resolve`) is: leave alone anything that's already absolute (`https://...`), already root-relative (`/...`), a fragment (`#...`), or a `mailto:`/`tel:` link; otherwise prepend a leading `/`. A plain `url:`/`src:` field with no `!url` tag is left byte-for-byte as written — if it's a relative path, that's the author's problem, not something Canary will silently "fix" for you.

## The template syntax (what you write in the `.html` file)

`Canary.Core.Templating.MustacheTemplate` is a hand-rolled implementation of a **practical subset** of real Mustache:

- `{{var}}` — HTML-escaped scalar output.
- `{{#section}} ... {{/section}}` — if `section` is a list, renders the body once per item (with that item as context); if it's a map, renders once against that map; if it's a truthy scalar (e.g. `copy: true`), renders once against the *outer* context (so sibling fields like `{{label}}` are still reachable inside).
- `{{^section}} ... {{/section}}` — inverted: renders the body (against the outer context) only if `section` is falsy/missing/empty. A value counts as falsy if it's missing, an empty string, the literal string `"false"`, or an empty list.

**Not supported**: partials (`{{> other}}`), unescaped/triple-mustache output (`{{{var}}}`), lambdas. There is no way to emit raw HTML from a YAML value — everything through `{{var}}` gets escaped. If you need conditional structure, use sections, not string concatenation.

### One gotcha specific to this templater

**Never write a literal `{{tag}}` in your own template's documentation comments.** The templater has no concept of "this is inside an HTML comment, skip it" — it scans the whole file for `{{`/`}}` and executes whatever it finds, including inside a `<!-- -->` block. This has actually broken things twice in this codebase already (the shell chrome template, and the downloads widget's own doc comment). Describe the syntax in prose instead:

```html
<!-- BAD: this gets executed for real, will corrupt output
     An "items" section: {{#items}}...{{/items}}
-->

<!-- GOOD -->
<!-- An "items" section iterates the items list, same idea as any Mustache section. -->
```

## The `.js` behavior file (optional, only for interactive widgets)

If your widget needs client-side behavior (a button, a toggle, keyboard nav), give it a sibling `.js` file with the same base name (`callout.js` next to `callout.html`). Key constraints, both load-bearing:

1. **It's copied once and referenced once per page**, not once per widget instance. `SiteBuilder` copies every discovered behavior script to `output/js/widgets/<name>.js` and the page shell's `{{widgetScripts}}` placeholder gets a `<script src="/js/widgets/<name>.js" defer>` tag for it — **every page on the site gets every discovered widget's script tag**, whether or not that page actually uses the widget (a v1 simplicity tradeoff: a few extra small requests on pages that don't need it, instead of building per-page usage tracking).
2. **Use event delegation on `document`, not per-instance listeners.** Your script runs once, on page load — it can't call `addEventListener` on a specific widget's DOM node, because in `hybrid` mode, a widget instance can appear *later*, spliced in by the fragment-fetch router without a page reload. Delegate instead:

   ```javascript
   document.addEventListener("click", (e) => {
     const button = e.target.closest(".callout-dismiss");
     if (!button) return;
     // ...
   });
   ```

   This handles a newly-swapped-in instance automatically — no re-init call needed anywhere in the router code.
3. **For "run setup once when a new instance first appears"** (something delegation can't cover — e.g. starting an autoplay timer the moment a specific slideshow's first image loads), use a `MutationObserver` watching for new widget nodes, not a manual init hook. See `slideshow.js` for a real example of this pattern.

Look at `runtime/widgets/downloads.js` (simple, one delegated click handler) and `runtime/widgets/slideshow.js` (more involved: delegation + a `MutationObserver` for autoplay/first-load setup) as reference implementations.

## The `.css` stylesheet file (optional, for any widget that needs its own styling)

Same idea as `.js`: a sibling `.css` file with the same base name (`callout.css` next to `callout.html`), entirely optional. `SiteBuilder` copies every discovered widget stylesheet to `output/css/widgets/<name>.css` and the page shell's `{{widgetStyles}}` placeholder gets a `<link rel="stylesheet" href="/css/widgets/<name>.css">` for it — same site-wide-not-per-page-usage tradeoff as `{{widgetScripts}}`, every page links every discovered widget's stylesheet whether or not that page uses it.

Your widget's CSS can reference the site's own color tokens (`--bg`, `--bg-elevated`, `--text`, `--text-dim`, `--border`, `--accent`, defined on `:root` in `framework.css`) directly — `var(--accent)` etc. work in your widget's stylesheet the same as anywhere else, since custom properties cascade globally once defined, regardless of which `<link>`ed file references them. That's how the built-in widgets stay visually consistent with whatever theme a site has applied without hardcoding any color themselves. See `runtime/widgets/downloads.css`/`slideshow.css` for real examples of this.

Don't reach into the site's own `framework.css`/`theme.css` to style your widget, even though nothing stops you technically — that file is the site's own base layer, not a place for a specific widget's rules to live. Keep every widget fully self-contained across all three of its files, same reasoning as everything else in this doc.

## Checking your work: `canary widgets` / `canary widget <name>`

- **`canary widgets [--config <path>]`** — lists every discovered widget name (built-in + site-authored), sorted. Confirms your new widget was actually found before you go debug a fence tag typo.
- **`canary widget <name> [--config <path>]`** — prints that widget's ready-to-paste usage example. This is pulled straight from a `<!--clipboard ... -->` comment block inside the widget's own `.html` file — **include one when you write a new widget**, so anyone (human or another Claude instance) can look up correct usage without reading your template source:

  ````html
  <!--clipboard
  ```callout
  title: Heads up
  body: This is a callout.
  ```
  -->
  ````

  Print-only, not real OS clipboard access (no cross-platform clipboard API in .NET) — pipe it yourself if you want it on your clipboard (`canary widget callout | pbcopy` etc.).

## Incremental builds do see widget edits

`canary build`'s checksum-gating folds every discovered widget file's content (all three types — `.html`, `.js`, `.css`) into every page's cache key, so editing any widget correctly invalidates the cache for pages that use it, not just the page whose own markdown changed. This didn't used to be true and is worth knowing the shape of if you're ever debugging a stale-output-feeling issue: the check is site-wide (any widget file changing invalidates every page, not just pages that reference that specific widget) rather than tracking per-page usage — a deliberate simplicity tradeoff, same one already made for `{{widgetScripts}}`/`{{widgetStyles}}` being site-wide too.

## Full working examples

Both built-in widgets are real, complete reference implementations — read them before writing your first custom one:

- `runtime/widgets/downloads.html` + `.js` + `.css` — a list of download links, with a conditional branch for a copy-to-clipboard command row (`{{#copy}}`/`{{^copy}}`), a `!url`-tagged relative path example, and CSS that uses the site's own color tokens.
- `runtime/widgets/slideshow.html` + `.js` + `.css` — a list-of-slides section, a title with a fallback, the more involved `.js` pattern (delegation + `MutationObserver` for autoplay), and CSS for the viewport/nav/dots layout.
