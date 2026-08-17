// Minimal hash router. Content-fetching/markdown-rendering plugs in via
// registerRouteHandler (see main.ts).

export interface Route {
  path: string;
  segments: string[];
}

export type RouteHandler = (route: Route) => void;

let handler: RouteHandler | null = null;

function parseHash(): Route {
  const hash = window.location.hash.replace(/^#/, "");
  const path = hash === "" ? "/" : hash;
  const segments = path.split("/").filter(Boolean);
  return { path, segments };
}

export function registerRouteHandler(fn: RouteHandler): void {
  handler = fn;
}

function handleRouteChange(): void {
  if (!handler) return;
  handler(parseHash());
}

export function start(): void {
  window.addEventListener("hashchange", handleRouteChange);
  handleRouteChange();
}
