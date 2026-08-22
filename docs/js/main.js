// Entry point for renderMode "hybrid" (see PLAN.md's Render modes section).
// Every route already has a prerendered file: cold loads get it straight
// from the server, no JS required. Once this script has loaded, in-app
// navigation (via router.ts's pushState-based click interception) fetches
// the TARGET route's own prerendered HTML file, extracting just the #app
// fragment and splicing it in -- no re-render, no raw markdown ever reaches
// the client, and cold vs. warm navigation show byte-identical content
// because it's the same file either way.
import { registerRouteHandler, start } from "./router.js";
import { loadNav, updateActiveNav } from "./nav.js";
function prerenderedUrlFor(routePath) {
    return routePath === "" ? "/" : `/${routePath}/`;
}
let initialLoad = true;
registerRouteHandler(async (route) => {
    const app = document.getElementById("app");
    if (!app)
        return;
    // The very first route dispatch is always the cold-load case (start()
    // calls the handler synchronously, before any click can happen) -- #app
    // already contains this exact page's prerendered content, so skip the
    // redundant fetch-and-replace.
    if (initialLoad) {
        initialLoad = false;
        return;
    }
    updateActiveNav();
    app.innerHTML = "<p>Loading&hellip;</p>";
    const url = prerenderedUrlFor(route.path);
    try {
        const res = await fetch(url);
        if (!res.ok) {
            app.innerHTML = `
        <h1>Not found</h1>
        <p>No page at <code>${url}</code>.</p>
        <p><a href="/">&larr; back home</a></p>
      `;
            return;
        }
        const html = await res.text();
        const fragment = new DOMParser().parseFromString(html, "text/html").getElementById("app");
        app.innerHTML = fragment ? fragment.innerHTML : "<h1>Error</h1><p>Could not parse this page.</p>";
    }
    catch {
        app.innerHTML = "<h1>Error</h1><p>Could not load this page.</p>";
    }
});
loadNav();
start();
