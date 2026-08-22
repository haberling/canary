namespace Canary.Core.Shell;

// Resolves an arbitrary command string (a canary.json "tools"/"publish"
// entry) into the actual FileName/Arguments a Process should start with.
// Shared between Canary.Core.Toolchain.ToolchainRunner and
// Canary.Core.Publish.PublishRunner so the cmd.exe quoting fix below only
// ever has to be gotten right once.
public static class ShellCommand
{
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
    public static (string FileName, string Arguments) Resolve(string command) =>
        OperatingSystem.IsWindows()
            ? ("cmd.exe", $"/c \"call {command}\"")
            : ("/bin/sh", $"-c \"{command.Replace("\"", "\\\"")}\"");

    // A caveat this class can't fix, only document -- found the same way
    // as everything above, by a real command that should have worked
    // failing anyway: on a Windows machine with the
    // NoDefaultCurrentDirectoryInExePath security hardening setting
    // enabled (not exotic -- a documented, sometimes IT-policy-enforced
    // mitigation), `call somescript.cmd` -- a bare filename with no path
    // separator, sitting directly in the working directory -- silently
    // fails to resolve, even though the file is right there. A path WITH
    // a separator (`./somescript.cmd`, `tools/somescript.cmd`) is
    // unaffected, because it bypasses that search algorithm entirely
    // rather than going through it. Not something Resolve() can safely
    // paper over: it can't tell "bare name meaning a local file" (needs a
    // "./" prefix) apart from "bare name meaning a PATH-resolved
    // executable" (a "./" prefix would break it, e.g. "dotnet"/"git"/
    // "powershell") without checking the filesystem, which this method
    // has no working directory to check against. The fix is on the
    // command author's side: always write a local script command with an
    // explicit path -- "tools/foo.cmd" or "./foo.cmd" -- never a bare
    // "foo.cmd". Every command Canary itself ships (built-in tools,
    // templates/default/tools/example.cs's own registration) already
    // follows this.
}
