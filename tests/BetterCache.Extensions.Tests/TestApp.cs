using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BetterCache.Tests;

/// <summary>An in-memory app with BetterCache and its own temporary web root.</summary>
internal sealed class TestApp : IAsyncDisposable
{
    private readonly WebApplication _app;

    private TestApp(WebApplication app, string webRoot)
    {
        _app = app;
        WebRoot = webRoot;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public IServiceProvider Services => _app.Services;

    public string WebRoot { get; }

    public static async Task<TestApp> StartAsync(
        Action<WebApplication>? configureApp = null,
        Action<IServiceCollection>? configureServices = null,
        Dictionary<string, string?>? configuration = null,
        Action<string>? seedWebRoot = null)
    {
        var webRoot = Directory.CreateTempSubdirectory("bettercache-tests-").FullName;

        seedWebRoot?.Invoke(webRoot);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = webRoot, WebRootPath = webRoot, EnvironmentName = Environments.Production });

        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(configuration ?? []);
        builder.Services.AddBetterCache();
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();

        app.UseBetterCache();
        configureApp?.Invoke(app);
        await app.StartAsync(TestContext.Current.CancellationToken);

        return new TestApp(app, webRoot);
    }

    public static void WriteFile(string webRoot, string relativePath, string content) => WriteFile(webRoot, relativePath, System.Text.Encoding.UTF8.GetBytes(content));

    public static void WriteFile(string webRoot, string relativePath, byte[] content)
    {
        var path = Path.Combine(webRoot, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();

        try
        {
            Directory.Delete(WebRoot, recursive: true);
        }
        catch (IOException)
        {
            // A file handle may still be closing; the OS temp cleanup will get it.
        }
    }
}
