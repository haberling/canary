using Canary.Core.Build;
using Canary.Core.Config;
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
        var configPath = GetOption(args, "--config") ?? "config.json";

        switch (command)
        {
            case "build":
                return RunBuild(configPath);
            case "serve":
                return RunServe(configPath, GetOption(args, "--port"));
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
        var runtimeDistDir = ResolveRuntimeDistDir();

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

    // `canary serve` does NOT re-read config.json on every rebuild -- the
    // site config is treated as fixed for the lifetime of a serve session.
    // If you edit config.json, restart the server. A deliberate v1 scope
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
        var runtimeDistDir = ResolveRuntimeDistDir();
        var port = portOption != null ? int.Parse(portOption) : 8080;
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

    // Neither widget command reads config.json at all -- they only need to
    // know the site's directory (to find its own widgets/ folder alongside
    // the built-in ones), not anything the config actually says. So unlike
    // build/serve, a site doesn't need a valid (or even present) config.json
    // for these to work.
    static int RunWidgetsList(string configPath)
    {
        var siteRoot = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
        var runtimeDistDir = ResolveRuntimeDistDir();

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
        var runtimeDistDir = ResolveRuntimeDistDir();

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

    // Dev-convenience only: true embedding of the compiled JS runtime into
    // the Canary package itself is a known, tracked gap (see PLAN.md), not
    // solved here. This just finds runtime/dist/ relative to Canary's own
    // repo layout when run via `dotnet run`/from a local build, so the
    // pipeline is testable end-to-end during development.
    static string? ResolveRuntimeDistDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "runtime", "dist");
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

    static void PrintUsage()
    {
        Console.WriteLine("Usage: canary <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  build [--config <path>]                Build the site once");
        Console.WriteLine("  serve [--config <path>] [--port <n>]   Build, then serve output.dir locally, rebuilding on change (default port: 8080)");
        Console.WriteLine("  widgets [--config <path>]              List discovered widgets (built-in + site-authored)");
        Console.WriteLine("  widget <name> [--config <path>]        Print that widget's clipboard usage example");
    }
}
