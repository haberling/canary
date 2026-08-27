namespace Canary.Core.Toolchain;

public sealed record ToolValidationResult(string ToolName, string Command, bool Passed, string Detail);

// Smoke-tests a registered tool's stdio against the exact bug class found on
// a real site: neither Canary's own pipe nor a tool's Console.In/Out was
// pinned to UTF-8, so non-ASCII markdown -- math symbols, smart quotes,
// accented letters, CJK, emoji -- came out mangled after going through a
// tool chain (see ToolchainRunner.ToolIoEncoding). That's silent: a tool
// author only notices once some page's content happens to contain the
// wrong character, which can be a long time after the tool shipped. This
// runs a probe payload through a tool via the exact same ToolchainRunner
// pipe a real build uses, then checks the probe reappears byte-for-byte.
//
// Best-effort, not a guarantee: a tool that legitimately rewrites ordinary
// paragraph text -- not just headings/directives it specifically targets --
// fails this check even with perfectly correct encoding handling. Every
// tool Canary ships (and the toolchain guide's contract) only ever
// transforms specific structural markers, leaving ordinary body text
// untouched, so that's the assumption this probe leans on.
public static class ToolchainValidator
{
    public const string ProbeMarker = "sum: ∑ W = w₁+w₂ + … + wₙ — “smart quotes”, café, 日本語, 🎉";

    private static readonly string ProbePayload =
        $"A page's ordinary paragraph text, exactly as a well-behaved tool should leave it.\n\n{ProbeMarker}\n";

    public static ToolValidationResult ValidateOne(string toolName, string command, ToolchainContext context)
    {
        string output;
        try
        {
            output = ToolchainRunner.Run([toolName], new Dictionary<string, string> { [toolName] = command }, context, ProbePayload);
        }
        catch (InvalidOperationException ex)
        {
            return new ToolValidationResult(toolName, command, false, $"tool failed to run: {ex.Message}");
        }

        return output.Contains(ProbeMarker, StringComparison.Ordinal)
            ? new ToolValidationResult(toolName, command, true, "ok")
            : new ToolValidationResult(toolName, command, false, DescribeMismatch(output));
    }

    private static string DescribeMismatch(string output)
    {
        var got = Truncate(output.Trim(), 200);
        return got.Length == 0
            ? "probe marker did not survive -- tool produced empty output."
            : $"probe marker did not survive unchanged. Tool's output was:\n    {got}";
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
