using System.Text.RegularExpressions;

namespace Canary.Core.Templating;

// Templates (widget .html files, shell.html) carry HTML comments meant as
// author-facing documentation -- contract explanations, "don't write a
// literal {{tag}} in here" warnings, WidgetClipboardExample's own
// <!--clipboard --> blocks. None of that is meant to survive into a built
// site's actual HTML output, but neither TemplateWidgetRenderer nor
// SiteBuilder.LoadShellTemplate stripped comments before handing raw file
// text to the templater, so every <!-- --> span -- doc comment or not --
// was rendered verbatim into every page. Stripped here, once, right after
// a template is read from disk and before any placeholder/Mustache
// substitution runs. Unconditional, not opt-out: these are build-time
// templates, not authored page content, and nothing about the current
// design gives a template author a way to say "keep this one" (see
// plan-0-2-0.md's comment-leak bug section).
//
// WidgetClipboardExample.Extract reads these same files directly off disk,
// independent of this path -- stripping here doesn't touch it.
public static partial class TemplateComments
{
    public static string Strip(string template) => CommentPattern().Replace(template, string.Empty);

    [GeneratedRegex(@"<!--[\s\S]*?-->")]
    private static partial Regex CommentPattern();
}
