using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BetterCache.Tests;

public sealed class CompressionTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("br, gzip", "br,gzip")]
    [InlineData("gzip, br", "br,gzip")] // equal q: server preference
    [InlineData("gzip;q=1, br;q=0.5", "gzip,br")]
    [InlineData("br;q=0, gzip", "gzip")]
    [InlineData("*", "br,gzip")]
    [InlineData("gzip, *;q=0", "gzip")]
    [InlineData("identity, gzip;q=0.5", "")] // the client prefers uncompressed
    [InlineData("deflate", "")]
    [InlineData("", "")] // no header: never compress for clients that did not ask
    [InlineData("br;q=nonsense", "")] // malformed: fall back to identity
    public void Negotiator_HonorsQualityValues(string acceptEncoding, string expected)
    {
        var request = new DefaultHttpContext().Request;

        if (acceptEncoding.Length > 0)
            request.Headers.AcceptEncoding = acceptEncoding;

        var result = new AcceptEncodingNegotiator().Negotiate(request, PrecompressedFilesOptions.DefaultEncodings);

        Assert.Equal(expected, string.Join(',', result.Select(encoding => encoding.Name)));
    }

    [Fact]
    public async Task Sibling_IsServedWithEncodingHeaders()
    {
        await using var app = await StartWithFilesAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/app.js") { Headers = { { "Accept-Encoding", "gzip, br" } } };

        var response = await app.Client.SendAsync(request, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("br", Assert.Single(response.Content.Headers.ContentEncoding));
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
        Assert.Equal("BROTLI", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task Sibling_SupportsConditionalRequests()
    {
        await using var app = await StartWithFilesAsync();
        using var first = new HttpRequestMessage(HttpMethod.Get, "/app.js") { Headers = { { "Accept-Encoding", "br" } } };
        var etag = (await app.Client.SendAsync(first, Cancellation)).Headers.ETag;
        using var second = new HttpRequestMessage(HttpMethod.Get, "/app.js") { Headers = { { "Accept-Encoding", "br" } } };

        second.Headers.IfNoneMatch.Add(etag!);

        var response = await app.Client.SendAsync(second, Cancellation);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Fact]
    public async Task MissingSibling_FallsBackToTheNextAcceptableEncoding()
    {
        await using var app = await StartWithFilesAsync(includeBrotli: false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/app.js") { Headers = { { "Accept-Encoding", "br, gzip" } } };

        var response = await app.Client.SendAsync(request, Cancellation);

        Assert.Equal("gzip", Assert.Single(response.Content.Headers.ContentEncoding));
        Assert.Equal("GZIP", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task ClientWithoutCompression_GetsTheOriginalFile()
    {
        await using var app = await StartWithFilesAsync();

        var response = await app.Client.GetAsync("/app.js", Cancellation);

        Assert.Empty(response.Content.Headers.ContentEncoding);
        Assert.Equal("console.log(1);", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task MatchedEndpoint_IsNeverRewritten()
    {
        await using var app = await StartWithFilesAsync(configureApp: app => app.MapGet("/app.js", () => "from endpoint"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/app.js") { Headers = { { "Accept-Encoding", "br" } } };

        var response = await app.Client.SendAsync(request, Cancellation);

        Assert.Equal("from endpoint", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task CustomEncodings_ComeFromConfiguration()
    {
        await using var app = await StartWithFilesAsync(
            configuration: new() { ["BetterCache:PrecompressedFiles:Encodings:0:Name"] = "zstd", ["BetterCache:PrecompressedFiles:Encodings:0:FileExtension"] = ".zst" },
            extraFiles: root => TestApp.WriteFile(root, "app.js.zst", "ZSTD"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/app.js") { Headers = { { "Accept-Encoding", "zstd, br" } } };

        var response = await app.Client.SendAsync(request, Cancellation);

        Assert.Equal("zstd", Assert.Single(response.Content.Headers.ContentEncoding));
        Assert.Equal("ZSTD", await response.Content.ReadAsStringAsync(Cancellation));
    }

    private static Task<TestApp> StartWithFilesAsync(
        bool includeBrotli = true,
        Action<WebApplication>? configureApp = null,
        Dictionary<string, string?>? configuration = null,
        Action<string>? extraFiles = null) =>
        TestApp.StartAsync(
            app =>
            {
                configureApp?.Invoke(app);
                app.UseStaticFiles();
            },
            configuration: configuration,
            seedWebRoot: root =>
            {
                TestApp.WriteFile(root, "app.js", "console.log(1);");
                TestApp.WriteFile(root, "app.js.gz", "GZIP");

                if (includeBrotli)
                    TestApp.WriteFile(root, "app.js.br", "BROTLI");

                extraFiles?.Invoke(root);
            });
}
