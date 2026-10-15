using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace BetterCache.Tests;

public sealed class WebAssemblyTests
{
    private const string Manifest = """
        {
          "mainAssemblyName": "App.Client",
          "resources": {
            "hash": "sha256-manifest",
            "jsModuleRuntime": { "dotnet.runtime.8ha609180a.js": "sha256-runtime" },
            "wasmNative": { "dotnet.native.pkfvibo6od.wasm": "sha256-native" },
            "assembly": { "App.Client.qhofiavyzc.wasm": "sha256-app" },
            "icu": { "icudt_EFIGS.tptq2av103.dat": "sha256-icu" },
            "satelliteResources": { "it": { "App.resources.abc123defg.wasm": "sha256-it" } }
          }
        }
        """;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("", false)] // the SDK omits the key when BlazorCacheBootResources is false
    [InlineData("\"cacheBootResources\": false,", false)]
    [InlineData("\"cacheBootResources\": true,", true)]
    public void Parse_ReadsTheCacheStorageFlag(string flag, bool expected)
    {
        var manifest = BootManifest.Parse(new MemoryStream(Encoding.UTF8.GetBytes(Manifest.Replace("\"mainAssemblyName\"", flag + "\"mainAssemblyName\""))));

        Assert.Equal(expected, manifest.CacheBootResources);
    }

    [Fact]
    public void CreateLinks_MapsSectionsToTheRequestsTheRuntimeMakes()
    {
        var manifest = BootManifest.Parse(new MemoryStream(Encoding.UTF8.GetBytes(Manifest)));

        var links = WebAssemblyPreloadLinkSource.CreateLinks(manifest, "_framework/", PreloadOptions.DefaultWebAssemblySections);

        Assert.Collection(
            links,
            runtime =>
            {
                Assert.Equal(("modulepreload", "_framework/dotnet.runtime.8ha609180a.js", "sha256-runtime"), (runtime.Rel, runtime.Href, runtime.Integrity));
                Assert.Null(runtime.As);
            },
            native => Assert.Equal(("preload", "fetch", "anonymous", "_framework/dotnet.native.pkfvibo6od.wasm", (string?)null), (native.Rel, native.As, native.CrossOrigin, native.Href, native.Integrity)),
            assembly => Assert.Equal("_framework/App.Client.qhofiavyzc.wasm", assembly.Href));
    }

    [Fact]
    public async Task Head_RendersRuntimeConfigAndPreloads()
    {
        await using var app = await StartWithManifestAsync(Manifest);

        var html = await app.Client.GetStringAsync("/head", Cancellation);

        Assert.Contains("""<meta name="better-cache-webassembly-config" content="{&quot;dotnetJs&quot;:&quot;_framework/dotnet.js&quot;,&quot;config&quot;:{&quot;disableNoCacheFetch&quot;:true}}" />""", html);
        Assert.Contains("""<link rel="modulepreload" href="_content/BetterCache.Extensions/BetterCache.Extensions.lib.module.js" />""", html);
        Assert.Contains("""<link rel="modulepreload" href="_framework/dotnet.js" />""", html);
        Assert.Contains("""<link rel="modulepreload" href="_framework/dotnet.runtime.8ha609180a.js" integrity="sha256-runtime" />""", html);
        Assert.Contains("""<link rel="preload" href="_framework/dotnet.native.pkfvibo6od.wasm" as="fetch" crossorigin="anonymous" />""", html);
        Assert.DoesNotContain("icudt", html);
    }

    [Fact]
    public async Task Head_SkipsWebAssemblyPreloads_WhenTheRuntimeUsesCacheStorage()
    {
        await using var app = await StartWithManifestAsync(Manifest.Replace("\"mainAssemblyName\"", "\"cacheBootResources\": true, \"mainAssemblyName\""));

        var html = await app.Client.GetStringAsync("/head", Cancellation);

        Assert.DoesNotContain("_framework/", html.Replace("_framework/dotnet.js&quot;", string.Empty));
    }

    [Fact]
    public async Task Head_HonorsConfiguredSectionsAndLinks()
    {
        await using var app = await StartWithManifestAsync(Manifest, new()
        {
            ["BetterCache:Preload:WebAssemblySections:0"] = "icu",
            ["BetterCache:Preload:Links:0:Href"] = "fonts/inter.woff2",
            ["BetterCache:Preload:Links:0:As"] = "font",
            ["BetterCache:Preload:Links:0:CrossOrigin"] = "anonymous",
            ["BetterCache:WebAssembly:UseHttpCache"] = "false",
        });

        var html = await app.Client.GetStringAsync("/head", Cancellation);

        Assert.Contains("_framework/icudt_EFIGS.tptq2av103.dat", html);
        Assert.DoesNotContain("dotnet.native", html);
        Assert.Contains("""<link rel="preload" href="fonts/inter.woff2" as="font" crossorigin="anonymous" />""", html);
        Assert.DoesNotContain("better-cache-webassembly-config", html);
    }

    [Fact]
    public async Task Head_RendersNothingForWebAssembly_WithoutABootManifest()
    {
        await using var app = await StartWithManifestAsync(manifest: null);

        var html = await app.Client.GetStringAsync("/head", Cancellation);

        Assert.DoesNotContain("better-cache-webassembly-config", html);
        Assert.DoesNotContain("_framework/", html);
    }

    private static Task<TestApp> StartWithManifestAsync(string? manifest, Dictionary<string, string?>? configuration = null) =>
        TestApp.StartAsync(
            app => app.MapGet("/head", () => new RazorComponentResult<BetterCacheHead>()),
            services => services.AddRazorComponents(),
            configuration,
            root =>
            {
                if (manifest is not null)
                    TestApp.WriteFile(root, "_framework/blazor.boot.json", manifest);
            });
}
