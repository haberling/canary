// Populates the top nav from /content/manifest.json, and highlights
// whichever item matches the current URL. Used by both render modes
// (static/hybrid) -- pure page-enhancement JS regardless of how content
// itself gets loaded/routed, per PLAN.md's Render modes section.
//
// Real root-relative hrefs ("/<path>/") in every mode now -- hybrid's
// router (router.ts) intercepts clicks on these the same as any other
// internal link via pushState, no hash indirection needed. The current
// route is always just window.location.pathname: true in static mode
// (nothing else exists) and in hybrid mode too, since router.ts keeps
// location in sync via pushState before any of this ever reads it.
//
// Root-relative fetch ("/content/manifest.json", not "content/manifest.json")
// is required for hybrid/static: those prerender to real nested paths, so a
// page-relative fetch from e.g. /games/Tesselate/ would resolve one level
// too deep. Same fix as Canary.Core.Markdown.MarkdownRenderer.ResolveUrl on
// the build-time (C#) side, see PLAN.md.
function escapeHtml(s) {
    return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}
function hrefFor(path) {
    return path === "" ? "/" : `/${path}/`;
}
function currentPath() {
    return window.location.pathname.replace(/^\/+|\/+$/g, "");
}
// A dropdown's children can themselves have children (nav.depth > 1, see
// Canary.Core.Config.NavConfig) -- rendered as a nested flyout submenu, not
// flattened away. A child with no children of its own stays a plain <a>,
// unchanged from before this existed, so a depth-1 site's markup/CSS is
// identical to what it always was.
function renderNavChild(item) {
    const hasChildren = !!item.children && item.children.length > 0;
    if (!hasChildren) {
        return item.path != null
            ? `<a href="${hrefFor(item.path)}" data-path="${escapeHtml(item.path)}">${escapeHtml(item.title)}</a>`
            : `<span>${escapeHtml(item.title)}</span>`;
    }
    const pathAttr = item.path != null ? ` data-path="${escapeHtml(item.path)}"` : "";
    // The chevron is a real element, not a ::after pseudo-element -- a text
    // node and an adjacent ::after both count as ONE merged anonymous flex
    // item in a flex container (there's no element boundary between them),
    // so `gap` has nothing to actually separate them with. Found by looking
    // at the rendered result directly, not by reasoning about the spec: the
    // ::after version rendered with the chevron flush against the text
    // despite a `gap` rule that should have worked. A genuine sibling <span>
    // is its own flex item, so `gap` applies for real.
    const titleSpan = `<span>${escapeHtml(item.title)}</span>`;
    const chevron = `<span class="nav-dropdown-chevron" aria-hidden="true">&#10095;</span>`;
    const label = item.path != null
        ? `<a class="nav-dropdown-label" href="${hrefFor(item.path)}" data-path="${escapeHtml(item.path)}">${titleSpan}${chevron}</a>`
        : `<span class="nav-dropdown-label">${titleSpan}${chevron}</span>`;
    const nested = item.children.map((child) => renderNavChild(child)).join("");
    return `<div class="nav-subitem has-subdropdown"${pathAttr}>${label}<div class="nav-dropdown nav-dropdown-nested">${nested}</div></div>`;
}
function renderNavItem(item) {
    const pathAttr = item.path != null ? ` data-path="${escapeHtml(item.path)}"` : "";
    const label = item.path != null
        ? `<a class="nav-label" href="${hrefFor(item.path)}" data-path="${escapeHtml(item.path)}">${escapeHtml(item.title)}</a>`
        : `<button type="button" class="nav-label">${escapeHtml(item.title)}</button>`;
    if (!item.children || item.children.length === 0) {
        return `<li class="nav-item"${pathAttr}>${label}</li>`;
    }
    const dropdown = item.children.map((child) => renderNavChild(child)).join("");
    return `<li class="nav-item has-dropdown"${pathAttr}>${label}<div class="nav-dropdown">${dropdown}</div></li>`;
}
// Walks up from the matched element (whatever depth it's actually at) to
// the nav root, marking every .nav-item/.nav-subitem ancestor active along
// the way -- not just the immediate parent. A depth-1 site only ever has
// one level to walk, so this is behaviorally identical to the old
// one-level version there; it only matters once nav.depth > 1 puts a real
// ancestor chain between the matched leaf and the top-level <li>.
export function updateActiveNav() {
    const nav = document.getElementById("site-nav");
    if (!nav)
        return;
    nav.querySelectorAll(".nav-item, .nav-subitem, [data-path]").forEach((el) => {
        el.classList.remove("active");
    });
    const path = currentPath();
    const matched = Array.from(nav.querySelectorAll("[data-path]"))
        .find((el) => el.dataset.path === path);
    if (!matched)
        return;
    let el = matched;
    while (el && el !== nav) {
        if (el.classList.contains("nav-item") || el.classList.contains("nav-subitem") || el.hasAttribute("data-path")) {
            el.classList.add("active");
        }
        el = el.parentElement;
    }
}
export async function loadNav() {
    const nav = document.getElementById("site-nav");
    if (!nav)
        return;
    try {
        const res = await fetch("/content/manifest.json");
        if (!res.ok)
            return;
        const manifest = (await res.json());
        nav.innerHTML = `<ul class="site-nav-list">${manifest.nav.map((item) => renderNavItem(item)).join("")}</ul>`;
        updateActiveNav();
    }
    catch {
        // No manifest yet (or offline); leave nav as-is.
    }
}
