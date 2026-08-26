using System.CommandLine;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using Canary.Core.Build;
using Canary.Core.Config;
using Canary.Core.Init;
using Canary.Core.Publish;
using Canary.Core.Serve;
using Canary.Core.Toolchain;
using Canary.Core.Widgets;

namespace Canary;

partial class Program
{
    static int Main(string[] args)
    {
        // Shared by every subcommand that operates on a site (build, serve,
        // publish, widgets, tools build) -- added to each of those directly
        // rather than Recursive on the root, so it doesn't also show up on
        // init/docs, which don't use it (see cli.md: "every command except
        // init and docs"). Distinct from init's own --from below, which
        // means something else entirely (an existing config to pull
        // scaffold values from, not this site's config).
        var configOption = new Option<string>("--config")
        {
            Description = "Path to canary.jsonc",
            DefaultValueFactory = _ => "canary.jsonc",
        };

        var rootCommand = new RootCommand("Canary -- hand-rolled static site engine");

        var targetDirArg = new Argument<string>("path") { DefaultValueFactory = _ => "." };
        var initFromOption = new Option<string?>("--from") { Description = "Pull values from an existing canary.jsonc instead of prompting" };
        var initForceOption = new Option<bool>("--force") { Description = "Overwrite an existing project" };
        var initCommand = new Command("init", "Scaffold a new site into path (default .)") { targetDirArg, initFromOption, initForceOption };
        initCommand.SetAction(parseResult => RunInit(
            parseResult.GetValue(targetDirArg)!,
            parseResult.GetValue(initFromOption),
            parseResult.GetValue(initForceOption)));
        rootCommand.Subcommands.Add(initCommand);

        var cleanOption = new Option<bool>("--clean") { Description = "Delete output.dir before rebuilding (prompts to confirm, default No)" };
        var buildCommand = new Command("build", "Build the site once") { configOption, cleanOption };
        buildCommand.SetAction(parseResult => RunBuild(
            parseResult.GetValue(configOption)!,
            parseResult.GetValue(cleanOption)));
        rootCommand.Subcommands.Add(buildCommand);

        var portOption = new Option<string?>("--port") { Description = "Port to serve on (default: canary.jsonc's serve.port, normally 6913)" };
        var serveCommand = new Command("serve", "Build, then serve output.dir locally, rebuilding on change") { configOption, portOption };
        serveCommand.SetAction(parseResult => RunServe(
            parseResult.GetValue(configOption)!,
            parseResult.GetValue(portOption)));
        rootCommand.Subcommands.Add(serveCommand);

        var publishCommand = new Command("publish", "Build, then run canary.jsonc's \"publish\" command") { configOption };
        publishCommand.SetAction(parseResult => RunPublish(parseResult.GetValue(configOption)!));
        rootCommand.Subcommands.Add(publishCommand);

        var docsForceOption = new Option<bool>("--force") { Description = "Close an already-open docs instance first" };
        var docsCommand = new Command("docs", "Open Canary's own bundled documentation in a browser") { docsForceOption };
        docsCommand.SetAction(parseResult => RunDocs(parseResult.GetValue(docsForceOption)));
        rootCommand.Subcommands.Add(docsCommand);

        var widgetNameArg = new Argument<string?>("name") { Arity = ArgumentArity.ZeroOrOne, Description = "List all widgets if omitted, else print one widget's clipboard usage example" };
        var widgetsCommand = new Command("widgets", "List discovered widgets, or print one widget's clipboard usage example") { configOption, widgetNameArg };
        widgetsCommand.SetAction(parseResult =>
        {
            var name = parseResult.GetValue(widgetNameArg);
            var config = parseResult.GetValue(configOption)!;
            return name is null ? RunWidgetsList(config) : RunWidgetShow(config, name);
        });
        rootCommand.Subcommands.Add(widgetsCommand);

        var toolNameArg = new Argument<string?>("name") { Arity = ArgumentArity.ZeroOrOne, Description = "Build only this tool; omit to build all buildable tools" };
        var toolsBuildCommand = new Command("build", "Precompile \"tools\" registry entries with a \"source\" field via dotnet publish (Native AOT)") { configOption, toolNameArg };
        toolsBuildCommand.SetAction(parseResult => RunToolsBuild(
            parseResult.GetValue(configOption)!,
            parseResult.GetValue(toolNameArg)));
        var toolsCommand = new Command("tools", "Tool registry management") { toolsBuildCommand };
        rootCommand.Subcommands.Add(toolsCommand);

        return rootCommand.Parse(args).Invoke();
    }

    static int RunInit(string targetDir, string? configSourcePath, bool force)
    {
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
        Console.WriteLine($"Run `canary build --config {Path.Combine(targetDir, "canary.jsonc")}` next.");
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

    static int RunBuild(string configPath, bool clean)
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

        try
        {
            ToolRegistryCheck.Run(config.Tools, siteRoot);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        if (clean)
        {
            var outputRoot = Path.Combine(siteRoot, config.Output.Dir);
            if (Directory.Exists(outputRoot))
            {
                // Default is No -- this is destructive and irreversible-by-
                // Canary, unlike init's prompts which default toward
                // convenience. A non-interactive invocation (Console.ReadLine()
                // returns null) falls back to this same default, so a piped/
                // CI caller never deletes anything without asking.
                var confirmed = PromptYesNo($"About to delete {outputRoot} and everything in it before rebuilding. Continue?", false);
                if (!confirmed)
                {
                    Console.WriteLine("Cancelled -- output directory left untouched.");
                    return 1;
                }
                Directory.Delete(outputRoot, recursive: true);
            }
        }

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
            Console.Error.WriteLine("No publish command configured. Add a top-level \"publish\" field to canary.jsonc (e.g. a git add/commit/push one-liner for a git-served host) -- see PLAN.md's \"Publishing\" section.");
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

    // `canary serve` does NOT re-read canary.jsonc on every rebuild -- the
    // site config is treated as fixed for the lifetime of a serve session.
    // If you edit canary.jsonc, restart the server. A deliberate v1 scope
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

        // Once per serve session, not on every debounced rebuild below --
        // an author actively editing a stale tool's source while serving
        // shouldn't see the same warning repeat on every save.
        try
        {
            ToolRegistryCheck.Run(config.Tools, siteRoot);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

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

    // One flag file per machine, not per invocation -- tracks the single
    // currently-running `canary docs` instance (if any) so a second
    // invocation can detect it instead of silently binding a second port
    // nobody asked for. Lives in ApplicationData (cross-platform via
    // Environment.SpecialFolder, not a hardcoded Windows path) rather than
    // anywhere inside a checkout, since "is docs already open" is a
    // machine-wide question, not a per-repo one.
    private sealed record DocsLockInfo(int Pid, int Port);

    // AOT-safe metadata for DocsLockInfo -- this is the exe's own private
    // bookkeeping type, not part of Canary.Core's model set, so it gets its
    // own tiny context rather than reaching into Canary.Core.Json for a
    // type nothing there otherwise needs to know about.
    [JsonSerializable(typeof(DocsLockInfo))]
    private partial class DocsLockJsonContext : JsonSerializerContext
    {
    }

    static string DocsLockFilePath()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Canary");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "docs.lock.json");
    }

    // Corrupt/unreadable is treated the same as absent -- this is Canary's
    // own bookkeeping file, never hand-edited, so there's nothing to
    // validate or report back to a user about; just don't let it block
    // `canary docs` from working.
    static DocsLockInfo? ReadDocsLock(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), DocsLockJsonContext.Default.DocsLockInfo);
        }
        catch
        {
            return null;
        }
    }

    static void WriteDocsLock(string path, int pid, int port) =>
        File.WriteAllText(path, JsonSerializer.Serialize(new DocsLockInfo(pid, port), DocsLockJsonContext.Default.DocsLockInfo));

    // Best-effort: cleanup running on the Ctrl+C shutdown path, or right
    // before overwriting a stale/just-killed lock, should never itself fail
    // the command over something as inconsequential as a delete race.
    static void DeleteDocsLock(string path)
    {
        try { File.Delete(path); } catch { }
    }

    static bool IsProcessAlive(int pid)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            // A stale lock file whose PID has since been reused by some
            // unrelated process would otherwise look identical to a real
            // still-running `canary docs` -- not airtight (nothing short of
            // inspecting the full command line would be), but a cheap check
            // against the common case.
            var name = process.ProcessName;
            return name.Contains("Canary", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("dotnet", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false; // no process with that id
        }
    }

    // Serves Canary's own bundled documentation (docsite/'s build output, the
    // repo root's docs/), not a site the caller owns -- no --config, no
    // build/watch, just a dumb static-file preview of whatever's already
    // built. Resolved the same way templates/runtime already are (a walk up
    // from the running exe's own directory), which already works for a
    // from-source checkout with zero extra copying -- see PLAN.md's
    // Documentation site section for why nothing more elaborate (embedding,
    // a build-time copy step) was built for this until real packaging exists
    // to design around.
    static int RunDocs(bool force)
    {
        var docsDir = ResolveRepoSubdir("docs");
        if (docsDir == null)
        {
            Console.Error.WriteLine("docs/ not found -- run `canary build --config docsite/canary.jsonc` from a full Canary checkout first (see PLAN.md's Documentation site section).");
            return 1;
        }

        var lockPath = DocsLockFilePath();
        var existing = ReadDocsLock(lockPath);

        if (existing != null && IsProcessAlive(existing.Pid))
        {
            if (!force)
            {
                Console.WriteLine($"docs already open at http://localhost:{existing.Port}/ (pid {existing.Pid}). Pass --force to close it and start a new one.");
                return 0;
            }

            Console.WriteLine($"--force: closing existing docs server (pid {existing.Pid})...");
            try
            {
                var oldProcess = Process.GetProcessById(existing.Pid);
                oldProcess.Kill();
                oldProcess.WaitForExit(3000);
            }
            catch
            {
                // Already gone, or couldn't be signaled -- "try to close"
                // is best-effort, same standard as OpenBrowser below; fall
                // through and start a new server regardless.
            }
        }

        // Stale (process already gone) or just force-killed -- either way,
        // don't leave old info sitting there before this run writes its own.
        DeleteDocsLock(lockPath);

        int port;
        try
        {
            port = FindUnusedPort(9000, 10000);
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        WriteDocsLock(lockPath, Environment.ProcessId, port);
        try
        {
            var url = $"http://localhost:{port}/";
            using var server = new StaticFileServer(docsDir, port);
            Console.WriteLine($"Serving Canary's own docs from {docsDir} at {url} (Ctrl+C to stop)");
            OpenBrowser(url);
            server.RunAsync(cts.Token).GetAwaiter().GetResult();
            return 0;
        }
        finally
        {
            DeleteDocsLock(lockPath);
        }
    }

    // Neither widget command reads canary.jsonc at all -- they only need to
    // know the site's directory (to find its own widgets/ folder alongside
    // the built-in ones), not anything the config actually says. So unlike
    // build/serve, a site doesn't need a valid (or even present) canary.jsonc
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

    // Precompiles every "tools" registry entry with a .cs Source (or just
    // <name>'s, if given) via dotnet publish -- see ToolsBuildRunner and
    // the 0.2.0 plan's "persistent toolchain-tool workers" section. A
    // plain-string entry, or an object entry with no Source, is silently
    // not "buildable" -- nothing to do for it, not an error.
    static int RunToolsBuild(string configPath, string? name)
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

        List<KeyValuePair<string, ToolEntry>> toBuild;
        if (name != null)
        {
            if (!config.Tools.TryGetValue(name, out var entry))
            {
                Console.Error.WriteLine($"No tool named '{name}' in canary.jsonc's \"tools\" registry.");
                return 1;
            }
            if (entry.Source is null || !entry.Source.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Tool '{name}' has no buildable \".cs\" source -- nothing to build.");
                return 1;
            }
            toBuild = [new(name, entry)];
        }
        else
        {
            toBuild = config.Tools
                .Where(kv => kv.Value.Source != null && kv.Value.Source.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (toBuild.Count == 0)
            {
                Console.WriteLine("No buildable tools found (no \"tools\" registry entry has a \".cs\" source).");
                return 0;
            }
        }

        var failed = false;
        foreach (var (toolName, entry) in toBuild)
        {
            Console.WriteLine($"Building '{toolName}' from {entry.Source} -> {entry.Command} ...");
            try
            {
                ToolsBuildRunner.Build(entry.Source!, entry.Command, siteRoot);
                Console.WriteLine("  done.");
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"  failed: {ex.Message}");
                failed = true;
            }
        }

        return failed ? 1 : 0;
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

    // Two ways this data (runtime/dist, templates/default, docs) can be
    // found, tried in order:
    //  1. Beside the exe itself -- how a real install works. `canary.csproj`
    //     stages these three directories into the publish output next to
    //     Canary.exe (see its Content items), and the MSI installs that
    //     whole publish output as-is, so a packaged install never needs
    //     anything beyond AppContext.BaseDirectory.
    //  2. Walking up from the exe looking for a repo checkout -- dev
    //     convenience only, so `dotnet run`/a local build still finds these
    //     without needing a publish step first. Never taken by a real
    //     install, since nothing above AppContext.BaseDirectory in
    //     Program Files is a Canary repo checkout.
    static string? ResolveRepoSubdir(params string[] segments)
    {
        var beside = Path.Combine([AppContext.BaseDirectory, .. segments]);
        if (Directory.Exists(beside))
        {
            return beside;
        }

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

    // Range picked to avoid the well-known local-dev defaults every other
    // tool on a machine is already fighting over (3000/3001 Node/React,
    // 4200 Angular, 5000/5001 Flask/Kestrel, 5173 Vite, 8000 Django,
    // 8080/8081, 8888 Jupyter, 9000 PHP-FPM/SonarQube). A fresh value per
    // `canary init` run, not a fixed constant, so two sibling projects
    // don't default to the same port and collide the moment both are
    // served at once.
    static int GenerateRandomServePort() => Random.Shared.Next(6500, 7000);

    // Unlike GenerateRandomServePort above (which just avoids well-known
    // *other tools'* defaults, never checked against what's actually free
    // right now), `canary docs` needs a port nothing else already holds --
    // it's a one-shot command someone might run again while a previous
    // instance, or anything else, is still bound to a port in the same
    // range. A bind-then-immediately-release probe on a plain TcpListener is
    // a real (if not perfectly race-free -- something else could grab the
    // port in the gap between this probe releasing it and StaticFileServer's
    // own HttpListener binding it) check that GenerateRandomServePort never
    // attempts at all; good enough for a local single-user dev command.
    static int FindUnusedPort(int minInclusive, int maxExclusive, int maxAttempts = 50)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            var candidate = Random.Shared.Next(minInclusive, maxExclusive);
            try
            {
                var probe = new TcpListener(IPAddress.Loopback, candidate);
                probe.Start();
                probe.Stop();
                return candidate;
            }
            catch (SocketException)
            {
                // Already in use -- try another candidate.
            }
        }

        throw new InvalidOperationException($"Could not find an unused port in [{minInclusive}, {maxExclusive}) after {maxAttempts} attempts.");
    }

    // Best-effort only: if no browser can be launched this way (no display,
    // an unrecognized OS, a sandboxed environment), the server is already up
    // and its URL already printed either way, so a failure here isn't fatal
    // to the command's actual job.
    static void OpenBrowser(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch
        {
            // Non-fatal -- see comment above.
        }
    }
}
