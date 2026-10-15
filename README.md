# BetterCache

Bring the .NET 10 Blazor loading and caching behavior to .NET 9 Blazor Web Apps with one NuGet package: WebAssembly files served from the browser HTTP cache, server-side preload hints with the exact fingerprinted URLs, a compressed and fingerprinted `blazor.web.js`, configurable Cache-Control rules and pre-compressed static files.

## Measured effect

Sample app in this repo (`/counter`, Interactive WebAssembly), Release publish, headless Chrome over HTTP/2, 150 ms latency, 4 MB/s, median of 5 runs. "Interactive" is the time from navigation until the counter button responds.

| | first visit | repeat visit | repeat-visit DOMContentLoaded |
|---|---|---|---|
| .NET 9 default | 1904 ms | 625 ms | 328 ms |
| with BetterCache | **1385 ms (−27%)** | **545 ms (−13%)** | **169 ms (−48%)** |

The trade-off: on localhost with no latency at all, first visits are equal (598 vs 597 ms) but a repeat visit is about 90 ms slower (322 vs 416 ms), because .NET 9's Cache Storage keeps decoded copies while the HTTP cache decompresses Brotli from disk. .NET 10 made the same trade when it dropped Cache Storage. To keep Cache Storage, see [Profiles](#profiles).

## What it does, and why

Each point was verified in a browser on .NET 9.0.20:

- **WebAssembly files through the HTTP cache.** The .NET 9 runtime fetches every boot resource with `cache: "no-cache"`, so even immutable fingerprinted files are revalidated, and it keeps a second, uncompressed copy in Cache Storage. BetterCache sets the runtime's `disableNoCacheFetch` (through a Blazor JS initializer) so those files load from disk with no request; with `BlazorCacheBootResources=false` in the client project the Cache Storage copy goes away, as in .NET 10.
- **Preload hints** (`<BetterCacheHead />`, the .NET 10 `ResourcePreloader` equivalent). Without them, the runtime downloads start only after `blazor.web.js` → `dotnet.js` → `blazor.boot.json` have loaded one after another. The server reads the boot manifest once and emits `modulepreload` / `preload` links with the real fingerprinted URLs, so the downloads start while the head is parsed. The browser reuses every preload: no double download, no console warning.
- **`blazor.web.js` compressed and fingerprinted** (`<BlazorWebScript />`). .NET 9 serves it uncompressed (188 KB) and revalidates it on every visit. BetterCache serves the framework's own file Brotli-compressed (45 KB) under an immutable fingerprinted URL.
- **Cache-Control rules.** First match wins: your rules, then rules registered in DI, then the defaults (fingerprinted file names → `public, max-age=31536000, immutable`; any other `_framework` file → `no-cache`). By default a rule never replaces a header the endpoint already set, so `MapStaticAssets` keeps its own headers; error responses are never given a policy.
- **Pre-compressed static files** for requests no endpoint handles (for example with `UseStaticFiles`): serves `app.js.br` / `app.js.gz` with RFC 9110 `Accept-Encoding` negotiation (q-values, `q=0`, `*`, `identity`), through `StaticFileMiddleware`, so ETag, 304, HEAD and Range work. `MapStaticAssets` already negotiates compression, so its endpoints are left alone.

What it no longer does: the .NET 9 SDK already compresses every static web asset at publish (Brotli at maximum quality, measured identical to quality 11), so the old `CompressAssets` MSBuild task was removed.

## Install

```shell
dotnet add package BetterCache.Extensions
```

Also published to GitHub Packages:

```shell
dotnet nuget add source --username <your-github-user> --password <YOUR_GITHUB_PAT> --store-password-in-clear-text --name github "https://nuget.pkg.github.com/massimopaganigh/index.json"
dotnet add package BetterCache.Extensions --source github
```

## Usage

`Program.cs` of the server project:

```csharp
using BetterCache;

builder.Services.AddBetterCache();

var app = builder.Build();

app.UseBetterCache(); // before MapStaticAssets / UseStaticFiles
app.MapStaticAssets();
```

`App.razor`:

```razor
@using BetterCache

<head>
    <base href="/" />
    <BetterCacheHead />   @* after <base href> *@
    ...
</head>
<body>
    <Routes />
    <BlazorWebScript />   @* replaces <script src="_framework/blazor.web.js"></script> *@
</body>
```

`<BlazorWebScript />` passes extra attributes through, for example `<BlazorWebScript autostart="false" />`.

The client (`.Client`) project:

```xml
<PropertyGroup>
  <BlazorCacheBootResources>false</BlazorCacheBootResources>
</PropertyGroup>
```

Without that property the runtime keeps reading its Cache Storage copy; BetterCache then skips the WebAssembly preloads, which would be wasted, and logs a warning at the first request.

## Profiles

| Goal | Client project | `BetterCache` options |
|---|---|---|
| .NET 10 behavior (default, best on real networks) | `BlazorCacheBootResources=false` | defaults |
| Keep .NET 9 Cache Storage (fastest repeat visits on very fast links) | nothing | `"Preload": { "PreloadWebAssembly": false }`, `"WebAssembly": { "UseHttpCache": false }` |

The second profile still gets the compressed, fingerprinted `blazor.web.js`, the Cache-Control rules and the pre-compressed files.

## Configuration

Options bind from the `BetterCache` section; code passed to `AddBetterCache(options => ...)` runs after it. Invalid values (for example a bad regex) fail at startup.

```jsonc
{
  "BetterCache": {
    "CacheControl": {
      "Enabled": true,
      "OverrideExisting": false,   // replace headers set by endpoints such as MapStaticAssets
      "UseDefaultRules": true,
      "Rules": [
        // case-insensitive regex on the request path, evaluated with RegexOptions.NonBacktracking
        { "PathPattern": "^/images/", "Value": "public, max-age=86400" }
      ]
    },
    "BlazorScript": { "Enabled": true },
    "Preload": {
      "Enabled": true,
      "PreloadWebAssembly": true,
      // boot manifest sections; default: jsModuleRuntime, jsModuleNative, wasmNative, coreAssembly, assembly
      "WebAssemblySections": [],
      "Links": [
        { "Href": "fonts/inter.woff2", "As": "font", "CrossOrigin": "anonymous" }
      ]
    },
    "PrecompressedFiles": {
      "Enabled": true,
      // server preference order; default: br (.br), gzip (.gz)
      "Encodings": [ { "Name": "zstd", "FileExtension": ".zst" }, { "Name": "br", "FileExtension": ".br" } ]
    },
    "WebAssembly": {
      "UseHttpCache": true,
      "BootManifestPath": "_framework/blazor.boot.json"   // change with a custom WebAssembly path prefix
    }
  }
}
```

```csharp
builder.Services.AddBetterCache(options =>
{
    options.CacheControl.Rules.Add(new CacheControlRule("^/downloads/", "no-store"));
    options.PrecompressedFiles.FileProvider = new PhysicalFileProvider("/srv/static");
});
```

## Extension points

Every service is registered with `TryAdd`, so you extend or replace each piece on its own:

| Interface | Default | Extend or replace |
|---|---|---|
| `ICacheControlRule` | `CacheControlRule` (regex), `FingerprintedFileRule` | `services.AddSingleton<ICacheControlRule, MyRule>()` adds a rule after the configured ones. |
| `ICacheControlPolicy` | first match over the rules | Register your own before `AddBetterCache()` to take full control. |
| `IContentEncodingNegotiator` | RFC 9110 `Accept-Encoding` | Register your own before `AddBetterCache()`. |
| `IPreloadLinkSource` | initializer, WebAssembly boot manifest, `Preload:Links` | `services.AddSingleton<IPreloadLinkSource, MySource>()`; links are de-duplicated by `Href`. |

A rule receives the `HttpContext` when the response starts, so it can look at the status, Content-Type or anything else:

```csharp
sealed class HtmlNoStoreRule : ICacheControlRule
{
    public string? Evaluate(HttpContext context, PathString path) =>
        context.Response.ContentType?.StartsWith("text/html") == true ? "no-store" : null;
}
```

## Notes

- In Development, `MapStaticAssets` sends `no-cache` and ignores `If-None-Match` (for hot reload), so with the HTTP cache every reload downloads the WebAssembly files again from localhost. Published apps get `304` / immutable caching.
- Preload links are rendered on every page that includes `<BetterCacheHead />`. If only a few pages are interactive, place it conditionally or set `Preload:PreloadWebAssembly` to `false`.
- The boot manifest is read once per process; restart the server after rebuilding only the client.
- .NET 9 calls a web JS initializer's `beforeWebAssemblyStart` only when `Blazor.start` receives explicit WebAssembly options, so the initializer uses `beforeWebStart` and the `dotnet.js` builder instead.

## Migrating from 2026.1.x

- `<BetterCachePreload />` → `<BetterCacheHead />`, and `<script src="_framework/blazor.web.js">` → `<BlazorWebScript />`.
- `BetterCacheOptions` is now grouped: `ImmutableMaxAgeSeconds` / `FrameworkPathSegment` / `BootManifestFileName` → `CacheControl.Rules`; `ServePrecompressedAssets` → `PrecompressedFiles.Enabled`; `EnablePreloadHints` / `PreloadAssets` / `PreloadFrameworkFromBootManifest` → `Preload`; `BootManifestPath` → `WebAssembly.BootManifestPath`.
- The MSBuild properties `BetterCacheCompress*` and `BetterCacheDisableBootCache` are gone. The old `BlazorCacheBootResources` default never reached the client project; set it there yourself.
- The old middleware marked every `/_framework/` file immutable, including non-fingerprinted ones such as `dotnet.js` and `blazor.web.js`; now only fingerprinted names are.

## Projects

- `src/BetterCache.Extensions` (net9.0): the package.
- `tests/BetterCache.Extensions.Tests`: xUnit v3 tests on TestServer.
- `samples/BetterCache.Sample` + `samples/BetterCache.Sample.Client`: Blazor Web App used for the measurements.

## Publish

Tagged pushes (`v*`) run `.github/workflows/publish.yml`: tests, pack, then push to nuget.org (`secrets.NUGET_API_KEY`) and GitHub Packages (`secrets.GITHUB_TOKEN`).

```shell
dotnet test
dotnet pack src/BetterCache.Extensions/BetterCache.Extensions.csproj -c Release -o ./artifacts
dotnet nuget push "./artifacts/*.nupkg" --source https://api.nuget.org/v3/index.json --api-key <NUGET_API_KEY> --skip-duplicate
```

## License

MIT, see [LICENSE](LICENSE).
