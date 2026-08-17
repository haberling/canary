// Wires the router to content fetching: hash path -> content/<path>.md ->
// markdown render -> #app. Also populates the top nav from content/manifest.json.

import { registerRouteHandler, start, type Route } from "./router.js";
import { renderMarkdown } from "./markdown.js";

interface NavItem {
  title: string;
  path?: string | null;
  children?: NavItem[] | null;
}

interface Manifest {
  nav: NavItem[];
}

function escapeHtml(s: string): string {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

function renderNavItem(item: NavItem): string {
  const pathAttr = item.path ? ` data-path="${escapeHtml(item.path)}"` : "";
  const label = item.path
    ? `<a class="nav-label" href="#/${item.path}" data-path="${escapeHtml(item.path)}">${escapeHtml(item.title)}</a>`
    : `<button type="button" class="nav-label">${escapeHtml(item.title)}</button>`;

  if (!item.children || item.children.length === 0) {
    return `<li class="nav-item"${pathAttr}>${label}</li>`;
  }

  const dropdown = item.children
    .map((child) => `<a href="#/${child.path}" data-path="${escapeHtml(child.path ?? "")}">${escapeHtml(child.title)}</a>`)
    .join("");

  return `<li class="nav-item has-dropdown"${pathAttr}>${label}<div class="nav-dropdown">${dropdown}</div></li>`;
}

function updateActiveNav(): void {
  const nav = document.getElementById("site-nav");
  if (!nav) return;

  nav.querySelectorAll<HTMLLIElement>(".nav-item").forEach((li) => {
    const childLinks = Array.from(li.querySelectorAll<HTMLAnchorElement>(".nav-dropdown a"));
    const activeChild = childLinks.find((a) => a.dataset.path === currentFile);

    li.classList.toggle("active", li.dataset.path === currentFile || !!activeChild);
    childLinks.forEach((a) => a.classList.toggle("active", a === activeChild));
  });
}

async function loadNav(): Promise<void> {
  const nav = document.getElementById("site-nav");
  if (!nav) return;

  try {
    const res = await fetch("content/manifest.json");
    if (!res.ok) return;
    const manifest = (await res.json()) as Manifest;
    nav.innerHTML = `<ul class="site-nav-list">${manifest.nav.map(renderNavItem).join("")}</ul>`;
    updateActiveNav();
  } catch {
    // No manifest yet (or offline); leave nav as-is.
  }
}

function contentFileFor(route: Route): string {
  return route.path === "/" ? "home" : route.segments.join("/");
}

let currentFile = "home";

registerRouteHandler(async (route) => {
  const app = document.getElementById("app");
  if (!app) return;

  const file = contentFileFor(route);
  currentFile = file;
  updateActiveNav();
  app.innerHTML = "<p>Loading&hellip;</p>";

  try {
    const res = await fetch(`content/${file}.md`);
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

loadNav();
start();
