// Populates the top nav from /content/manifest.json. Used by both render
// modes (static/hybrid) -- pure page-enhancement JS regardless of how
// content itself gets loaded/routed, per PLAN.md's Render modes section.
//
// hrefFor lets each mode decide the link shape: hybrid uses hash links
// ("#/<path>") for the client router to intercept; static uses real
// root-relative paths ("/<path>/") since there's no client router.
//
// Root-relative fetch ("/content/manifest.json", not "content/manifest.json")
// is required for hybrid/static: those prerender to real nested paths, so a
// page-relative fetch from e.g. /games/Tesselate/ would resolve one level
// too deep. Same fix as Canary.Core.Markdown.MarkdownRenderer.ResolveUrl on
// the build-time (C#) side, see PLAN.md.

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

function renderNavItem(item: NavItem, hrefFor: (path: string) => string): string {
  const pathAttr = item.path != null ? ` data-path="${escapeHtml(item.path)}"` : "";
  const label = item.path != null
    ? `<a class="nav-label" href="${hrefFor(item.path)}" data-path="${escapeHtml(item.path)}">${escapeHtml(item.title)}</a>`
    : `<button type="button" class="nav-label">${escapeHtml(item.title)}</button>`;

  if (!item.children || item.children.length === 0) {
    return `<li class="nav-item"${pathAttr}>${label}</li>`;
  }

  const dropdown = item.children
    .map((child) => `<a href="${hrefFor(child.path ?? "")}" data-path="${escapeHtml(child.path ?? "")}">${escapeHtml(child.title)}</a>`)
    .join("");

  return `<li class="nav-item has-dropdown"${pathAttr}>${label}<div class="nav-dropdown">${dropdown}</div></li>`;
}

// currentPath must match a nav item's own `path` field exactly (e.g. "" for
// the pinned Home item, "games/Tesselate" for a page) -- callers are
// responsible for translating their own routing representation into that
// convention; see hybrid-router.ts's contentFileFor for why that translation
// is needed there.
export function updateActiveNav(currentPath: string): void {
  const nav = document.getElementById("site-nav");
  if (!nav) return;

  nav.querySelectorAll<HTMLLIElement>(".nav-item").forEach((li) => {
    const childLinks = Array.from(li.querySelectorAll<HTMLAnchorElement>(".nav-dropdown a"));
    const activeChild = childLinks.find((a) => a.dataset.path === currentPath);

    li.classList.toggle("active", li.dataset.path === currentPath || !!activeChild);
    childLinks.forEach((a) => a.classList.toggle("active", a === activeChild));
  });
}

// getCurrentPath is a thunk, not a plain string: this fetch resolves
// asynchronously, racing against whatever route-handling logic the caller
// also has in flight. Reading the current path lazily (at the moment nav
// HTML actually exists to highlight) instead of a value snapshotted before
// routing has even run avoids highlighting a stale/wrong item once both
// have settled.
export async function loadNav(hrefFor: (path: string) => string, getCurrentPath: () => string): Promise<void> {
  const nav = document.getElementById("site-nav");
  if (!nav) return;

  try {
    const res = await fetch("/content/manifest.json");
    if (!res.ok) return;
    const manifest = (await res.json()) as Manifest;
    nav.innerHTML = `<ul class="site-nav-list">${manifest.nav.map((item) => renderNavItem(item, hrefFor)).join("")}</ul>`;
    updateActiveNav(getCurrentPath());
  } catch {
    // No manifest yet (or offline); leave nav as-is.
  }
}
