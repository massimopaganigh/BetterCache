using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BetterCache.Tests;

public sealed class CacheControlTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/_framework/dotnet.native.pkfvibo6od.wasm", true)]
    [InlineData("/app.jago5f97na.css", true)]
    [InlineData("/BetterCache.5jbna5rlia.styles.css", true)]
    [InlineData("/_framework/dotnet.native.pkfvibo6od.wasm.br", true)]
    [InlineData("/_framework/dotnet.js", false)]
    [InlineData("/_framework/blazor.boot.json", false)]
    [InlineData("/lib/jquery.validation.js", false)] // 10 letters, no digit
    [InlineData("/pkfvibo6od.wasm", false)] // base name, not a fingerprint segment
    [InlineData("/app.PKFVIBO6OD.css", false)] // fingerprints are lowercase
    [InlineData("/", false)]
    public void FingerprintedFileRule_RecognizesStaticWebAssetFingerprints(string path, bool expected) =>
        Assert.Equal(expected, FingerprintedFileRule.IsFingerprinted(path));

    [Theory]
    [InlineData("/_framework/dotnet.native.pkfvibo6od.wasm", CacheControlOptions.Immutable)]
    [InlineData("/_framework/blazor.boot.json", "no-cache")]
    [InlineData("/_framework/blazor.web.js", "no-cache")]
    public async Task DefaultRules_ApplyWhenTheEndpointSetsNoHeader(string path, string expected)
    {
        await using var app = await TestApp.StartAsync(app => app.MapGet(path, () => "content"));

        var response = await app.Client.GetAsync(path, Cancellation);

        Assert.Equal(expected, response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task ErrorResponses_NeverGetACachePolicy()
    {
        await using var app = await TestApp.StartAsync();

        var response = await app.Client.GetAsync("/_framework/missing.pkfvibo6od.wasm", Cancellation);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(response.Headers.CacheControl);
    }

    [Theory]
    [InlineData(false, "private")]
    [InlineData(true, CacheControlOptions.Immutable)]
    public async Task ExistingHeader_IsOnlyReplacedWithOverrideExisting(bool overrideExisting, string expected)
    {
        const string path = "/app.jago5f97na.css";

        await using var app = await TestApp.StartAsync(
            app => app.MapGet(path, (HttpContext context) =>
            {
                context.Response.Headers.CacheControl = "private";

                return "content";
            }),
            configuration: new() { ["BetterCache:CacheControl:OverrideExisting"] = overrideExisting.ToString() });

        var response = await app.Client.GetAsync(path, Cancellation);

        Assert.Equal(expected, response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Rules_RunInOrder_ConfiguredThenRegisteredThenDefaults()
    {
        await using var app = await TestApp.StartAsync(
            app =>
            {
                app.MapGet("/_framework/a.pkfvibo6od.wasm", () => "content");
                app.MapGet("/images/logo.png", () => "content");
            },
            services => services.AddSingleton<ICacheControlRule>(new CacheControlRule(@"\.(wasm|png)$", "public, max-age=60")),
            new() { ["BetterCache:CacheControl:Rules:0:PathPattern"] = "^/images/", ["BetterCache:CacheControl:Rules:0:Value"] = "public, max-age=3600" });

        var configured = await app.Client.GetAsync("/images/logo.png", Cancellation);
        var registered = await app.Client.GetAsync("/_framework/a.pkfvibo6od.wasm", Cancellation);

        Assert.Equal("public, max-age=3600", configured.Headers.CacheControl?.ToString());
        Assert.Equal("public, max-age=60", registered.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task DefaultRules_CanBeTurnedOff()
    {
        const string path = "/_framework/blazor.boot.json";

        await using var app = await TestApp.StartAsync(
            app => app.MapGet(path, () => "content"),
            configuration: new() { ["BetterCache:CacheControl:UseDefaultRules"] = "false" });

        var response = await app.Client.GetAsync(path, Cancellation);

        Assert.Null(response.Headers.CacheControl);
    }

    [Theory]
    [InlineData("(?=lookahead)", "x")] // unsupported by the non-backtracking engine
    [InlineData("[unclosed", "x")]
    [InlineData("^/ok$", "")]
    public async Task InvalidRules_FailAtStartup(string pattern, string value)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["BetterCache:CacheControl:Rules:0:PathPattern"] = pattern,
            ["BetterCache:CacheControl:Rules:0:Value"] = value,
        };

        await Assert.ThrowsAsync<OptionsValidationException>(() => TestApp.StartAsync(configuration: configuration));
    }
}
