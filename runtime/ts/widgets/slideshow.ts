// Widget for a fenced ```slideshow block in markdown. Block syntax (one
// slide per line):
//
//   ```slideshow:Optional Title
//   img/shot1.png | Optional caption
//   img/shot2.png
//   ```
//
// Renders a prev/next + dot-indicator carousel, focusable and steppable with
// the Left/Right arrow keys, that autoplays every 10s with a play/pause
// toggle in the header (opposite the title). Since the rendered HTML is
// injected via innerHTML (see main.ts), <script> tags would be inert -- all
// interactivity is wired through inline onclick/onkeydown/onload attributes
// instead, mirroring downloads.ts's copy-button approach. Each script is
// self-contained and locates its widget root via closest('.slideshow-widget')
// (or `this`, for the root's own onkeydown), so multiple slideshows can
// coexist on one page without id collisions.
//
// Autoplay starts itself via the first slide's onload/onerror -- there's no
// DOM "mounted" event to hook otherwise. The running interval is stashed as
// an expando (r.__timer) on the widget root; every tick it checks
// r.isConnected and clears itself once the widget has been removed from the
// DOM (e.g. the SPA router replaced #app for a different page), so switching
// pages doesn't leak a timer ticking forever against a detached node.
//
// See markdown.ts's fence handling for how widget modules are dispatched.

interface Slide {
  src: string;
  caption: string;
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

// For text embedded inside an HTML attribute that itself holds inline JS
// (e.g. onclick="..."): escaping is enough on its own, since the browser's
// HTML parser decodes entities before handing the attribute value to the JS
// engine.
const escapeAttr = escapeHtml;

function splitFirst(line: string, sep: string): [string, string] {
  const i = line.indexOf(sep);
  if (i === -1) return [line.trim(), ""];
  return [line.slice(0, i).trim(), line.slice(i + sep.length).trim()];
}

function parseSlides(body: string): Slide[] {
  return body
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line): Slide | null => {
      const [src, caption] = splitFirst(line, "|");
      return src ? { src, caption } : null;
    })
    .filter((slide): slide is Slide => slide !== null);
}

function renderSlide(slide: Slide, index: number, bootstrapAttrs: string): string {
  const caption = slide.caption
    ? `<figcaption class="slideshow-caption">${escapeHtml(slide.caption)}</figcaption>`
    : "";
  return `<figure class="slideshow-slide" aria-roledescription="slide" aria-label="${index + 1}">
      <img src="${escapeAttr(slide.src)}" alt="${escapeAttr(slide.caption)}" loading="lazy"${bootstrapAttrs}>
      ${caption}
    </figure>`;
}

const AUTOPLAY_MS = 3000;

// Body assuming `r` (the widget root) is already in scope: recompute the
// target index, move the track, and sync the dots + counter to match.
function applyIndex(indexExpr: string): string {
  return (
    `const t=r.querySelector('.slideshow-track');` +
    `const n=t.children.length;` +
    `const i=${indexExpr};` +
    `r.dataset.index=i;` +
    `t.style.transform='translateX(-'+(i*100)+'%)';` +
    `r.querySelectorAll('.slideshow-dot').forEach((d,j)=>d.classList.toggle('active',j===i));` +
    `r.querySelectorAll('.slideshow-counter').forEach((c)=>{c.textContent=(i+1)+' / '+n;});`
  );
}

// Resolves `r` via `rootExpr` first -- buttons reach the root with
// closest('.slideshow-widget') since they're nested inside it; the root's
// own onkeydown just uses `this`.
function gotoScript(rootExpr: string, indexExpr: string): string {
  return `const r=${rootExpr};${applyIndex(indexExpr)}`;
}

// (Re)starts the autoplay interval against the already-in-scope `r`. Each
// tick self-clears once the widget leaves the DOM.
function startTimerScript(): string {
  return `r.__timer=setInterval(()=>{if(!r.isConnected){clearInterval(r.__timer);return;}${applyIndex("((parseInt(r.dataset.index||'0')+1)%n+n)%n")}},${AUTOPLAY_MS});`;
}

// If autoplay is currently running, restart its interval so a manual nav
// (arrow key, dot, prev/next) doesn't get immediately followed by an
// autoplay tick a moment later.
function restartTimerIfPlayingScript(): string {
  return `if(r.__playing){clearInterval(r.__timer);${startTimerScript()}}`;
}

const WIDGET_ROOT = "this.closest('.slideshow-widget')";

function stepScript(direction: 1 | -1): string {
  return (
    gotoScript(WIDGET_ROOT, `((parseInt(r.dataset.index||'0')+(${direction}))%n+n)%n`) +
    restartTimerIfPlayingScript()
  );
}

function dotScript(index: number): string {
  return gotoScript(WIDGET_ROOT, String(index)) + restartTimerIfPlayingScript();
}

// Left/Right arrow keys step the slideshow whenever focus is anywhere inside
// the widget (the root div itself, or one of its prev/next/dot buttons),
// since keydown bubbles up to the root's own handler.
function keydownScript(): string {
  const step = gotoScript("this", `((parseInt(r.dataset.index||'0')+(event.key==='ArrowLeft'?-1:1))%n+n)%n`);
  return `const k=event.key;if(k!=='ArrowLeft'&&k!=='ArrowRight')return;event.preventDefault();${step}${restartTimerIfPlayingScript()}`;
}

// Fires once when the first slide's image loads (or fails to), via
// onload/onerror -- the closest thing to a "widget mounted" hook available
// without a real script tag. Guarded against re-firing (e.g. an onerror
// after an onload) by bailing if a timer's already running.
function bootstrapScript(): string {
  return `const r=this.closest('.slideshow-widget');if(r.__timer)return;r.__playing=true;${startTimerScript()}`;
}

const PAUSE_ICON = "⏸";
const PLAY_ICON = "▶";

// Toggle button in the header: flips r.__playing, starts/stops the interval,
// and swaps its own icon/label to reflect the new state.
function toggleScript(): string {
  return (
    `const r=this.closest('.slideshow-widget');` +
    `if(r.__playing){` +
    `clearInterval(r.__timer);r.__playing=false;` +
    `this.textContent='${PLAY_ICON}';this.setAttribute('aria-label','Play slideshow');` +
    `}else{` +
    `r.__playing=true;${startTimerScript()}` +
    `this.textContent='${PAUSE_ICON}';this.setAttribute('aria-label','Pause slideshow');` +
    `}`
  );
}

export function render(title: string, body: string): string {
  const slides = parseSlides(body);
  if (slides.length === 0) {
    return `<div class="slideshow-widget"><p>No slides.</p></div>`;
  }

  const autoplay = slides.length > 1;
  const bootstrapAttrs = autoplay ? ` onload="${escapeAttr(bootstrapScript())}" onerror="${escapeAttr(bootstrapScript())}"` : "";
  const slidesHtml = slides
    .map((slide, i) => renderSlide(slide, i, i === 0 ? bootstrapAttrs : ""))
    .join("");

  const headingHtml = title ? `<h3>${escapeHtml(title)}</h3>` : "";
  const toggleHtml = autoplay
    ? `<button type="button" class="slideshow-toggle" aria-label="Pause slideshow" onclick="${escapeAttr(toggleScript())}">${PAUSE_ICON}</button>`
    : "";
  const header = headingHtml || toggleHtml ? `<div class="slideshow-header">${headingHtml}${toggleHtml}</div>` : "";

  let nav = "";
  let footer = "";
  if (autoplay) {
    nav = `<button type="button" class="slideshow-nav slideshow-prev" aria-label="Previous slide" onclick="${escapeAttr(stepScript(-1))}">&#10094;</button>
      <button type="button" class="slideshow-nav slideshow-next" aria-label="Next slide" onclick="${escapeAttr(stepScript(1))}">&#10095;</button>`;

    const dotsHtml = slides
      .map(
        (_, i) =>
          `<button type="button" class="slideshow-dot${i === 0 ? " active" : ""}" aria-label="Go to slide ${i + 1}" onclick="${escapeAttr(dotScript(i))}"></button>`
      )
      .join("");

    footer = `<div class="slideshow-footer">
      <div class="slideshow-dots">${dotsHtml}</div>
      <span class="slideshow-counter">1 / ${slides.length}</span>
    </div>`;
  }

  const keyboardAttrs = autoplay ? ` tabindex="0" onkeydown="${escapeAttr(keydownScript())}"` : "";

  return `<div class="slideshow-widget" data-index="0"${keyboardAttrs}>
    ${header}
    <div class="slideshow-viewport">
      <div class="slideshow-track">${slidesHtml}</div>
      ${nav}
    </div>
    ${footer}
  </div>`;
}
