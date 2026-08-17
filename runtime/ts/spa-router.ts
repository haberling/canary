// Entry point for renderMode "spa" -- no prerendered files exist, so this is
// the only mode that fetches raw markdown and renders it in-browser (see
// PLAN.md's Render modes section). Closest to consoland's original
// pre-Canary behavior.

import { registerRouteHandler, start, type Route } from "./router.js";
import { renderMarkdown } from "./markdown.js";
import { loadNav, updateActiveNav } from "./nav.js";

function hrefFor(path: string): string {
  return `#/${path}`;
}

// Content root's own landing page is content/index.md (see
// Canary.Core.Manifest.ManifestBuilder), so the root route fetches "index",
// not consoland's old "home".
function contentFileFor(route: Route): string {
  return route.path === "/" ? "index" : route.segments.join("/");
}

// The fetched *file* is "index" for the root route, but the nav item's own
// `path` field is "" for that same page (see ManifestBuilder.BuildNav) --
// these represent the same page with two different conventions for two
// different purposes, same as hybrid-router.ts's comment on contentFileFor.
function navPathFor(file: string): string {
  return file === "index" ? "" : file;
}

let currentFile = "index";

registerRouteHandler(async (route) => {
  const app = document.getElementById("app");
  if (!app) return;

  const file = contentFileFor(route);
  currentFile = file;
  updateActiveNav(navPathFor(file));
  app.innerHTML = "<p>Loading&hellip;</p>";

  try {
    const res = await fetch(`/content/${file}.md`);
    if (!res.ok) {
      app.innerHTML = `
        <h1>Not found</h1>
        <p>No page at <code>content/${file}.md</code>.</p>
        <p><a href="#/">&larr; back home</a></p>
      `;
      return;
    }
    const source = await res.text();
    app.innerHTML = await renderMarkdown(source);
  } catch {
    app.innerHTML = "<h1>Error</h1><p>Could not load this page.</p>";
  }
});

loadNav(hrefFor, () => navPathFor(currentFile));
start();
