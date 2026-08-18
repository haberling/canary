using System.Net;
using System.Net.Sockets;
using Canary.Core.Serve;

namespace Canary.Core.Tests.Serve;

public class StaticFileServerTests : IDisposable
{
    private readonly string _root;
    private readonly HttpClient _client = new();

    public StaticFileServerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "canary-static-server-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        _client.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static int FindFreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private async Task<(StaticFileServer server, CancellationTokenSource cts, int port)> StartServerAsync()
    {
        var port = FindFreePort();
        var server = new StaticFileServer(_root, port);
        var cts = new CancellationTokenSource();
        _ = server.RunAsync(cts.Token);
        // Give HttpListener a moment to actually start listening.
        await Task.Delay(100);
        return (server, cts, port);
    }

    [Fact]
    public async Task ServesAFileWithCorrectContentType()
    {
        File.WriteAllText(Path.Combine(_root, "style.css"), "body { color: red; }");
        var (server, cts, port) = await StartServerAsync();
        using (server)
        using (cts)
        {
            var res = await _client.GetAsync($"http://localhost:{port}/style.css");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.StartsWith("text/css", res.Content.Headers.ContentType?.ToString());
            Assert.Equal("body { color: red; }", await res.Content.ReadAsStringAsync());
            cts.Cancel();
        }
    }

    [Fact]
    public async Task ServedResponses_AreNeverCacheable()
    {
        // A local dev server must never let the browser cache a response --
        // otherwise a real edit + rebuild can look like it silently didn't
        // take effect, because the browser is still showing a stale
        // previous fetch of the same URL rather than re-requesting it.
        File.WriteAllText(Path.Combine(_root, "style.css"), "body { color: red; }");
        var (server, cts, port) = await StartServerAsync();
        using (server)
        using (cts)
        {
            var res = await _client.GetAsync($"http://localhost:{port}/style.css");
            Assert.True(res.Headers.CacheControl?.NoStore);
            cts.Cancel();
        }
    }

    [Fact]
    public async Task DirectoryRequest_ServesIndexHtml()
    {
        Directory.CreateDirectory(Path.Combine(_root, "games"));
        File.WriteAllText(Path.Combine(_root, "games", "index.html"), "<h1>Games</h1>");
        var (server, cts, port) = await StartServerAsync();
        using (server)
        using (cts)
        {
            var res = await _client.GetAsync($"http://localhost:{port}/games");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("<h1>Games</h1>", await res.Content.ReadAsStringAsync());
            cts.Cancel();
        }
    }

    [Fact]
    public async Task RootRequest_ServesRootIndexHtml()
    {
        File.WriteAllText(Path.Combine(_root, "index.html"), "<h1>Home</h1>");
        var (server, cts, port) = await StartServerAsync();
        using (server)
        using (cts)
        {
            var res = await _client.GetAsync($"http://localhost:{port}/");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
            Assert.Equal("<h1>Home</h1>", await res.Content.ReadAsStringAsync());
            cts.Cancel();
        }
    }

    [Fact]
    public async Task MissingFile_Returns404()
    {
        var (server, cts, port) = await StartServerAsync();
        using (server)
        using (cts)
        {
            var res = await _client.GetAsync($"http://localhost:{port}/nope.html");
            Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
            cts.Cancel();
        }
    }

    [Fact]
    public async Task PathEscapeAttempt_DoesNotServeFilesOutsideRoot()
    {
        var outsideDir = Path.Combine(Path.GetTempPath(), "canary-static-server-outside-" + Guid.NewGuid());
        Directory.CreateDirectory(outsideDir);
        var secretPath = Path.Combine(outsideDir, "secret.txt");
        File.WriteAllText(secretPath, "should not be servable");

        try
        {
            var (server, cts, port) = await StartServerAsync();
            using (server)
            using (cts)
            {
                var relative = Path.GetRelativePath(_root, secretPath).Replace('\\', '/');
                var res = await _client.GetAsync($"http://localhost:{port}/{relative}");
                Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
                cts.Cancel();
            }
        }
        finally
        {
            Directory.Delete(outsideDir, recursive: true);
        }
    }
}
