using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Canary.Core.Toolchain;

// Precompiles a "tools" registry entry's Source (.cs file) into its
// Command path via `dotnet publish`, targeting Native AOT -- the only
// publish mode that actually cuts the per-invocation process-start/JIT
// overhead this feature exists to avoid (a framework-dependent or
// self-contained-but-JIT'd publish still pays full CLR startup cost on
// every launch, same as `dotnet run` does today -- see the 0.2.0 plan's
// "persistent toolchain-tool workers" section).
//
// Invoked directly as `dotnet publish ...` via ProcessStartInfo.ArgumentList
// rather than through Shell.ShellCommand -- that helper exists to handle
// cmd.exe's quoting quirks for an arbitrary author-supplied command
// string; here the command is entirely Canary's own construction, so
// there's nothing arbitrary to quote around.
//
// Streams the child's output live rather than buffering it, same
// reasoning as Publish.PublishRunner -- `dotnet publish` output (restore
// progress, "Generating native code", any compiler errors) is exactly
// what an author watching `canary tools build` run wants to see as it
// happens, not dumped after the fact.
public static class ToolsBuildRunner
{
    // source/command are both relative to siteRoot, same convention as
    // every other path in a "tools" registry entry (ToolRegistryCheck,
    // ToolchainRunner's WorkingDirectory).
    public static void Build(string source, string command, string siteRoot)
    {
        var sourcePath = Path.Combine(siteRoot, source);
        var commandPath = Path.Combine(siteRoot, command);
        var outputDir = Path.GetDirectoryName(commandPath);
        if (string.IsNullOrEmpty(outputDir))
        {
            outputDir = siteRoot;
        }
        var assemblyName = Path.GetFileNameWithoutExtension(commandPath);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = siteRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("publish");
        psi.ArgumentList.Add(sourcePath);
        psi.ArgumentList.Add("-r");
        psi.ArgumentList.Add(RuntimeInformation.RuntimeIdentifier);
        psi.ArgumentList.Add("-p:PublishAot=true");
        psi.ArgumentList.Add("-p:DebugType=none");
        // Decouples the published exe's name from source's own basename
        // (dotnet publish otherwise always names output after the source
        // file, regardless of -o) -- confirmed empirically before this
        // landed. Lets a "tools" registry author name `command` anything
        // they want, independent of the .cs filename.
        psi.ArgumentList.Add($"-p:AssemblyName={assemblyName}");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outputDir);

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start dotnet publish for tool source: {source}");
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet publish failed (exit code {process.ExitCode}) for tool source: {source}");
        }
    }
}
