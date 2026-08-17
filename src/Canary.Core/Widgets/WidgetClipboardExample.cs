namespace Canary.Core.Widgets;

// Extracts the "<!--clipboard ... -->" block from a widget's .html
// template -- a ready-to-paste usage example kept inside the widget file
// itself (built-in or site-authored) so it can never drift out of sync
// with separate documentation, and travels with the file if it's copied
// elsewhere. Used by the `canary widget <name>` CLI command. Print-only by
// design, not copied to the real OS clipboard: there's no cross-platform
// clipboard API in .NET itself, and getting one means either a
// Windows-only dependency (WinForms/WPF) or shelling out to a different
// OS-specific tool per platform (clip/pbcopy/xclip-or-xsel-or-wl-copy) --
// printing to stdout gets nearly all the value for none of that cost, and
// a user who wants their real clipboard can already pipe the output
// themselves (e.g. `canary widget slideshow | pbcopy`).
public static class WidgetClipboardExample
{
    private const string StartMarker = "<!--clipboard";
    private const string EndMarker = "-->";

    public static string? Extract(string templatePath)
    {
        var content = File.ReadAllText(templatePath);

        var start = content.IndexOf(StartMarker, StringComparison.Ordinal);
        if (start == -1) return null;

        var contentStart = start + StartMarker.Length;
        var end = content.IndexOf(EndMarker, contentStart, StringComparison.Ordinal);
        if (end == -1) return null;

        return content[contentStart..end].Trim();
    }
}
