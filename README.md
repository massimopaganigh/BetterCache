# BetterCache

Bring .NET 10 Blazor cache optimizations to .NET 9 projects via a single NuGet package: fingerprinting, pre-compression (Brotli/Gzip), preload hints, and aggressive immutable HTTP cache headers.

## What it does

- **Immutable cache headers** for fingerprinted `_framework/*` assets (1-year `max-age, immutable`), with `must-revalidate` on `blazor.boot.json`.
- **Pre-compressed static file serving** — rewrites requests to `.br` / `.gz` siblings when the client supports the encoding.
- **MSBuild `CompressAssets` task** — produces `.br` / `.gz` siblings at build time using `System.IO.Compression.BrotliStream` (no external `brotli.exe` dependency). No-op on assets the Blazor WebAssembly SDK already compresses; covers Blazor Server apps and custom static assets.
- **Preload hints** — optional inline script that fetches `blazor.boot.json` client-side and emits `<link rel="preload">` tags with the real fingerprinted URLs, avoiding the "preloaded but not used" warning.

## Install

Published to **nuget.org** and **GitHub Packages**.

### nuget.org (default)

```shell
dotnet add package BetterCache.Extensions
```

### GitHub Packages (mirror)

```shell
dotnet nuget add source \
  --username <your-github-user> \
  --password <YOUR_GITHUB_PAT> \
  --store-password-in-clear-text \
  --name github \
  "https://nuget.pkg.github.com/massimopaganigh/index.json"

dotnet add package BetterCache.Extensions --source github
```

The PAT needs the `read:packages` scope (and `write:packages` to publish).

MSBuild props/targets auto-import via the NuGet `build/` convention.

## Publish

Tagged pushes (`v*`) trigger `.github/workflows/publish.yml`, which packs `BetterCache.Extensions` and pushes to both feeds:

- **nuget.org** via `secrets.NUGET_API_KEY` (repo secret — create from https://www.nuget.org/account/apikeys)
- **GitHub Packages** via `secrets.GITHUB_TOKEN`

Manual publish:

```shell
dotnet pack BetterCache/BetterCache.Extensions/BetterCache.Extensions.csproj -c Release -o ./artifacts
dotnet nuget push "./artifacts/*.nupkg" --source https://api.nuget.org/v3/index.json --api-key <NUGET_API_KEY> --skip-duplicate
dotnet nuget push "./artifacts/*.nupkg" --source github --skip-duplicate
```

## Usage

`Program.cs`:

```csharp
using BetterCache;

builder.Services.AddBetterCache(options =>
{
    options.EnablePreloadHints = true;
    options.PreloadFrameworkFromBootManifest = true;
});

var app = builder.Build();

app.UseBetterCache(); // before MapStaticAssets
app.MapStaticAssets();
```

Head of your root layout:

```razor
@using BetterCache
<BetterCachePreload />
```

## Options (`BetterCacheOptions`)

| Property | Default | Purpose |
|---|---|---|
| `ImmutableMaxAgeSeconds` | `31536000` | `max-age` for fingerprinted framework assets. |
| `FrameworkPathSegment` | `/_framework/` | Path segment that identifies framework assets. |
| `BootManifestFileName` | `blazor.boot.json` | Always revalidated. |
| `ServePrecompressedAssets` | `true` | Serve `.br` / `.gz` siblings. |
| `EnablePreloadHints` | `false` | Emit `<link rel="preload">` tags. |
| `PreloadAssets` | `[]` | Static preload entries (use only for fingerprint-stable URLs). |
| `PreloadFrameworkFromBootManifest` | `false` | Inject inline script to preload fingerprinted framework assets. |
| `BootManifestPath` | `_framework/blazor.boot.json` | Relative to base href. |

## Projects

- `BetterCache.Extensions` (net9.0) — runtime library: middleware, extensions, `BetterCachePreload` component, `build/*.props|targets`.
- `BetterCache.Extensions.Tasks` (net8.0) — MSBuild `CompressAssets` task assembly.
- `BetterCache` — sample Blazor Web App consumer.
- `BetterCache.Client` — sample Blazor WebAssembly client.

## Notes

- **Do not** preload `_framework/dotnet.runtime.js` or `_framework/dotnet.native.wasm` with static URLs in .NET 9 — they are fingerprinted (`dotnet.runtime.<hash>.js`) and the URL will not match the real fetch. Use `PreloadFrameworkFromBootManifest` instead.
- **ProjectReference consumers** must explicitly `<Import>` the `build/*.props|targets` files — auto-import only applies via NuGet.
- `PackageId` is `BetterCache.Extensions`, not `BetterCache`, to avoid a NuGet restore collision with the sample project name.

## License

MIT — see [LICENSE](LICENSE).
