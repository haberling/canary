// Entry point for renderMode "static". No client router, no content-swap
// logic at all -- every internal link is a real <a href="/games/tesselate/">,
// every navigation is a full page load. The only JS this mode ships is nav
// population/highlighting (page-enhancement, same as every mode; see
// PLAN.md's Render modes section on why "how much JS runs" isn't what
// distinguishes the modes). Widgets need no JS file at all -- they're
// entirely self-contained via inline onclick/onload attributes.

import { loadNav } from "./nav.js";

loadNav();
