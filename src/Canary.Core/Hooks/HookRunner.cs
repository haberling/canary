using System.Diagnostics;
using System.Text;

namespace Canary.Core.Hooks;

// Executes a page's applicable hooks (see HooksOverrideFile) in declared
// order, chained: one hook's stdout becomes the next one's stdin. A hook is
// an arbitrary external command -- no in-process API, no language
// requirement -- run via the platform shell so a bare script path, a
// multi-word command line, etc. all work the same way a git hook or an npm
// script would. See PLAN.md's "Content hooks" section.
//
// A hook command exiting non-zero fails the whole build outright (thrown as
// InvalidOperationException, same as every other build-time config error in
// this codebase) -- no silently-broken output.
public static class HookRunner
{
    public static string Run(IReadOnlyList<string> hookNames, IReadOnlyDictionary<string, string> registry, string siteRoot, string markdown)
    {
        var current = markdown;
        foreach (var name in hookNames)
        {
            var command = ResolveCommand(name, registry);
            current = Execute(command, siteRoot, current);
        }
        return current;
    }

    // Cheap-to-compute contribution to a page's incremental-build checksum,
    // WITHOUT actually running any hook -- checksum-gating exists precisely
    // so an unchanged page can skip re-running expensive work, hooks
    // included. Covers a hook's own registered command string (so editing
    // config.json's "hooks" entry invalidates) and, when that command looks
    // like a path to a real file under the site root, that file's content
    // too (so editing the referenced script invalidates). A command that
    // isn't a bare file path (e.g. one with arguments, or a shell builtin)
    // still invalidates on the command-string/.hooks.json level, just not
    // on the referenced tool's own internal changes -- Canary has no
    // reliable way to know which file(s) an arbitrary command line touches,
    // and isn't going to guess.
    public static string ChecksumSeed(IReadOnlyList<string> hookNames, IReadOnlyDictionary<string, string> registry, string siteRoot)
    {
        var sb = new StringBuilder();
        foreach (var name in hookNames)
        {
            var command = ResolveCommand(name, registry);
            sb.Append(name).Append('\0').Append(command).Append('\0');

            var candidatePath = Path.Combine(siteRoot, command);
            if (File.Exists(candidatePath))
            {
                sb.Append(File.ReadAllText(candidatePath));
            }
            sb.Append('\0');
        }
        return sb.ToString();
    }

    private static string ResolveCommand(string name, IReadOnlyDictionary<string, string> registry)
    {
        if (!registry.TryGetValue(name, out var command))
        {
            throw new InvalidOperationException(
                $"Unknown hook '{name}' referenced in .hooks.json -- no matching entry in config.json's \"hooks\" registry.");
        }
        return command;
    }

    private static string Execute(string command, string siteRoot, string input)
    {
        var (fileName, arguments) = ShellInvocation(command);
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = siteRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start hook command: {command}");

        // Read stdout/stderr concurrently with writing stdin -- writing all
        // of a large input before anything drains the output pipe can
        // deadlock once either OS pipe buffer fills.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        process.StandardInput.Write(input);
        process.StandardInput.Close();

        process.WaitForExit();
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Hook command failed (exit code {process.ExitCode}): {command}\n{stderr}");
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
