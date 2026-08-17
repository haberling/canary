// Entry point for renderMode "static". No hash router, no content-swap
// logic at all -- every internal link is a real <a href="/games/tesselate/">,
// every navigation is a full page load. The only JS this mode ships is nav
// population/highlighting (page-enhancement, same as every mode; see
// PLAN.md's Render modes section on why "how much JS runs" isn't what
// distinguishes the modes). Widgets need no JS file at all -- they're
// entirely self-contained via inline onclick/onload attributes.

import { loadNav } from "./nav.js";

function hrefFor(path: string): string {
  return path === "" ? "/" : `/${path}/`;
}

// No client router means no in-memory route state to read -- the current
// page's own URL is the only source of truth, and it's stable for this
// mode's whole lifetime (a full reload happens on every navigation anyway).
function currentPathFromLocation(): string {
  return window.location.pathname.replace(/^\/+|\/+$/g, "");
}

loadNav(hrefFor, currentPathFromLocation);
