using Canary.Core.Config;

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
            default:
                Console.Error.WriteLine($"Unknown command: {command}");
                PrintUsage();
                return 1;
        }
    }

    // Phase 0: proves the config loader end-to-end. The real build pipeline
    // (manifest generation, prerendering, incremental writes) is Phase 1.
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

        Console.WriteLine($"Loaded config: {configPath}");
        Console.WriteLine($"  site.name     = {config.Site.Name}");
        Console.WriteLine($"  site.baseUrl  = {config.Site.BaseUrl}");
        Console.WriteLine($"  content.root  = {config.Content.Root}");
        Console.WriteLine($"  output.dir    = {config.Output.Dir}");
        Console.WriteLine($"  renderMode    = {config.RenderMode}");
        Console.WriteLine();
        Console.WriteLine("(build pipeline not implemented yet — this is Phase 0 scaffolding)");
        return 0;
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
        Console.WriteLine("  build [--config <path>]   Load and validate a site config (default: ./config.json)");
    }
}
