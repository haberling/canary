namespace Canary.Core.Shell;

// Resolves an arbitrary command string (a canary.jsonc "tools"/"publish"
// entry) into the actual FileName/Arguments a Process should start with.
// Shared between Canary.Core.Toolchain.ToolchainRunner and
// Canary.Core.Publish.PublishRunner so the cmd.exe quoting fix below only
// ever has to be gotten right once.
public static class ShellCommand
{
    // Found via a real test failure, not by inspection -- cmd.exe's own
    // command-line quoting is famously two bugs deep, and it turns out to
    // be three. Plain `/c command` breaks the instant the command contains
    // a forward slash (cmd's switch parser keeps scanning past /c for more
    // "/x"-shaped switches, so "tools/breadcrumb.sh" gets misread as a
    // second switch, "/breadcrumb.sh"). Simply quoting it (`/c "command"`)
    // doesn't fix that either: cmd only preserves the quotes as part of
    // the command text under a specific set of conditions (see `cmd /?`),
    // one of which requires whitespace *inside* the quoted string -- a
    // single bare path like "tools/breadcrumb.sh" has none, so cmd falls
    // back to stripping the quotes and we're right back to the broken
    // unquoted case. Prefixing with `call ` (`/c "call tools/breadcrumb.sh"`)
    // satisfies that whitespace condition and is a safe no-op prefix for
    // both batch files and ordinary executables -- but only actually fixes
    // the forward-slash misparse for a .cmd/.bat target: `call` hands a
    // batch file off to cmd's own internal batch-processing subsystem,
    // which doesn't re-trip the "/" switch-scanning bug, but a non-batch
    // target (a precompiled tool's .exe, say) falls through to a plain
    // external-command dispatch that does. Confirmed empirically: `call
    // tools/bin/x.exe` (no arguments) still fails with cmd misreading
    // "tools" as the whole command; `call "tools/bin/x.exe"` -- quoting
    // just that one token -- works. Quoting the *entire* command instead
    // of just its first token breaks every multi-word command (cmd then
    // treats the whole quoted string, spaces included, as one literal,
    // nonexistent filename) -- confirmed both for a plain multi-word
    // command and for a forward-slash .exe with a trailing argument, so
    // QuoteFirstToken below only ever quotes the program/path itself,
    // never anything after its first space.
    public static (string FileName, string Arguments) Resolve(string command) =>
        OperatingSystem.IsWindows()
            ? ("cmd.exe", $"/c \"call {QuoteFirstToken(command)}\"")
            : ("/bin/sh", $"-c \"{command.Replace("\"", "\\\"")}\"");

    private static string QuoteFirstToken(string command)
    {
        var spaceIndex = command.IndexOf(' ');
        return spaceIndex < 0
            ? $"\"{command}\""
            : $"\"{command[..spaceIndex]}\"{command[spaceIndex..]}";
    }

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
