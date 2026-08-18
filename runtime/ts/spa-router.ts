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

// `file` might be a leaf page (content/<file>.md) or a directory's own
// landing page (content/<file>/index.md, per ContentScanner/ManifestBuilder's
// "index.md is a directory's landing page" convention -- see PLAN.md's
// Known bugs entry on this). The client has no route manifest saying which
// paths are directories up front, so this tries the leaf file first and
// falls back to the directory-landing-page form on a 404 rather than
// guessing. Root ("index") never needs the fallback -- it's already the
// exact landing-page file.
async function fetchMarkdownSource(file: string): Promise<string | null> {
  const leaf = await fetch(`/content/${file}.md`);
  if (leaf.ok) return leaf.text();

  if (file !== "index") {
    const dirLanding = await fetch(`/content/${file}/index.md`);
    if (dirLanding.ok) return dirLanding.text();
  }

  return null;
}

registerRouteHandler(async (route) => {
  const app = document.getElementById("app");
  if (!app) return;

  const file = contentFileFor(route);
  currentFile = file;
  updateActiveNav(navPathFor(file));
  app.innerHTML = "<p>Loading&hellip;</p>";

  try {
    const source = await fetchMarkdownSource(file);
    if (source == null) {
      app.innerHTML = `
        <h1>Not found</h1>
        <p>No page at <code>content/${file}.md</code>.</p>
        <p><a href="#/">&larr; back home</a></p>
      `;
      return;
    }
    app.innerHTML = await renderMarkdown(source);
  } catch {
    app.innerHTML = "<h1>Error</h1><p>Could not load this page.</p>";
  }
});

loadNav(hrefFor, () => navPathFor(currentFile));
start();
