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
        var targetConfigPath = Path.Combine(targetDir, "canary.json");
        if (!File.Exists(targetConfigPath) || force)
        {
            return null;
        }

        var alreadyInitialized = TryReadInitializedFlag(targetConfigPath);
        var message = alreadyInitialized
            ? $"'{targetConfigPath}' is already an initialized Canary project. Pass --force to re-scaffold it."
            : $"A canary.json already exists at '{targetConfigPath}' (not created by `canary init`). Pass --force to overwrite it.";
        return new InitResult { Refused = true, RefusalMessage = message };
    }

    public static InitResult Initialize(InitOptions options, string targetDir, string? templatesDir, string? runtimeDistDir, bool force)
    {
        var refusal = CheckAlreadyInitialized(targetDir, force);
        if (refusal != null)
        {
            return refusal;
        }

        var targetConfigPath = Path.Combine(targetDir, "canary.json");
        var result = new InitResult();
        Directory.CreateDirectory(targetDir);

        WriteConfig(options, targetConfigPath);
        result.FilesWritten.Add(targetConfigPath);

        if (templatesDir != null)
        {
            CopyOverwrite(Path.Combine(templatesDir, "shell.html"), Path.Combine(targetDir, "shell.html"), result);
            CopyOverwrite(Path.Combine(templatesDir, "css", "framework.css"), Path.Combine(targetDir, "css", "framework.css"), result);
            CopyOverwrite(Path.Combine(templatesDir, "css", "theme.css"), Path.Combine(targetDir, "css", "theme.css"), result);
            CopyOverwrite(Path.Combine(templatesDir, "tools", "example.cs"), Path.Combine(targetDir, "tools", "example.cs"), result);
        }
        else
        {
            result.Warnings.Add("templates/default not found -- shell.html/css/tools/example.cs not scaffolded; copy them manually from a Canary dev checkout.");
        }

        WriteStarterContent(options, targetDir, result);

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
            // Registered but not applied anywhere -- add "example" to a
            // content directory's own .toolchain.json to actually run it.
            // See templates/default/tools/example.cs and PLAN.md's
            // "Content toolchain" section.
            ["tools"] = new JsonObject { ["example"] = "dotnet run tools/example.cs" },
            ["initialized"] = true,
        };

        File.WriteAllText(path, config.ToJsonString(WriteOptions));
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
