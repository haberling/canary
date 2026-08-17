# Canary — Website Framework

## What Canary is

Canary is a standalone C# website framework, distributed as a native-feeling CLI tool (`canary`/`canary.exe`). A "Canary site" is a directory containing a JSON project configuration file plus content (markdown) and theme assets; `canary build`/`canary serve` operate on that directory the same way for any site that adopts it — consoland included, eventually, but consoland is now a *consumer* of Canary, not the thing Canary is extracted from and tied to.

This supersedes the original framing of "split the framework pieces out of consolandWebsite." That extraction is still where the initial code comes from (see **Origin**, below), but the target is a general-purpose tool, not a one-off migration.

## Origin (institutional memory, not the current goal)

Canary began as a plan to split consolandWebsite's hand-rolled TypeScript runtime (hash router + markdown renderer + nav-from-manifest) and C# build/deploy tooling out from that site's actual content and branding — motivated by wanting the markdown-wiki site to be crawlable without losing its "no framework" hand-built character.

**Why hash routing existed in consoland in the first place**, worth carrying forward: GitHub Pages is pure static file serving with no server-side rewrites available (short of the "404.html redirect" trick). Hash routing needs zero server config — any request loads `index.html`, and the fragment after `#` is never even sent to the server, so client-side JS alone decides what renders. That's still a mode Canary needs to support well; it's just no longer the *only* mode.

## Distribution & consumption model

Canary should feel like a native CLI tool to use — `canary build`, `canary serve`, `canary.exe build` on Windows — not something invoked through `dotnet tool run` ceremony. Long-term intended distribution channel is **winget**, not a NuGet/`dotnet tool install` package; calling `dotnet tool run` for every invocation was explicitly rejected as unnatural. That preference should inform build/publish choices later (e.g. self-contained or Native AOT single-file publish, so end users don't need the .NET SDK/runtime preinstalled just to run it) — but that's a later-phase concern, not something to solve now.

For now, **local/source-only**: build and run Canary from source while it's this early. No public package (winget or otherwise) published yet — revisit once the CLI surface has stabilized through the phases below.

## Architecture: two layers

1. **C# core (the CLI/build engine)** — config loading, manifest generation, prerendering, incremental builds, output writing, sitemap/robots generation. This is Canary's actual product.
2. **Vendored client runtime (TypeScript, compiled to JS)** — the hash router, markdown renderer, and content widgets (download links, slideshow) that run in the visitor's browser. This still needs a TS toolchain to *develop*, but is pre-compiled and embedded into the Canary package at release time — a site author consuming Canary never runs `npm`/`node` themselves.

### Client runtime packaging

- TS source lives in Canary's own repo (e.g. `runtime/ts/`) and is compiled to JS via the existing npm-based toolchain, but only as part of *Canary's own* release/build process.
- The compiled output gets embedded into the Canary package (as embedded resources in the C# project, or similar) and written out to each site's build directory by `canary build`.
- Net effect: **zero Node dependency for anyone consuming Canary**, Node only matters if you're developing Canary itself.

## Render modes (config-selectable, not one-size-fits-all)

Original consoland decision was hybrid-only: prerender to real static paths *and* keep the hash-router SPA layer for in-app nav. Generalizing this for arbitrary sites, `renderMode` becomes a config field with three values. All three modes still run JS for nav population (`loadNav()` reading `manifest.json`, same as today) and for widgets (slideshow, downloads) — "how much JS runs" isn't what distinguishes the modes. What actually differs is **how the content itself gets from source to screen**:

- **`hybrid`** (what consoland needs) — a real prerendered HTML file exists per route, so cold loads (crawlers, direct links, social-preview scrapers) get real content immediately. For in-app navigation once JS has loaded, the router intercepts the click and **fetches the target route's own prerendered HTML file, extracts the `<main id="app">` fragment via `DOMParser`, and splices it into the DOM** — no full reload, and critically, no re-rendering. It's the same file, same bytes, whether you arrive cold or navigate in warm. Raw markdown is never shipped to or parsed by the client in this mode.
- **`static`** — same prerendered HTML files as hybrid, but no hash-router/content-swap layer at all. Every internal link is a real `<a href="/games/tesselate/">` href; every navigation is a full page load, handled by the browser/server, not JS. Nav and widgets still run as page-enhancement JS. No markdown shipped to the client here either — nothing needs it, pages already arrived pre-rendered.
- **`spa`** — no prerendered per-route files, just `index.html`, closer to consoland's original pre-Canary behavior. This is the **only mode that ships raw markdown documents to the client and renders them in-browser** — there's no prerendered HTML to draw from, so the client-side markdown renderer is load-bearing here, not just a leftover. Not crawlable, but simplest; useful for internal tools or sites that don't care about SEO.

Net effect: the client-side markdown renderer only matters at runtime for `spa`. For `hybrid`/`static`, markdown→HTML only happens at build time; the browser never sees markdown source. Each mode is a legitimate, independently selectable output — this is a bigger surface area than the original plan (which only ever needed hybrid), so the prerender step, the router, and the markdown renderer all need to work standalone, not just together.

**Runtime bundle is tailored per `renderMode`**, not one-size-fits-all: `canary build` assembles the JS it writes to a `hybrid`/`static` site's output without the markdown renderer at all — only a `spa` site's bundle includes it. Keeps `hybrid`/`static` output lean and makes the "markdown never reaches the browser outside `spa`" property enforced by what's shipped, not just by convention.

## Core built-in functionality (absorbed from consoland's tools)

`tools/build-manifest.cs` and `tools/deploy.cs` are not going to survive as separate opt-in files a site has to wire up — their functionality (manifest/nav generation, incremental build) is core enough that it becomes **built into the Canary executable itself**, generalized away from consoland specifics (no hardcoded domain, no baked-in CNAME, no consoland branding assumptions).

**What "deploy" actually reduces to.** The real end goal across all render modes is a website that lives on a file server — Canary's job stops at producing correct output in a configurable output folder (`output.dir`, defaulting to `docs` for GitHub Pages' "serve from `/docs` on main" convention). For a git-based static host like GitHub Pages, that's the *entire* deploy story: `canary build` writes to `docs/`, the site owner commits and pushes it themselves — Canary shouldn't be auto-committing/pushing either. No bespoke GitHub-specific deploy step is needed.

A **remote push mechanism (FTP/SFTP)** is flagged as a real but later feature — build locally, verify the output, then send the already-built folder to a remote host, matching how a lot of flat-file/shared hosting actually works. This is genuinely a v-later item, not part of the core build/output story.

## Plugin system — cut from scope (for now)

Originally planned as build-time C# extensibility via Roslyn scripting (a site-supplied `.cs` file, run in-process, direct access to Canary's build objects). **Deliberately axed for now** — premature to design an extension mechanism before the core it would extend even exists. If a real need shows up once Phases 0–3 are built, revisit then with an actual use case driving the hook points, rather than speculating about them now. Not on the phase list below.

This has no bearing on **widgets** (client-side/browser-executed JS — download links, slideshow), which remain part of the vendored runtime regardless.

## Project configuration file (JSON)

Format is JSON (matches the existing `manifest.json` convention, zero new parsing dependency). v1 shape — one config per environment (no dev/prod split), nav/routes always derived from `content.root` + generated manifest (never redeclared in config), single flat content root:

```json
{
  "site": { "name": "...", "baseUrl": "https://..." },
  "content": { "root": "content/" },
  "output": { "dir": "docs" },
  "renderMode": "hybrid",
  "theme": { "shell": "templates/shell.html", "base": "css/framework.css", "theme": "css/theme.css" }
}
```

No `deploy` field yet — see **Core built-in functionality** above for why: `output.dir` is the entire deploy story for git-served hosts. A remote-push config block (FTP/SFTP target, credentials reference, etc.) gets added once that feature actually exists, not speculatively now.

## CLI surface

- `canary init` — scaffold a new site. Two modes: pass an existing `config.json` to scaffold from directly, or run interactively and be prompted for each option one at a time (site name, base URL, render mode, content root, ...).
- `canary build` — full build: manifest generation, chrome re-stamp (always), content render (checksum-gated per **Incremental builds**, below), sitemap/robots generation, all written to `output.dir`. For a git-served host like GitHub Pages, this *is* the deploy step — the site owner commits/pushes `output.dir` themselves.
- `canary serve` — local dev server against the config, for iterating before committing/pushing.
- *(later, optional)* a remote-push verb for hosts that need file transfer rather than a git-served folder (FTP/SFTP) — not part of v1, see **Core built-in functionality** above.

## Incremental builds (carried over, still the plan)

- Chrome (header/nav/footer skeleton + script/style tags) is **unconditionally re-stamped every build** — it's cheap and always current from one source template, so there's no "did it change" case to special-case.
- Content rendering (markdown → HTML) is the expensive, diff-noisy part, so it's the only thing gated: each generated page embeds a `<!-- source-checksum: sha256:<hash> -->` marker; the build step compares that to the current source's hash and skips re-rendering when unchanged.
- `canary build` writes only changed files into `output.dir`, rather than wiping and recreating the whole directory every time — keeps git diffs against `output.dir` small and meaningful.

## Local dogfood workspace

A gitignored `workspace/` directory holds a **full fake sample site** used to exercise Canary end-to-end during its own development — real config, real markdown content, built and served through the actual CLI, not just unit tests. Theme: an informational site about the Gilbert & Sullivan opera *The Pirates of Penzance*. Fully disposable, never committed, never a real published site.

## Resources ported from consolandWebsite (as a starting point, then generalized)

| Bring over | Fate under the new plan |
|---|---|
| `tools/build-manifest.cs` | Reference for the manifest-generation logic to reimplement as core Canary functionality (C#, in the CLI itself). |
| `tools/deploy.cs` | Reference for the incremental-write-to-output-folder logic to reimplement as core Canary functionality, generalized off consoland's hardcoded domain/CNAME/GitHub-specific assumptions. Its actual GitHub Pages "deploy" behavior reduces to nothing more than writing to `output.dir`. |
| `src/ts/router.ts` | Becomes part of the vendored client runtime; in `hybrid` mode gains the "fetch target route's prerendered HTML, extract the `#app` fragment, splice it in" navigation mechanism (replacing raw-markdown re-fetch/re-render for warm nav). |
| `src/ts/main.ts` | Wires router + manifest fetch + (mode-dependent) content loading together; part of vendored runtime. |
| `src/ts/markdown.ts` | Markdown→HTML renderer. Only ships to/runs in the browser for `spa` mode. For `hybrid`/`static`, it's a build-time-only concern — reused or reimplemented for the C# prerender step, never shipped to the client. |
| `src/ts/widgets/downloads.ts`, `src/ts/widgets/slideshow.ts` | Client-side widgets, part of vendored runtime. |
| `scripts/dev-server.mjs` | Reference for `canary serve`; likely reimplemented in C# rather than kept as a Node script, to avoid a Node dependency at consume-time. |
| `tsconfig.json`, `package.json` (scripts) | Starting point for Canary's *own* internal TS build step (dev-only, not shipped to consumers). |

**Still not brought over** (consoland-specific, not framework): `content/**`, `branding/**`, `src/img/logo.png`, the Google Analytics tag, `docs/CNAME`/`consoland.net` domain wiring. `src/css/style.css` is *not* brought over as-is either — decided: split it now (see Work items, Phase 0) into a generic framework base layer and a consoland-specific theme layer, rather than deferring the split until a second site exists.

## Work items / rough phase order

- [x] **Phase 0 — Bootstrap the C# project.** Done. `Canary.slnx` solution: `src/Canary` (CLI), `src/Canary.Core` (build-engine library), `tests/Canary.Core.Tests`. JSON config loader (`Canary.Core.Config`) implemented and unit-tested (8 tests) against the v1 schema, with `canary build --config <path>` wired up as a smoke-testable CLI command (loads/validates/prints config — no real build pipeline yet, that's Phase 1). TS runtime source ported to `runtime/ts/` (router, main, markdown, both widgets) and compiles clean to `runtime/dist/` via `npm run build` — confirms the vendoring pipeline works, though wiring the compiled output into the C# package as embedded, per-mode-tailored resources is Phase 1 (that's where the tailored-bundle logic actually lives). `src/css/style.css` split into `templates/default/css/framework.css` (generic base layer, committed here) + consoland's theme overrides (written to scratchpad, not committed — consoland-specific, belongs in consoland's own repo once it migrates to Canary).
- [ ] **Phase 1 — Render pipeline.** Template/chrome wrapper (config-driven, not baked into one `index.html`). Prerender step. `renderMode` switch (`hybrid`/`spa`/`static`) each functioning standalone — including the hybrid fragment-fetch navigation mechanism (fetch prerendered HTML, extract `#app`, splice in). Checksum tagging + incremental content re-render. Embed the vendored runtime into the C# package, tailored per `renderMode` (markdown renderer only in `spa` bundles, per the earlier decision).
- [ ] **Phase 2 — Serve.** `canary serve` local dev server against the config.
- [ ] **Phase 3 — SEO plumbing.** `sitemap.xml` generation from the route list + config `baseUrl`. `robots.txt` generation.
- [ ] **Phase 4 — Scaffolding.** `canary init`, both config-file and interactive modes.
- [ ] **Phase 5 — Dogfooding.** Build the Pirates of Penzance sample site in `workspace/`, exercised through every phase above as it lands, not bolted on at the end.
- [ ] **Phase 6 — Remote push (later/optional).** FTP/SFTP-based deploy for hosts that need file transfer rather than a git-served folder. Not committed roadmap, just a known future need.

Plugin system explicitly not on this list — see **Plugin system — cut from scope** above.

## Open questions still unresolved

- **Remote-push (FTP/SFTP) config shape and credential handling** — deferred along with the feature itself (Phase 6), but worth noting credentials-in-a-JSON-config is a real question to get right (env var reference vs. OS credential store vs. something else) whenever that phase starts.

## Resolved (for reference — no longer open)

- **Deploy targets beyond GitHub Pages** → not building a target abstraction; `output.dir` is the whole story for git-served hosts, remote push is a distinct later feature (Phase 6).
- **Plugin system** → cut from scope for now; revisit only if a real use case shows up post-Phase 3.
- **CSS framework/theme split** → split now (Phase 0), not deferred, using consoland's actual `style.css` as the source to split.
- **Packaging/versioning** → local/source-only for now; long-term is winget with a native `canary`/`canary.exe` command, explicitly not `dotnet tool run` ceremony. Revisit package id/SemVer once the CLI surface stabilizes.
- **Does `static` mode ship zero client JS?** → no, nav (`loadNav()`) and widgets still run as JS in every mode; what's mode-specific is content routing/rendering, not "JS or no JS."
- **Config schema for v1** → settled shape above; single config (no env split), nav/routes always derived from content + manifest (never redeclared), single flat content root.
- **Runtime bundle: one-size-fits-all vs. per-mode-tailored** → tailored per `renderMode`; markdown renderer only ships in `spa` bundles.

## Immediate next steps

Phase 0 is done (see Work items above). Next, starting Phase 1:

1. Template/chrome wrapper: extract the header/nav/`#app`/footer skeleton (currently baked into consoland's `src/index.html`) into a config-driven shell template (`theme.shell`), stamped onto every generated page.
2. Prerender step in `Canary.Core`: render markdown → HTML at build time for a route, writing `<route>/index.html` with root-relative asset paths.
3. `renderMode` switch: get `hybrid`, `spa`, and `static` each functioning standalone, including the hybrid fragment-fetch navigation mechanism.
4. Checksum tagging + incremental content re-render (the `<!-- source-checksum: sha256:... -->` marker mechanism).
5. Embed the vendored runtime into the C# package, tailored per `renderMode`.
