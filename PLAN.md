# Canary — Prerendering, Sitemap & Incremental Build

## Context

Canary is the site-engine being split out of `consolandWebsite`: the hand-rolled TypeScript runtime (hash router + markdown renderer + nav-from-manifest) and the C# build/deploy tooling, minus that site's actual content and branding. It was born out of a design discussion about making the markdown-wiki approach crawlable by search engines without losing the "no framework" hand-built character of the original site.

The problem that started this: `consolandWebsite` is a client-side SPA using hash routing (`#/games/Tesselate`) that fetches markdown at runtime and renders it into `#app`. That's invisible to most web crawlers — URL fragments are never sent to the server, so a sitemap of hash URLs would list N URLs that all resolve to the same document. The fix decided on is prerendering (SSG): generate a real static HTML file per content page, so `loc` in a sitemap can equal an actually-servable URL.

## Why hash routing existed in the first place

Worth carrying forward as institutional memory, not relitigating:
- GitHub Pages is pure static file serving — no server-side rewrites available without the hacky "404.html redirect" trick.
- Hash routing needs zero server config: any request just loads `index.html`; the fragment after `#` is never even sent to the server, so the JS router alone decides what renders.
- `index.html` links assets with relative paths (`css/style.css`, `js/main.js`). That only works because every "page" is the same URL under hash routing — real paths at nested depths would break those relative references unless converted to root-relative paths (which prerendering should do anyway, see below).

## Design decisions already locked in

1. **Don't point a sitemap at raw `.md` files or hash-fragment URLs.** A sitemap's `loc` must equal a real, directly-browsable URL — that's the whole point of a sitemap, and pointing it at something else (a raw markdown file with no chrome, or a fragment the server never sees) was rejected.
2. **Prerender to real static paths, keep the hash-routed SPA for in-app navigation.** Each content page gets a real file at a real path (e.g. `games/tesselate/index.html`), which GitHub Pages serves natively — no rewrite trickery needed for those exact paths. The existing hash router keeps working unchanged for clicking between pages once JS has loaded; this is additive, not a routing rewrite.
3. **Header/nav/footer chrome is not duplicated content, it's an identically-referenced skeleton.** Every generated page ships the same static header/footer shell plus the same `<script src="js/main.js">` tag — referenced identically everywhere, the same way every page already references `css/style.css` identically today. Nav itself stays populated client-side at runtime via `loadNav()` reading `manifest.json`, exactly as it works now. The only genuinely new content per generated file is the rendered `<main id="app">` block.
4. **Chrome is unconditionally re-stamped every build; only content-rendering is checksum-gated.** The header/footer skeleton is cheap to regenerate and always current from its one source template — there's no "did the template change" case to handle because it's never skipped in the first place. Only the markdown→HTML render step (the part with real cost, and the part that produces noisy diffs) is worth skipping when unchanged.
5. **Incremental build via a per-page source checksum embedded in the output.** Each generated page gets a marker like `<!-- source-checksum: sha256:<hash of the .md source> -->` near the top. The build step reads the existing output file's checksum (if any), compares it to the current `.md` file's hash, and skips re-rendering that page's content when they match.
6. **Sitemap.xml lists the real prerendered paths; robots.txt points at the sitemap.** Straightforward once (2) exists.

## Work items

- [ ] **Template wrapper.** Extract the header/footer/script-tag skeleton (currently baked directly into `index.html`) into a single source template usable both for the SPA shell and for stamping every prerendered route.
- [ ] **Prerender step.** For every route in `manifest.json`, render its markdown to HTML at build time (port/reuse the existing `markdown.ts` renderer to run outside the browser — either a C# reimplementation or shelling out to Node against the compiled `markdown.js`) and write `<route>/index.html` with root-relative asset paths (`/css/style.css`, not `css/style.css`, since nested routes are now real directory depths).
- [ ] **Checksum tagging.** Embed the `source-checksum` comment/meta tag in each generated page as part of the prerender step.
- [ ] **Incremental deploy.** Change the build/deploy tool from "wipe `docs/` and recreate everything" to: for each route, compare current `.md` hash against the existing output's embedded checksum; skip the (expensive) content re-render if unchanged, but always re-stamp the (cheap) chrome around it regardless of the checksum result.
- [ ] **Router/main.ts tweak.** On initial page load, detect that `#app` already contains real prerendered content matching the current route and skip the redundant fetch-and-replace; keep fetching normally for subsequent in-app hash navigation.
- [ ] **Sitemap generation.** Extend the manifest-building tool (or add a sibling step) to emit `sitemap.xml` from the same route list, using the real prerendered paths.
- [ ] **robots.txt.** Two or three lines plus a `Sitemap: https://<domain>/sitemap.xml` pointer.

## Resources to bring over from `consolandWebsite`

These are the framework pieces — copy them as the starting point, not the site's actual content:

| Bring over | Notes |
|---|---|
| `tools/build-manifest.cs` | Nav/manifest generator; becomes core to Canary's build step and the basis for sitemap generation. |
| `tools/deploy.cs` | Becomes the base for the new incremental prerendering build tool — expect to rewrite most of its body per the work items above. |
| `src/ts/router.ts` | The hash router. Stays conceptually as-is; gets the "skip redundant initial fetch" tweak. |
| `src/ts/main.ts` | Wires router + manifest fetch + markdown render together. |
| `src/ts/markdown.ts` | The hand-written markdown→HTML renderer — needs to become runnable at build time, not just in-browser. |
| `src/ts/widgets/downloads.ts`, `src/ts/widgets/slideshow.ts` | Content widgets (installer download links, image slideshows) — generic enough to keep. |
| `scripts/dev-server.mjs` | Local dev server. |
| `tsconfig.json`, `package.json` (scripts section) | Starting point for the new project's build scripts. |

**Do not bring over** (these are `consolandWebsite`-specific, not framework):
- `content/**` — that site's actual pages/prose.
- `branding/**`, `src/img/logo.png` — that site's identity assets.
- The Google Analytics tag and Measurement ID in `index.html`'s `<head>`.
- `docs/CNAME` / the `consoland.net` domain wiring in `deploy.cs`.
- `src/css/style.css` as-is — likely worth reviewing for consoland-specific branding (colors, logo sizing) mixed into what should be framework-generic layout rules. Flag as a decision to make in Canary: split into a base framework stylesheet vs. a site theme layer, rather than copying verbatim.

## Open questions to resolve once work starts here

- **Relationship between Canary and `consolandWebsite` going forward.** Does Canary become an installable dependency the site pulls in, a template repo copied per-site, or does `consolandWebsite`'s own build eventually just call into Canary directly? Not decided — don't assume one while building.
- **Whether the CSS needs splitting** into a framework base layer vs. a per-site theme layer (see table above).
- **Whether the GitHub Pages 404.html SPA-fallback trick is needed anywhere** once most routes are real static files — probably not, since prerendered routes are literal files GitHub Pages serves natively, but confirm there's no remaining case (e.g. a route added to `manifest.json` without a prerendered file existing yet) that needs it.

## Immediate next steps

1. `git init` this directory.
2. Copy over the resources listed above from `consolandWebsite`.
3. Strip anything consoland-specific per the "do not bring over" list; replace with a minimal placeholder page for framework development (no need for real content yet).
4. Work the item list above roughly in order: template wrapper → prerender step → checksum tagging → incremental deploy → router tweak → sitemap → robots.txt.
