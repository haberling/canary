using Canary.Core.Build;
using Canary.Core.Config;
using Canary.Core.Init;
using Canary.Core.Publish;
using Canary.Core.Serve;
using Canary.Core.Widgets;

namespace Canary;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0];
        var configPath = GetOption(args, "--config") ?? "canary.json";

        switch (command)
        {
            case "init":
                return RunInit(args);
            case "build":
                return RunBuild(configPath);
            case "serve":
                return RunServe(configPath, GetOption(args, "--port"));
            case "publish":
                return RunPublish(configPath);
            case "widgets":
                return RunWidgetsList(configPath);
            case "widget":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Usage: canary widget <name> [--config <path>]");
                    return 1;
                }
                return RunWidgetShow(configPath, args[1]);
            default:
                Console.Error.WriteLine($"Unknown command: {command}");
                PrintUsage();
                return 1;
        }
    }

    // Positional target dir (default ".") plus --config/--force, both parsed
    // independently of the shared `configPath` computed in Main -- --config
    // means something different here (the *source* to scaffold values from)
    // than it does for build/serve/widgets/widget (the site's own config).
    static int RunInit(string[] args)
    {
        var targetDir = args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal) ? args[1] : ".";
        var configSourcePath = GetOption(args, "--config");
        var force = HasFlag(args, "--force");

        // Checked up front, before any interactive prompting -- no point
        // asking eight questions just to refuse at the end.
        var earlyRefusal = SiteInitializer.CheckAlreadyInitialized(targetDir, force);
        if (earlyRefusal != null)
        {
            Console.Error.WriteLine(earlyRefusal.RefusalMessage);
            return 1;
        }

        InitOptions options;
        if (configSourcePath != null)
        {
            CanaryConfig source;
            try
            {
                source = ConfigLoader.Load(configSourcePath);
            }
            catch (CanaryConfigException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }

            // Core values only -- theme paths and the tools registry are
            // never sourced from --config, see PLAN.md's "Scaffolding"
            // section for why. Serve port joins that list too: a fresh
            // random port every init run, not copied from the source,
            // specifically so two sibling projects (e.g. scaffolded from
            // the same template close together) don't default to the same
            // port and collide the moment both are served at once.
            options = new InitOptions(
                source.Site.Name!,
                source.Site.BaseUrl!,
                source.RenderMode,
                source.Content.Root!,
                source.Output.Dir,
                source.Nav.Depth,
                source.Widgets.CopyDefaultsOnInit,
                source.Widgets.PreferBuiltIn,
                GenerateRandomServePort());
        }
        else
        {
            options = PromptForInitOptions();
        }

        var templatesDir = ResolveRepoSubdir("templates", "default");
        var runtimeDistDir = ResolveRepoSubdir("runtime", "dist");
        var result = SiteInitializer.Initialize(options, targetDir, templatesDir, runtimeDistDir, force);

        if (result.Refused)
        {
            Console.Error.WriteLine(result.RefusalMessage);
            return 1;
        }

        Console.WriteLine($"Initialized Canary project in {Path.GetFullPath(targetDir)}");
        foreach (var file in result.FilesWritten)
        {
            Console.WriteLine($"  wrote {file}");
        }
        foreach (var warning in result.Warnings)
        {
            Console.WriteLine($"  warning: {warning}");
        }
        Console.WriteLine($"Run `canary build --config {Path.Combine(targetDir, "canary.json")}` next.");
        return 0;
    }

    static InitOptions PromptForInitOptions()
    {
        var siteName = PromptRequired("Site name");
        var baseUrl = PromptRequired("Base URL");
        var renderMode = PromptRenderMode("Render mode (hybrid/static)", RenderMode.Hybrid);
        var contentRoot = PromptWithDefault("Content root", "content");
        var outputDir = PromptWithDefault("Output dir", "docs");
        var navDepth = PromptInt("Nav depth", 1);
        var servePort = PromptInt("Serve port", GenerateRandomServePort());
        var copyDefaultsOnInit = PromptYesNo("Copy default widgets into widgets/ on init?", true);
        var preferBuiltIn = PromptYesNo("Prefer Canary's built-in widgets over local copies?", false);

        return new InitOptions(siteName, baseUrl, renderMode, contentRoot, outputDir, navDepth, copyDefaultsOnInit, preferBuiltIn, servePort);
    }

    static string PromptRequired(string label)
    {
        while (true)
        {
            Console.Write($"{label}: ");
            var input = Console.ReadLine();

            // null means stdin hit EOF (closed pipe, no TTY, ran out of
            // piped input) -- without this check, a required field with no
            // default loops forever re-printing "(required)" against an
            // input stream that will never produce anything else.
            if (input is null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"No input available for required field '{label}' -- aborting. Pass --config <path> to scaffold non-interactively instead.");
                Environment.Exit(1);
            }

            if (!string.IsNullOrWhiteSpace(input))
            {
                return input.Trim();
            }
            Console.WriteLine("  (required)");
        }
    }

    static string PromptWithDefault(string label, string defaultValue)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
    }

    static RenderMode PromptRenderMode(string label, RenderMode defaultValue)
    {
        while (true)
        {
            Console.Write($"{label} [{defaultValue.ToString().ToLowerInvariant()}]: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }
            switch (input.Trim().ToLowerInvariant())
            {
                case "hybrid":
                    return RenderMode.Hybrid;
                case "static":
                    return RenderMode.Static;
                default:
                    Console.WriteLine("  (must be 'hybrid' or 'static')");
                    break;
            }
        }
    }

    static int PromptInt(string label, int defaultValue)
    {
        while (true)
        {
            Console.Write($"{label} [{defaultValue}]: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }
            if (int.TryParse(input.Trim(), out var value))
            {
                return value;
            }
            Console.WriteLine("  (must be a number)");
        }
    }

    static bool PromptYesNo(string label, bool defaultValue)
    {
        var defaultText = defaultValue ? "Y/n" : "y/N";
        while (true)
        {
            Console.Write($"{label} [{defaultText}]: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultValue;
            }
            switch (input.Trim().ToLowerInvariant())
            {
                case "y":
                case "yes":
                    return true;
                case "n":
                case "no":
                    return false;
                default:
                    Console.WriteLine("  (please answer y or n)");
                    break;
            }
        }
    }

    static int RunBuild(string configPath)
    {
        CanaryConfig config;
        try
        {
            config = ConfigLoader.Load(configPath);
        }
        catch (CanaryConfigException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var siteRoot = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        var runtimeDistDir = ResolveRepoSubdir("runtime", "dist");

        if (!RunOneBuild(config, siteRoot, runtimeDistDir, "Built"))
        {
            return 1;
        }
        if (runtimeDistDir == null)
        {
            Console.WriteLine("  (runtime/dist not found -- built without nav/routing JS; see PLAN.md's Client runtime packaging section)");
        }
        return 0;
    }

    // Always builds first -- publishing stale output would be worse than
    // refusing to publish at all. The publish command itself inherits the
    // console directly (see PublishRunner) so e.g. `git push` progress is
    // visible live, not buffered and dumped after the fact.
    static int RunPublish(string configPath)
    {
        CanaryConfig config;
        try
        {
            config = ConfigLoader.Load(configPath);
        }
        catch (CanaryConfigException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var siteRoot = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        var runtimeDistDir = ResolveRepoSubdir("runtime", "dist");

        if (!RunOneBuild(config, siteRoot, runtimeDistDir, "Built"))
        {
            return 1;
        }

        if (string.IsNullOrWhiteSpace(config.Publish))
        {
            Console.Error.WriteLine("No publish command configured. Add a top-level \"publish\" field to canary.json (e.g. a git add/commit/push one-liner for a git-served host) -- see PLAN.md's \"Publishing\" section.");
            return 1;
        }

        Console.WriteLine($"Publishing via: {config.Publish}");
        try
        {
            PublishRunner.Run(config.Publish, siteRoot, config.Output.Dir);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        Console.WriteLine("Published.");
        return 0;
    }

    // `canary serve` does NOT re-read canary.json on every rebuild -- the
    // site config is treated as fixed for the lifetime of a serve session.
    // If you edit canary.json, restart the server. A deliberate v1 scope
    // choice (see PLAN.md Phase 2), not an oversight: re-validating a config
    // that might now be broken mid-session, while still serving the last
    // good output, adds real complexity for a dev-only tool.
    static int RunServe(string configPath, string? portOption)
    {
        CanaryConfig config;
        try
        {
            config = ConfigLoader.Load(configPath);
        }
        catch (CanaryConfigException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var siteRoot = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        var runtimeDistDir = ResolveRepoSubdir("runtime", "dist");
        var port = portOption != null ? int.Parse(portOption) : config.Serve.Port;
        var outputRoot = Path.Combine(siteRoot, config.Output.Dir);

        RunOneBuild(config, siteRoot, runtimeDistDir, "[build]");

        using var watcher = new SiteWatcher(siteRoot, changedPaths => RunOneBuild(config, siteRoot, runtimeDistDir, "[rebuild]", changedPaths));
        watcher.Start();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        using var server = new StaticFileServer(outputRoot, port);
        Console.WriteLine($"Serving {outputRoot} at http://localhost:{port}/ (Ctrl+C to stop)");
        server.RunAsync(cts.Token).GetAwaiter().GetResult();
        return 0;
    }

    // Neither widget command reads canary.json at all -- they only need to
    // know the site's directory (to find its own widgets/ folder alongside
    // the built-in ones), not anything the config actually says. So unlike
    // build/serve, a site doesn't need a valid (or even present) canary.json
    // for these to work.
    static int RunWidgetsList(string configPath)
    {
        var siteRoot = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        var runtimeDistDir = ResolveRepoSubdir("runtime", "dist");

        var templates = WidgetDiscovery.Discover(siteRoot, runtimeDistDir, "*.html");
        if (templates.Count == 0)
        {
            Console.WriteLine("No widgets found.");
            return 0;
        }

        foreach (var name in templates.Keys.OrderBy(n => n, StringComparer.Ordinal))
        {
            Console.WriteLine(name);
        }
        return 0;
    }

    static int RunWidgetShow(string configPath, string name)
    {
        var siteRoot = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        var runtimeDistDir = ResolveRepoSubdir("runtime", "dist");

        var templates = WidgetDiscovery.Discover(siteRoot, runtimeDistDir, "*.html");
        if (!templates.TryGetValue(name.ToLowerInvariant(), out var templatePath))
        {
            Console.Error.WriteLine($"No widget named '{name}' found.");
            return 1;
        }

        var example = WidgetClipboardExample.Extract(templatePath);
        if (example == null)
        {
            Console.Error.WriteLine($"Widget '{name}' has no clipboard example.");
            return 1;
        }

        Console.WriteLine(example);
        return 0;
    }

    static bool RunOneBuild(CanaryConfig config, string siteRoot, string? runtimeDistDir, string label, IReadOnlySet<string>? changedPaths = null)
    {
        try
        {
            var summary = new SiteBuilder().Build(config, siteRoot, runtimeDistDir, changedPaths);
            Console.WriteLine($"{label} {summary.TotalRoutes} route(s) -> {summary.OutputRoot}");
            Console.WriteLine($"  written   = {summary.PagesWritten}");
            Console.WriteLine($"  unchanged = {summary.PagesUnchanged}");
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            Console.Error.WriteLine($"{label} failed: {ex.Message}");
            return false;
        }
    }

    // Dev-convenience only: true embedding of the compiled JS runtime (and,
    // for `canary init`, the templates/default/ scaffold) into the Canary
    // package itself is a known, tracked gap (see PLAN.md), not solved
    // here. This just finds a subdirectory relative to Canary's own repo
    // layout when run via `dotnet run`/from a local build, so both are
    // testable end-to-end during development.
    static string? ResolveRepoSubdir(params string[] segments)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var parts = new string[segments.Length + 1];
            parts[0] = dir.FullName;
            Array.Copy(segments, 0, parts, 1, segments.Length);
            var candidate = Path.Combine(parts);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    static string? GetOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    static bool HasFlag(string[] args, string name) => Array.IndexOf(args, name) >= 0;

    // Range picked to avoid the well-known local-dev defaults every other
    // tool on a machine is already fighting over (3000/3001 Node/React,
    // 4200 Angular, 5000/5001 Flask/Kestrel, 5173 Vite, 8000 Django,
    // 8080/8081, 8888 Jupyter, 9000 PHP-FPM/SonarQube). A fresh value per
    // `canary init` run, not a fixed constant, so two sibling projects
    // don't default to the same port and collide the moment both are
    // served at once.
    static int GenerateRandomServePort() => Random.Shared.Next(6500, 7000);

    static void PrintUsage()
    {
        Console.WriteLine("Usage: canary <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  init [path] [--config <path>] [--force]  Scaffold a new site into path (default .); --config pulls values from an existing canary.json instead of prompting; --force overwrites an existing project");
        Console.WriteLine("  build [--config <path>]                Build the site once");
        Console.WriteLine("  serve [--config <path>] [--port <n>]   Build, then serve output.dir locally, rebuilding on change (default port: canary.json's serve.port, normally 6913)");
        Console.WriteLine("  publish [--config <path>]              Build, then run canary.json's \"publish\" command (e.g. git add/commit/push for a git-served host)");
        Console.WriteLine("  widgets [--config <path>]              List discovered widgets (built-in + site-authored)");
        Console.WriteLine("  widget <name> [--config <path>]        Print that widget's clipboard usage example");
    }
}
