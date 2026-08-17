// Hand-rolled markdown -> HTML renderer covering the subset this site needs:
// headings, paragraphs, bold/italic, inline code, links/images, fenced code
// blocks, widgets, unordered/ordered lists, blockquotes, hr.

const CODE_SPAN_MARKER = String.fromCharCode(0);
const ESCAPED_BACKSLASH_MARKER = String.fromCharCode(1);

// A fenced block tagged ```<name> or ```<name>:<title> is dispatched to
// src/ts/widgets/<name>.ts (compiled to widgets/<name>.js), loaded on demand
// via dynamic import. Adding a new widget is just adding a new file there
// with a `render(title, body)` export -- this file never needs to change.
// An untagged ``` fence, or a tag with no matching widget file, renders as
// a plain code block.
interface WidgetModule {
  render(title: string, body: string): string;
}

async function loadWidget(name: string): Promise<WidgetModule | null> {
  try {
    const mod = (await import(`./widgets/${name.toLowerCase()}.js`)) as Partial<WidgetModule>;
    return typeof mod.render === "function" ? (mod as WidgetModule) : null;
  } catch {
    return null; // no widget registered for this fence tag
  }
}

function escapeHtml(s: string): string {
  return s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

function escapeAttr(s: string): string {
  return escapeHtml(s).replace(/"/g, "&quot;");
}

// Reverses escapeHtml()'s entity substitutions. Needed because renderInline
// escapes the WHOLE line before the image/link regexes extract a URL out of
// it -- without this, a URL containing "&" (e.g. a query string) would get
// escaped once by that whole-line pass and AGAIN by escapeAttr() below,
// producing "&amp;amp;". Ported fix, coordinated with the same bug found in
// Canary.Core.Markdown.MarkdownRenderer (build-time C# renderer) -- see
// PLAN.md's Phase 1 notes; fixing only one side would make build-time and
// client-time rendering diverge.
function unescapeHtmlEntities(s: string): string {
  return s.replace(/&amp;/g, "&").replace(/&lt;/g, "<").replace(/&gt;/g, ">");
}

// Deliberate departure from consoland's original behavior: content authors
// write asset paths like "content/games/images/x.png" -- which only worked
// in the old pure-SPA because every page rendered at the same document URL
// ("/"). Canary.Core.Markdown.MarkdownRenderer.ResolveUrl applies this same
// fix on the build-time (C#) side for hybrid/static; spa mode still renders
// entirely client-side, but the client always runs at "/" regardless of
// hash route (no real page nesting happens in spa mode), so this mostly
// matters here for consistency between the two renderers rather than fixing
// an active bug in spa mode specifically.
function resolveUrl(url: string): string {
  if (url.length === 0) return url;
  if (url.startsWith("/") || url.startsWith("#")) return url;
  if (url.includes("://") || url.startsWith("mailto:") || url.startsWith("tel:")) return url;
  return "/" + url;
}

function renderInline(text: string): string {
  const codeSpans: string[] = [];
  let out = escapeHtml(text);

  // Protect inline code spans before any other inline syntax is processed,
  // using a NUL-delimited token that can't appear in escaped source text.
  out = out.replace(/`([^`]+)`/g, (_m, code: string) => {
    codeSpans.push(`<code>${code}</code>`);
    return CODE_SPAN_MARKER + (codeSpans.length - 1) + CODE_SPAN_MARKER;
  });

  out = out.replace(/!\[([^\]]*)\]\(([^)\s]+)\)/g, (_m, alt: string, url: string) => {
    return `<img src="${escapeAttr(resolveUrl(unescapeHtmlEntities(url)))}" alt="${escapeAttr(alt)}">`;
  });

  out = out.replace(/\[([^\]]*)\]\(([^)\s]+)\)/g, (_m, label: string, url: string) => {
    return `<a href="${escapeAttr(resolveUrl(unescapeHtmlEntities(url)))}">${label}</a>`;
  });

  out = out.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
  out = out.replace(/__([^_]+)__/g, "<strong>$1</strong>");
  out = out.replace(/\*([^*]+)\*/g, "<em>$1</em>");
  out = out.replace(/_([^_]+)_/g, "<em>$1</em>");

  // Explicit escapes/passthroughs, applied after the escapeHtml() above has
  // already turned "<" and "\" into inert text. "\\" is pulled out first
  // (CommonMark-style) so a literal backslash before "-" isn't itself
  // consumed by the "\-" escape, e.g. "\\-" -> literal "\-".
  out = out.replace(/\\\\/g, ESCAPED_BACKSLASH_MARKER);
  out = out.replace(/&lt;br\s*\/?&gt;/gi, "<br>");
  out = out.replace(/\\-/g, "-");
  out = out.replace(new RegExp(ESCAPED_BACKSLASH_MARKER, "g"), "\\");

  const markerPattern = new RegExp(`${CODE_SPAN_MARKER}(\\d+)${CODE_SPAN_MARKER}`, "g");
  out = out.replace(markerPattern, (_m, i: string) => codeSpans[Number(i)]);

  return out;
}

export async function renderMarkdown(source: string): Promise<string> {
  const lines = source.replace(/\r\n/g, "\n").split("\n");
  const html: string[] = [];

  let paragraphBuf: string[] = [];
  let listBuf: string[] = [];
  let listType: "ul" | "ol" | null = null;
  let quoteBuf: string[] = [];

  function flushParagraph(): void {
    if (paragraphBuf.length) {
      html.push(`<p>${renderInline(paragraphBuf.join(" "))}</p>`);
      paragraphBuf = [];
    }
  }

  function flushList(): void {
    if (listBuf.length && listType) {
      const items = listBuf.map((item) => `<li>${renderInline(item)}</li>`).join("");
      html.push(`<${listType}>${items}</${listType}>`);
    }
    listBuf = [];
    listType = null;
  }

  function flushQuote(): void {
    if (quoteBuf.length) {
      html.push(`<blockquote><p>${renderInline(quoteBuf.join(" "))}</p></blockquote>`);
      quoteBuf = [];
    }
  }

  function flushAll(): void {
    flushParagraph();
    flushList();
    flushQuote();
  }

  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    const trimmed = line.trim();

    const fenceMatch = trimmed.match(/^```(.*)$/);
    if (fenceMatch) {
      flushAll();
      const info = (fenceMatch[1] ?? "").trim();
      const bodyLines: string[] = [];
      i++;
      while (i < lines.length && lines[i].trim() !== "```") {
        bodyLines.push(lines[i]);
        i++;
      }
      i++; // skip closing fence
      const body = bodyLines.join("\n");

      let rendered: string | null = null;
      if (info) {
        const [name, ...titleParts] = info.split(":");
        const widget = await loadWidget(name);
        if (widget) rendered = widget.render(titleParts.join(":"), body);
      }
      html.push(rendered ?? `<pre><code>${escapeHtml(body)}</code></pre>`);
      continue;
    }

    if (trimmed === "") {
      flushAll();
      i++;
      continue;
    }

    const headingMatch = trimmed.match(/^(#{1,6})\s+(.*)$/);
    if (headingMatch) {
      flushAll();
      const level = headingMatch[1].length;
      html.push(`<h${level}>${renderInline(headingMatch[2])}</h${level}>`);
      i++;
      continue;
    }

    if (/^(-{3,}|\*{3,}|_{3,})$/.test(trimmed)) {
      flushAll();
      html.push("<hr>");
      i++;
      continue;
    }

    const ulMatch = trimmed.match(/^[-*]\s+(.*)$/);
    if (ulMatch) {
      flushParagraph();
      flushQuote();
      if (listType && listType !== "ul") flushList();
      listType = "ul";
      listBuf.push(ulMatch[1]);
      i++;
      continue;
    }

    const olMatch = trimmed.match(/^\d+\.\s+(.*)$/);
    if (olMatch) {
      flushParagraph();
      flushQuote();
      if (listType && listType !== "ol") flushList();
      listType = "ol";
      listBuf.push(olMatch[1]);
      i++;
      continue;
    }

    const quoteMatch = trimmed.match(/^>\s?(.*)$/);
    if (quoteMatch) {
      flushParagraph();
      flushList();
      quoteBuf.push(quoteMatch[1]);
      i++;
      continue;
    }

    flushList();
    flushQuote();
    paragraphBuf.push(trimmed);
    i++;
  }

  flushAll();
  return html.join("\n");
}
