using System.Net;
using System.Text;

namespace Canary.Core.Serve;

// Minimal zero-dependency static file server (System.Net.HttpListener, no
// external packages -- matches the "no framework, hand-built" ethos) for
// `canary serve`'s local dev server. Knows nothing about CanaryConfig,
// SiteBuilder, or file watching -- just serves whatever's in a root
// directory, same separation of concerns as the rest of Canary.Core.
//
// A request for a directory path serves that directory's index.html if
// present, matching how real static hosts (GitHub Pages included) resolve
// directory URLs -- see PLAN.md's Render modes section for why that
// convention matters (content/<dir>/index.md -> <dir>/index.html).
public sealed class StaticFileServer : IDisposable
{
    private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".md"] = "text/markdown; charset=utf-8",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
    };

    private readonly HttpListener _listener = new();
    private readonly string _root;

    public StaticFileServer(string root, int port)
    {
        _root = Path.GetFullPath(root);
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    // Note: HttpListener.GetContextAsync() has no built-in cancellation
    // support. cancellationToken stops this loop from picking up further
    // requests, but a request already in flight when cancellation fires is
    // simply abandoned, not aborted -- an acceptable simplification for a
    // dev-only server whose process is about to exit anyway (Ctrl+C).
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var getContextTask = _listener.GetContextAsync();
                var cancelTask = Task.Delay(Timeout.Infinite, cancellationToken);
                var completed = await Task.WhenAny(getContextTask, cancelTask);
                if (completed != getContextTask) break;

                _ = HandleAsync(await getContextTask); // fire-and-forget per request
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            var urlPath = Uri.UnescapeDataString(context.Request.Url?.AbsolutePath ?? "/");
            var filePath = ResolveFile(urlPath);

            if (filePath == null)
            {
                context.Response.StatusCode = 404;
                var body = Encoding.UTF8.GetBytes($"Not found: {urlPath}");
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
                return;
            }

            var bytes = await File.ReadAllBytesAsync(filePath);
            context.Response.ContentType = ContentTypes.GetValueOrDefault(Path.GetExtension(filePath).ToLowerInvariant(), "application/octet-stream");
            // A local dev server must never let the browser cache anything --
            // no headers here at all left caching entirely up to browser
            // heuristics, and a plain navigation (not a hard refresh) can
            // silently reuse a stale stylesheet/script from before a build
            // just ran, making a real fix look like it didn't take effect.
            context.Response.Headers.Add("Cache-Control", "no-store");
            context.Response.StatusCode = 200;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch
        {
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
                // Response already closed/aborted -- nothing more to do.
            }
        }
    }

    private string? ResolveFile(string urlPath)
    {
        var relative = urlPath.TrimStart('/');
        var full = Path.GetFullPath(Path.Combine(_root, relative));

        // Guard against path escape (e.g. a request for "/../../secrets").
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase)) return null;

        if (Directory.Exists(full))
        {
            full = Path.Combine(full, "index.html");
        }

        return File.Exists(full) ? full : null;
    }

    public void Dispose() => ((IDisposable)_listener).Dispose();
}
