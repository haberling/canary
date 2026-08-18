// Copies runtime/widgets/*.html (Mustache templates), *.js (optional shared
// behavior), and *.css (optional shared styling) into runtime/dist/widgets/
// unchanged -- no compile step, all three are already final files by design
// (see PLAN.md's widget-controversy notes). The .html is what
// Canary.Core.Widgets.TemplateWidgetRenderer fills at build time; the .js/
// .css (when present) are copied into every site's output and referenced
// once per page if that widget is used. Run as part of `npm run build` in
// runtime/ts/, alongside tsc.
import { readdir, copyFile, mkdir } from "node:fs/promises";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const runtimeRoot = dirname(fileURLToPath(import.meta.url));
const srcDir = join(runtimeRoot, "widgets");
const destDir = join(runtimeRoot, "dist", "widgets");

await mkdir(destDir, { recursive: true });
const files = (await readdir(srcDir)).filter((f) => f.endsWith(".html") || f.endsWith(".js") || f.endsWith(".css"));
for (const file of files) {
  await copyFile(join(srcDir, file), join(destDir, file));
  console.log(`copied widgets/${file} -> dist/widgets/${file}`);
}
