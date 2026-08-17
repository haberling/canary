// Entry point for renderMode "hybrid" (see PLAN.md's Render modes section).
// Every route already has a prerendered file: cold loads get it straight
// from the server, no JS required. Once this script has loaded, in-app
// navigation intercepts the hash change and fetches the TARGET route's own
// prerendered HTML file, extracting just the #app fragment and splicing it
// in -- no re-render, no raw markdown ever reaches the client, and cold vs.
// warm navigation show byte-identical content because it's the same file
// either way.

import { registerRouteHandler, start, type Route } from "./router.js";
import { loadNav, updateActiveNav } from "./nav.js";

function hrefFor(path: string): string {
  return `#/${path}`;
}

// "" for the site root, matching the pinned Home nav item's own `path`
// field (see Canary.Core.Manifest.ManifestBuilder.BuildNav) -- no
// translation needed here, unlike spa-router.ts, since a prerendered URL of
// "/" already falls out of "" directly.
function contentFileFor(route: Route): string {
  return route.path === "/" ? "" : route.segments.join("/");
}

function prerenderedUrlFor(routeFile: string): string {
  return routeFile === "" ? "/" : `/${routeFile}/`;
}

// A cold load of a real prerendered URL (e.g. /games/tesselate/, reached
// directly or via an in-content link -- markdown-authored links resolve to
// real root-relative hrefs, not hash links, so clicking one is a genuine
// full page load) has NO hash fragment at all. The router's own route.path
// would misread that as "/" (site root) regardless of the real URL, since
// it only ever looks at location.hash -- wrong nav highlighting, even
// though content is unaffected (the initial-load branch never touches
// #app). The real pathname is the only reliable signal for "what page did
// we actually land on," so it seeds currentPath instead.
function currentPathFromLocation(): string {
  return window.location.pathname.replace(/^\/+|\/+$/g, "");
}

let initialLoad = true;
let currentPath = currentPathFromLocation();

registerRouteHandler(async (route) => {
  const app = document.getElementById("app");
  if (!app) return;

  // The very first route dispatch is always the cold-load case (start()
  // calls the handler synchronously, before any user interaction is
  // possible) -- #app already contains this exact page's prerendered
  // content, so skip the redundant fetch-and-replace. currentPath is
  // already correctly seeded from the real pathname above; the hash-derived
  // route for this same first dispatch is meaningless (see
  // currentPathFromLocation) and must NOT overwrite it.
  if (initialLoad) {
    initialLoad = false;
    return;
  }

  const file = contentFileFor(route);
  currentPath = file;
  updateActiveNav(file);

  app.innerHTML = "<p>Loading&hellip;</p>";
  const url = prerenderedUrlFor(file);
  try {
    const res = await fetch(url);
    if (!res.ok) {
      app.innerHTML = `
        <h1>Not found</h1>
        <p>No page at <code>${url}</code>.</p>
        <p><a href="#/">&larr; back home</a></p>
      `;
      return;
    }
    const html = await res.text();
    const fragment = new DOMParser().parseFromString(html, "text/html").getElementById("app");
    app.innerHTML = fragment ? fragment.innerHTML : "<h1>Error</h1><p>Could not parse this page.</p>";
  } catch {
    app.innerHTML = "<h1>Error</h1><p>Could not load this page.</p>";
  }
});

loadNav(hrefFor, () => currentPath);
start();
