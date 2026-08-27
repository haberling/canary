using System.Diagnostics;
using System.Text;
using Canary.Core.Shell;

namespace Canary.Core.Toolchain;

// A tool's process context -- grouped into one record rather than three
// bare string parameters (SiteRoot, RoutePath, ManifestPath are all
// same-typed) to avoid a transposition footgun in Run/Execute's parameter
// list as this grows.
//
// RoutePath uses the bare nav-tree convention ("" for the site root, e.g.
// "games/tesselate" for a nested page -- see Canary.Core.Manifest.
// ManifestBuilder's NavItem.Path and PLAN.md's "Content toolchain" section),
// not ContentScanner's URL-style RoutePath ("/" for root) -- it's what
// directly matches manifest.json's own "path" fields with zero translation,
// so a tool comparing its own route against the manifest tree doesn't need
// to know about the other convention at all.
public readonly record struct ToolchainContext(string SiteRoot, string RoutePath, string ManifestPath);

// Executes a page's applicable tools (see ToolchainOverrideFile) in declared
// order, chained: one tool's stdout becomes the next one's stdin. A tool is
// an arbitrary external command -- no in-process API, no language
// requirement -- run via the platform shell so a bare script path, a
// multi-word command line, etc. all work the same way a git hook or an npm
// script would. See PLAN.md's "Content toolchain" section.
//
// A tool command exiting non-zero fails the whole build outright (thrown as
// InvalidOperationException, same as every other build-time config error in
// this codebase) -- no silently-broken output.
public static class ToolchainRunner
{
    // onToolStarted, when given, fires right before each tool's process
    // starts -- index into toolNames plus its name, so a caller building a
    // display chain (see Build.IBuildProgress.RenderStageChanged) doesn't
    // have to re-derive the index via IndexOf (fragile if the same tool
    // name ever appears twice in one chain).
    public static string Run(
        IReadOnlyList<string> toolNames, IReadOnlyDictionary<string, string> registry, ToolchainContext context, string markdown,
        Action<int, string>? onToolStarted = null)
    {
        var current = markdown;
        for (var i = 0; i < toolNames.Count; i++)
        {
            onToolStarted?.Invoke(i, toolNames[i]);
            var command = ResolveCommand(toolNames[i], registry);
            current = Execute(command, context, current);
        }
        return current;
    }

    private static string ResolveCommand(string name, IReadOnlyDictionary<string, string> registry)
    {
        if (!registry.TryGetValue(name, out var command))
        {
            throw new InvalidOperationException(
                $"Unknown tool '{name}' referenced in .toolchain.json -- no matching entry in canary.jsonc's \"tools\" registry.");
        }
        return command;
    }

    // Markdown source and rendered HTML are UTF-8 everywhere else in Canary
    // (PageBuilder reads/writes both with no encoding override, which
    // defaults to UTF-8) -- tool stdio needs to match that explicitly.
    // Without it, .NET's Process falls back to the OS console codepage for
    // redirected pipes (commonly 437/1252 on Windows, never UTF-8), so any
    // non-ASCII character a tool passes through -- math symbols, smart
    // quotes, accents -- comes out corrupted, and a chain of multiple tools
    // compounds the damage on every hop. Found via a real site page (a
    // math-heavy blog post) whose formulas turned to mojibake after going
    // through a three-tool chain. A tool built the same way Canary's own
    // guide instructs (plain top-level-statements C#, reading Console.In,
    // writing Console.Out) needs to set its own Console.InputEncoding/
    // OutputEncoding to UTF-8 too -- this only fixes Canary's side of the
    // pipe, see docsite's toolchain guide.
    private static readonly UTF8Encoding ToolIoEncoding = new(encoderShouldEmitUTF8Identifier: false);

    private static string Execute(string command, ToolchainContext context, string input)
    {
        var (fileName, arguments) = ShellCommand.Resolve(command);
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = context.SiteRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = ToolIoEncoding,
            StandardOutputEncoding = ToolIoEncoding,
            StandardErrorEncoding = ToolIoEncoding,
            UseShellExecute = false,
        };
        // Lets a tool do mass, programmatic modification across the site --
        // e.g. "add a breadcrumb to every page under games/" -- by reading
        // its own position in the nav tree instead of only ever seeing its
        // own page's markdown. See PLAN.md's "Content toolchain" section.
        psi.Environment["CANARY_ROUTE_PATH"] = context.RoutePath;
        psi.Environment["CANARY_MANIFEST_PATH"] = context.ManifestPath;

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start tool command: {command}");

        // Read stdout/stderr concurrently with writing stdin -- writing all
        // of a large input before anything drains the output pipe can
        // deadlock once either OS pipe buffer fills.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            process.StandardInput.Write(input);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // Broken pipe: the process exited (e.g. a tool that ignores its
            // input entirely, or a fast-failing one) before consuming all of
            // stdin, or before consuming any of it at all. Not an error on
            // its own -- the real signal is the exit code checked below, so
            // let that produce the actual error message instead of this
            // write failure masking it. Found via a real, reproducible test
            // failure (a tool command that exits immediately without
            // reading stdin), not by inspection.
        }

        process.WaitForExit();
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Tool command failed (exit code {process.ExitCode}): {command}\n{stderr}");
        }

        return stdout;
    }
}
