using System.Text.Json;
using System.Text.Json.Nodes;
using Canary.Core.Config;

namespace Canary.Core.Init;

// Scaffolds a new Canary project on disk. No Console I/O here -- prompting
// lives in the CLI (Program.cs), same split as SiteBuilder/ConfigLoader
// keeping the build pipeline testable without a real terminal.
public static class SiteInitializer
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    // Exposed separately so a CLI can check this *before* running an
    // interactive prompt sequence -- there's no point asking eight
    // questions just to refuse at the end. Initialize() also calls this
    // itself, so it's still the single source of truth and callers (like
    // the --config path, or a test) that skip the pre-check are still safe.
    public static InitResult? CheckAlreadyInitialized(string targetDir, bool force)
    {
        var targetConfigPath = Path.Combine(targetDir, "canary.jsonc");
        if (!File.Exists(targetConfigPath) || force)
        {
            return null;
        }

        var alreadyInitialized = TryReadInitializedFlag(targetConfigPath);
        var message = alreadyInitialized
            ? $"'{targetConfigPath}' is already an initialized Canary project. Pass --force to re-scaffold it."
            : $"A canary.jsonc already exists at '{targetConfigPath}' (not created by `canary init`). Pass --force to overwrite it.";
        return new InitResult { Refused = true, RefusalMessage = message };
    }

    public static InitResult Initialize(InitOptions options, string targetDir, string? templatesDir, string? runtimeDistDir, bool force)
    {
        var refusal = CheckAlreadyInitialized(targetDir, force);
        if (refusal != null)
        {
            return refusal;
        }

        var targetConfigPath = Path.Combine(targetDir, "canary.jsonc");
        var result = new InitResult();
        Directory.CreateDirectory(targetDir);

        WriteConfig(options, targetConfigPath);
        result.FilesWritten.Add(targetConfigPath);

        if (templatesDir != null)
        {
            CopyOverwrite(Path.Combine(templatesDir, "shell.html"), Path.Combine(targetDir, "shell.html"), result);
            CopyOverwrite(Path.Combine(templatesDir, "css", "framework.css"), Path.Combine(targetDir, "css", "framework.css"), result);
            CopyOverwrite(Path.Combine(templatesDir, "css", "theme.css"), Path.Combine(targetDir, "css", "theme.css"), result);
            CopyOverwrite(Path.Combine(templatesDir, "tools", "curtain.cs"), Path.Combine(targetDir, "tools", "curtain.cs"), result);
            CopyOverwrite(Path.Combine(templatesDir, "tools", "reading-time.ps1"), Path.Combine(targetDir, "tools", "reading-time.ps1"), result);
        }
        else
        {
            result.Warnings.Add("templates/default not found -- shell.html/css/tools/curtain.cs/tools/reading-time.ps1 not scaffolded; copy them manually from a Canary dev checkout.");
        }

        WriteStarterContent(options, targetDir, result);
        WriteGitignore(targetDir, options, result);

        // Starts empty -- see SiteBuilder.CopyRootCopyAssets's own doc
        // comment for what this directory is for (files that need to land
        // at output.dir's own root: GitHub Pages' CNAME, .nojekyll, a
        // robots.txt/favicon.ico override). Just needs to exist, not
        // contain anything -- Directory.CreateDirectory is already a no-op
        // if it's there (a re-scaffold via --force, say), so there's no
        // "already exists" branch to write here.
        Directory.CreateDirectory(Path.Combine(targetDir, "root-copy"));

        if (options.CopyDefaultsOnInit)
        {
            CopyBuiltInWidgets(runtimeDistDir, targetDir, result);
        }

        return result;
    }

    private static void WriteConfig(InitOptions options, string path)
    {
        var config = new JsonObject
        {
            ["site"] = new JsonObject { ["name"] = options.SiteName, ["baseUrl"] = options.BaseUrl },
            ["content"] = new JsonObject { ["root"] = options.ContentRoot },
            ["output"] = new JsonObject { ["dir"] = options.OutputDir },
            ["renderMode"] = options.RenderMode.ToString().ToLowerInvariant(),
            ["nav"] = new JsonObject { ["depth"] = options.NavDepth },
            ["serve"] = new JsonObject { ["port"] = options.ServePort },
            ["theme"] = new JsonObject
            {
                ["shell"] = "shell.html",
                ["base"] = "css/framework.css",
                ["theme"] = "css/theme.css",
            },
            ["widgets"] = new JsonObject
            {
                ["copyDefaultsOnInit"] = options.CopyDefaultsOnInit,
                ["preferBuiltIn"] = options.PreferBuiltIn,
            },
            // Registered but not applied anywhere -- add a name to a
            // content directory's own .toolchain.json to actually run it.
            // Two languages deliberately, not one -- a tool is just an
            // external command, and curtain.cs (C#, needs a .NET SDK) next
            // to reading-time.ps1 (PowerShell, ships with Windows, no
            // separate runtime) makes that visible instead of implied. See
            // templates/default/tools/, and PLAN.md's "Content toolchain"
            // section.
            ["tools"] = new JsonObject
            {
                ["reading-time"] = "powershell -NoProfile -ExecutionPolicy Bypass -File tools/reading-time.ps1",
                ["curtain"] = "dotnet run tools/curtain.cs",
            },
            ["initialized"] = true,
        };

        // JsonNode has no concept of comments -- can't express "commented
        // out" through the object graph above, so the precompiled-form
        // demo is spliced into the serialized text afterward instead of
        // built the same uniform way as every other field. Anchored on
        // just the "curtain" property's own line + the tools object's
        // closing brace (its own exact substring, unique in the file,
        // regardless of what else lives in "tools" alongside it) --
        // confirmed empirically, not guessed; ToJsonString's indented
        // writer uses Environment.NewLine (CRLF on Windows), not a bare
        // "\n" -- found the hard way when an earlier version of this
        // silently no-op'd instead of erroring.
        var nl = Environment.NewLine;
        var withCommentedPrecompileExample = config.ToJsonString(WriteOptions).Replace(
            $"    \"curtain\": \"dotnet run tools/curtain.cs\"{nl}  }},",
            $"    // Precompile this to a native binary once (needs a .NET 10+ SDK and a{nl}" +
            $"    // working Native AOT toolchain) via `canary tools build curtain`, then{nl}" +
            $"    // swap the active line below for the commented one to skip re-JITing{nl}" +
            $"    // curtain.cs on every page build:{nl}" +
            $"    // \"curtain\": {{ \"command\": \"tools/bin/curtain.exe\", \"source\": \"tools/curtain.cs\" }},{nl}" +
            $"    \"curtain\": \"dotnet run tools/curtain.cs\"{nl}" +
            $"  }},");

        File.WriteAllText(path, withCommentedPrecompileExample);
    }

    // Write-once, like content/index.md below -- once a .gitignore exists,
    // an author may have added their own entries to it, so a later
    // `canary init --force` (a re-scaffold, not a fresh one) must never
    // clobber it.
    //
    // output.dir is deliberately NOT ignored by default -- it defaults to
    // "docs" specifically to match GitHub Pages' "serve from /docs on
    // main" convention (see reference/config.md), which requires that
    // directory to actually be committed. Ignoring it unconditionally
    // would break the single most obvious deploy path a new site would
    // reach for -- the commented-out line below is for the opposite case
    // (a separate branch/repo, `canary publish`, a CI build step) where an
    // author doesn't want build output in their history. Same caveat this
    // repo's own root .gitignore already has to carve an exception for
    // (`docs/` / `!/docs/`, for docsite's own build output).
    private static void WriteGitignore(string targetDir, InitOptions options, InitResult result)
    {
        var path = Path.Combine(targetDir, ".gitignore");
        if (File.Exists(path))
        {
            return;
        }

        var content =
            $"""
            # Site build output -- left tracked by default so a GitHub Pages
            # "serve from {options.OutputDir}/ on main" deploy works out of the box.
            # Uncomment if you deploy some other way (a separate branch/repo,
            # `canary publish`, a CI build step) and don't want build output
            # cluttering this repo's history:
            # {options.OutputDir}/

            # Precompiled tool binaries (see `canary tools build`)
            tools/bin/

            # Editor / OS
            .vs/
            .vscode/
            .idea/
            .DS_Store
            Thumbs.db
            """ + Environment.NewLine;

        File.WriteAllText(path, content);
        result.FilesWritten.Add(path);
    }

    private static void WriteStarterContent(InitOptions options, string targetDir, InitResult result)
    {
        var contentDir = Path.Combine(targetDir, options.ContentRoot);
        Directory.CreateDirectory(contentDir);

        // Never overwritten, even with --force -- real content, not
        // framework scaffold like the theme/widget files above.
        var indexPath = Path.Combine(contentDir, "index.md");
        if (!File.Exists(indexPath))
        {
            File.WriteAllText(indexPath, StarterIndexMarkdown(options.SiteName));
            result.FilesWritten.Add(indexPath);
        }
    }

    private static string StarterIndexMarkdown(string siteName) =>
        $"""
        # {siteName}

        This is your new Canary site's home page. Edit `content/index.md` to replace this text, add more `.md` files alongside it, and run `canary build` (or `canary serve` while you work) to see it live.
        """ + Environment.NewLine;

    private static void CopyBuiltInWidgets(string? runtimeDistDir, string targetDir, InitResult result)
    {
        if (runtimeDistDir is null)
        {
            result.Warnings.Add("runtime/dist not found -- built-in widgets not scaffolded.");
            return;
        }

        var builtInWidgetsDir = Path.Combine(runtimeDistDir, "widgets");
        if (!Directory.Exists(builtInWidgetsDir))
        {
            result.Warnings.Add($"No built-in widgets found at '{builtInWidgetsDir}' -- widgets/ not scaffolded.");
            return;
        }

        var widgetsDir = Path.Combine(targetDir, "widgets");
        Directory.CreateDirectory(widgetsDir);

        // Always overwrites, even over a locally-customized copy -- see
        // PLAN.md's "Scaffolding" section for why.
        foreach (var sourcePath in Directory.GetFiles(builtInWidgetsDir))
        {
            var destPath = Path.Combine(widgetsDir, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, destPath, overwrite: true);
            result.FilesWritten.Add(destPath);
        }
    }

    private static void CopyOverwrite(string sourcePath, string destPath, InitResult result)
    {
        if (!File.Exists(sourcePath))
        {
            result.Warnings.Add($"Expected template file not found: {sourcePath}");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
        File.Copy(sourcePath, destPath, overwrite: true);
        result.FilesWritten.Add(destPath);
    }

    private static bool TryReadInitializedFlag(string path)
    {
        try
        {
            return ConfigLoader.Load(path).Initialized;
        }
        catch (CanaryConfigException)
        {
            return false;
        }
    }
}
