// Starter toolchain tool -- does nothing to the markdown itself, on
// purpose: reads a page's markdown from stdin line by line and writes it
// back to stdout unchanged. Registered as "example" in canary.json's
// "tools" registry, but not applied anywhere yet -- add "example" to a
// content directory's own .toolchain.json to actually run it there. Copy
// this file, rename it, and change what it does to write your own tool;
// any language works as long as it reads stdin and writes stdout, this
// just happens to be C# via `dotnet run`, no .csproj needed. See PLAN.md's
// "Content toolchain" section for the full design (stdin/stdout contract,
// execution order).
//
// Every tool also gets two environment variables, loaded below -- routePath
// is the page's own route ("" for the site root, "characters/frederic" for
// a nested page), manifestPath is the absolute path to the site's
// just-written manifest.json (read it yourself, e.g. via
// System.Text.Json, to see the whole nav tree -- useful for a tool that
// modifies many pages at once, not just the one it's currently running
// against). Written to stderr here just to prove they're there; stdout
// stays untouched so this is still a genuine no-op against the markdown.
var routePath = Environment.GetEnvironmentVariable("CANARY_ROUTE_PATH");
var manifestPath = Environment.GetEnvironmentVariable("CANARY_MANIFEST_PATH");
Console.Error.WriteLine($"[example] CANARY_ROUTE_PATH={routePath} CANARY_MANIFEST_PATH={manifestPath}");

string? line;
while ((line = Console.In.ReadLine()) is not null)
{
    Console.Out.WriteLine(line);
}
