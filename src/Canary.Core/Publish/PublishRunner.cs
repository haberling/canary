using System.Diagnostics;
using Canary.Core.Shell;

namespace Canary.Core.Publish;

// Runs a site's configured `publish` command (canary.jsonc's top-level
// "publish" string) -- an arbitrary external command, same philosophy as
// a toolchain tool: Canary doesn't know or care how a site is actually
// hosted (git-served folder, rsync, FTP, ...), it just runs whatever the
// site author wrote. See PLAN.md's "Publishing" section.
//
// Unlike ToolchainRunner, this doesn't buffer the child's output into a
// string -- a publish command's whole point is showing the user live
// progress (git's push output, an rsync transfer, ...). Redirects
// stdout/stderr and forwards each line to Console.Out/Error as it
// arrives, rather than simply inheriting the console's handles: found via
// a real test failure that inheriting fails outright (exit code 1 before
// the child even runs) in a non-interactive host with no attached
// console, so streaming redirected output is actually the more robust
// choice, not just an equivalent one.
public static class PublishRunner
{
    public static void Run(string command, string siteRoot, string outputDir)
    {
        var (fileName, arguments) = ShellCommand.Resolve(command);
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = siteRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Lets a publish command reference the real build output location
        // without having to hardcode/duplicate output.dir from canary.jsonc.
        psi.Environment["CANARY_OUTPUT_DIR"] = Path.GetFullPath(Path.Combine(siteRoot, outputDir));

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start publish command: {command}");
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Publish command failed (exit code {process.ExitCode}): {command}");
        }
    }
}
