using System.Diagnostics;

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
    public static string Run(IReadOnlyList<string> toolNames, IReadOnlyDictionary<string, string> registry, ToolchainContext context, string markdown)
    {
        var current = markdown;
        foreach (var name in toolNames)
        {
            var command = ResolveCommand(name, registry);
            current = Execute(command, context, current);
        }
        return current;
    }

    private static string ResolveCommand(string name, IReadOnlyDictionary<string, string> registry)
    {
        if (!registry.TryGetValue(name, out var command))
        {
            throw new InvalidOperationException(
                $"Unknown tool '{name}' referenced in .toolchain.json -- no matching entry in config.json's \"tools\" registry.");
        }
        return command;
    }

    private static string Execute(string command, ToolchainContext context, string input)
    {
        var (fileName, arguments) = ShellInvocation(command);
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = context.SiteRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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

    // Found via a real test failure, not by inspection -- cmd.exe's own
    // command-line quoting is famously two bugs deep. Plain `/c command`
    // breaks the instant the command contains a forward slash (cmd's
    // switch parser keeps scanning past /c for more "/x"-shaped switches,
    // so "tools/breadcrumb.sh" gets misread as a second switch,
    // "/breadcrumb.sh"). Simply quoting it (`/c "command"`) doesn't fix
    // that either: cmd only preserves the quotes as part of the command
    // text under a specific set of conditions (see `cmd /?`), one of which
    // requires whitespace *inside* the quoted string -- a single bare path
    // like "tools/breadcrumb.sh" has none, so cmd falls back to stripping
    // the quotes and we're right back to the broken unquoted case.
    // Prefixing with `call ` (`/c "call tools/breadcrumb.sh"`) reliably
    // satisfies that whitespace condition regardless of what the actual
    // command looks like, and is a safe no-op prefix for both batch files
    // and ordinary executables -- verified empirically (including that
    // exit codes still propagate correctly) before landing this.
    private static (string FileName, string Arguments) ShellInvocation(string command) =>
        OperatingSystem.IsWindows()
            ? ("cmd.exe", $"/c \"call {command}\"")
            : ("/bin/sh", $"-c \"{command.Replace("\"", "\\\"")}\"");
}
