// Widget for a fenced ```downloads block in markdown. Block syntax (one
// item per line):
//
//   ```downloads:Optional Title
//   Label | https://example.com/file.msi
//   copy: Label | command --to run --in a console
//   ```
//
// A `copy:` line renders as a command box with a "Copy" button instead of a
// download link. Only the first "|" after the label splits it from the
// command, so the command itself may contain "|" (e.g. a pipe-to-shell
// installer) without being cut off.
//
// See markdown.ts's fence handling for how widget modules are dispatched.

interface DownloadLink {
  kind: "link";
  label: string;
  url: string;
}

interface CopyCommand {
  kind: "copy";
  label: string;
  command: string;
}

type DownloadItem = DownloadLink | CopyCommand;

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

function jsStringEscape(s: string): string {
  return s
    .replace(/\\/g, "\\\\")
    .replace(/'/g, "\\'")
    .replace(/\r?\n/g, "\\n");
}

function splitFirst(line: string, sep: string): [string, string] {
  const i = line.indexOf(sep);
  if (i === -1) return [line.trim(), ""];
  return [line.slice(0, i).trim(), line.slice(i + sep.length).trim()];
}

function parseItems(body: string): DownloadItem[] {
  return body
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line): DownloadItem | null => {
      const copyMatch = line.match(/^copy:\s*(.*)$/i);
      if (copyMatch) {
        const [label, command] = splitFirst(copyMatch[1], "|");
        return label && command ? { kind: "copy", label, command } : null;
      }
      const [label, url] = splitFirst(line, "|");
      return label && url ? { kind: "link", label, url } : null;
    })
    .filter((item): item is DownloadItem => item !== null);
}

function renderLink(link: DownloadLink): string {
  return `<li><a class="download-button" href="${escapeHtml(link.url)}">${escapeHtml(link.label)}</a></li>`;
}

function renderCopy(copy: CopyCommand): string {
  const onclick =
    `const b=this,t=b.textContent;` +
    `navigator.clipboard.writeText('${jsStringEscape(copy.command)}');` +
    `b.textContent='Copied!';` +
    `setTimeout(()=>{b.textContent=t;},1500);`;
  return `<li class="copy-command">
    <span class="copy-command-label">${escapeHtml(copy.label)}</span>
    <div class="copy-command-row">
      <code class="copy-command-text">${escapeHtml(copy.command)}</code>
      <button type="button" class="copy-command-button" onclick="${escapeAttr(onclick)}">Copy</button>
    </div>
  </li>`;
}

export function render(title: string, body: string): string {
  const items = parseItems(body);
  const rendered = items.map((item) => (item.kind === "link" ? renderLink(item) : renderCopy(item))).join("");
  return `<div class="downloads-widget"><h3>${escapeHtml(title || "Downloads")}</h3><ul>${rendered}</ul></div>`;
}
