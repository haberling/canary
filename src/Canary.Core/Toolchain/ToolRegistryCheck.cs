using Canary.Core.Config;

namespace Canary.Core.Toolchain;

// Once-per-run (canary build/serve, not once per page or tool
// invocation -- see Program.cs's call sites) check over every
// source-bearing "tools" registry entry. Two independent concerns
// bundled into one pass because they need the exact same walk over the
// same entries: a missing compiled binary is a hard failure (nothing to
// run -- ToolchainRunner would otherwise fail with a generic cmd.exe
// "not recognized" exit-code error, since ShellCommand.Resolve always
// launches through cmd.exe/sh, which always exist, so Process.Start
// itself never fails for a missing tool binary); a stale one (source
// edited since the last `canary tools build`) is advisory-only, per the
// 0.2.0 plan's "persistent toolchain-tool workers" section: Canary
// always runs whatever binary is currently on disk, it just tells the
// author it looks stale.
//
// A plain-string registry entry (Source null) is entirely untouched by
// this check -- it never opted into precompilation, so its "command"
// might be an arbitrary shell command line, not a file path, and
// checking File.Exists against it would be meaningless.
public static class ToolRegistryCheck
{
    public static void Run(IReadOnlyDictionary<string, ToolEntry> tools, string siteRoot)
    {
        foreach (var (name, entry) in tools)
        {
            if (entry.Source is null) continue;

            var commandPath = Path.Combine(siteRoot, entry.Command);
            if (!File.Exists(commandPath))
            {
                throw new InvalidOperationException(
                    $"Tool '{name}' has no compiled binary at '{entry.Command}'. Run `canary tools build {name}` first.");
            }

            var sourcePath = Path.Combine(siteRoot, entry.Source);
            if (File.Exists(sourcePath) && File.GetLastWriteTimeUtc(sourcePath) > File.GetLastWriteTimeUtc(commandPath))
            {
                Console.Error.WriteLine(
                    $"Warning: tool '{name}' source ({entry.Source}) is newer than its compiled binary ({entry.Command}) -- run `canary tools build {name}` to rebuild it. Continuing with the existing binary.");
            }
        }
    }
}
