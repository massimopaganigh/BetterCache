using System.IO.Compression;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace BetterCache.Tests;

public sealed class BlazorScriptTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PlainUrl_IsServedCompressedAndRevalidated()
    {
        await using var app = await TestApp.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_framework/blazor.web.js") { Headers = { { "Accept-Encoding", "gzip, br" } } };

        var response = await app.Client.SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("br", Assert.Single(response.Content.Headers.ContentEncoding));
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());
        Assert.Equal(FrameworkScript(), Decompress(await response.Content.ReadAsByteArrayAsync(Cancellation)));
    }

    [Fact]
    public async Task FingerprintedUrl_IsImmutable()
    {
        await using var app = await TestApp.StartAsync();
        var path = app.Services.GetRequiredService<BlazorWebScriptAsset>().FingerprintedPath;

        var response = await app.Client.GetAsync(path, Cancellation);

        Assert.Matches(@"^/_framework/blazor\.web\.[0-9a-z]{10}\.js$", path);
        Assert.Equal(CacheControlOptions.Immutable, response.Headers.CacheControl?.ToString());
        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.Equal(FrameworkScript(), await response.Content.ReadAsByteArrayAsync(Cancellation));
    }

    [Fact]
    public async Task MatchingETag_Returns304()
    {
        await using var app = await TestApp.StartAsync();
        using var first = new HttpRequestMessage(HttpMethod.Get, "/_framework/blazor.web.js") { Headers = { { "Accept-Encoding", "gzip" } } };
        var etag = (await app.Client.SendAsync(first, Cancellation)).Headers.ETag;
        using var second = new HttpRequestMessage(HttpMethod.Get, "/_framework/blazor.web.js") { Headers = { { "Accept-Encoding", "gzip" } } };

        second.Headers.IfNoneMatch.Add(etag!);

        var response = await app.Client.SendAsync(second, Cancellation);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task Disabled_LeavesTheRequestToTheFramework()
    {
        await using var app = await TestApp.StartAsync(configuration: new() { ["BetterCache:BlazorScript:Enabled"] = "false" });

        var response = await app.Client.GetAsync("/_framework/blazor.web.js", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); // no MapRazorComponents in this app
    }

    [Fact]
    public void Fingerprint_HasTheStaticWebAssetsShape() =>
        Assert.Matches("^[0-9a-z]{10}$", BlazorWebScriptAsset.ToFingerprint(System.Security.Cryptography.SHA256.HashData([1, 2, 3])));

    private static byte[] FrameworkScript()
    {
        var file = new ManifestEmbeddedFileProvider(typeof(RazorComponentsEndpointRouteBuilderExtensions).Assembly).GetFileInfo("_framework/blazor.web.js");
        using var stream = file.CreateReadStream();
        using var buffer = new MemoryStream();

        stream.CopyTo(buffer);

        return buffer.ToArray();
    }

    private static byte[] Decompress(byte[] brotli)
    {
        using var input = new BrotliStream(new MemoryStream(brotli), CompressionMode.Decompress);
        using var output = new MemoryStream();

        input.CopyTo(output);

        return output.ToArray();
    }
}
