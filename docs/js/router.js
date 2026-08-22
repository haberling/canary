// Real-path (History API) router. Every route already has a real
// prerendered URL (see PLAN.md's Render modes section), so there's no need
// for hash-based routing here -- pushState keeps the address bar showing
// the real path at all times, avoiding the old hash-router's class of bugs
// (a bookmarked/shared URL where the hash and the served path disagree).
//
// Intercepts clicks on same-origin internal links -- both nav links and
// in-content markdown links, uniformly, since both are now just plain
// <a href="/real/path/"> elements -- calls preventDefault to stop the
// browser's native full navigation, then pushState + dispatch instead.
// Back/forward is handled via popstate. Content-fetching/rendering plugs in
// via registerRouteHandler (see hybrid-router.ts).
let handler = null;
function parseLocation() {
    const path = window.location.pathname.replace(/^\/+|\/+$/g, "");
    const segments = path.split("/").filter(Boolean);
    return { path, segments };
}
export function registerRouteHandler(fn) {
    handler = fn;
}
function dispatch() {
    if (!handler)
        return;
    handler(parseLocation());
}
// True for a plain, unmodified left-click on a same-origin <a href> that
// isn't opting itself out (new tab/window, download, external target) --
// anything else must fall through to the browser's normal handling
// untouched. A same-page anchor link (differs only in #fragment) is native
// in-page scrolling, not a route change, so that's excluded too.
function isInterceptable(event, anchor) {
    if (event.defaultPrevented || event.button !== 0)
        return false;
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey)
        return false;
    if (anchor.target && anchor.target !== "_self")
        return false;
    if (anchor.hasAttribute("download"))
        return false;
    if (anchor.origin !== window.location.origin)
        return false;
    if (anchor.pathname === window.location.pathname && anchor.hash !== "")
        return false;
    return true;
}
function handleClick(event) {
    const target = event.target;
    if (!(target instanceof Element))
        return;
    const anchor = target.closest("a");
    if (!anchor || !isInterceptable(event, anchor))
        return;
    event.preventDefault();
    if (anchor.href === window.location.href)
        return;
    window.history.pushState(null, "", anchor.href);
    dispatch();
}
export function start() {
    document.addEventListener("click", handleClick);
    window.addEventListener("popstate", dispatch);
    dispatch();
}
