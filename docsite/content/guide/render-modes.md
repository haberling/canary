# Render Modes

`renderMode` in `canary.jsonc` is `hybrid` or `static`. Both modes prerender every route to real static HTML at build time — markdown is never shipped to or parsed by the browser in either one. What differs is **how in-app navigation happens** once a page has loaded, not how much JavaScript runs (nav population and widgets run as page-enhancement JS either way).

## `hybrid`

A real prerendered HTML file exists per route, so a cold load — a crawler, a direct link, a social-preview scraper — gets full content immediately, no JS required. Once JS has loaded, clicking an internal link is intercepted by the client router: it fetches the target route's own prerendered HTML file, extracts the `<main id="app">` fragment, and splices it into the current page. No full reload, and critically, no re-rendering — it's the exact same bytes a cold load would have gotten, just spliced in instead of navigated to. Navigation is keyed off the real URL path via the History API (`pushState`/`popstate`), so back/forward and copy-paste-the-URL both work normally.

This is the mode you want for a public site where both crawlability and snappy in-app navigation matter.

## `static`

The same prerendered HTML files as `hybrid`, but no router/content-swap layer at all. Every internal link is a real `<a href="/games/tesselate/">`, and every navigation is a full page load handled by the browser. Nav and widgets still run as page-enhancement JS — this mode isn't "no JS," it's "no client-side routing."

Simpler, and the right choice if you don't need warm in-app navigation to feel instant, or want to minimize what runs in the browser at all.

## Picking one

Default to `hybrid` unless you have a specific reason not to — it's a strict superset of `static`'s crawlability story with a nicer navigation experience layered on top, at the cost of a small router. If that router is unwanted complexity for your site (very few pages, or navigation speed genuinely doesn't matter), `static` is a legitimate, simpler choice, not a lesser one.
