using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BetterCache;

/// <summary>Reads the boot manifest from the web root once; <c>null</c> when the app has no WebAssembly client.</summary>
internal sealed class BootManifestProvider
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger _logger;
    private readonly BetterCacheOptions _options;

    // shortcut: read once per process; a client rebuild without a server restart (dev only) keeps stale data until restart.
    private readonly Lazy<BootManifest?> _manifest;

    public BootManifestProvider(IWebHostEnvironment environment, IOptions<BetterCacheOptions> options, ILogger<BootManifestProvider> logger)
    {
        _environment = environment;
        _options = options.Value;
        _logger = logger;
        _manifest = new(Load);
    }

    public BootManifest? Manifest => _manifest.Value;

    private BootManifest? Load()
    {
        var path = _options.WebAssembly.BootManifestPath;
        var file = _environment.WebRootFileProvider.GetFileInfo(path);

        if (!file.Exists)
        {
            _logger.LogDebug("No WebAssembly boot manifest at {Path}; WebAssembly features are off.", path);

            return null;
        }

        BootManifest manifest;

        try
        {
            using var stream = file.CreateReadStream();

            manifest = BootManifest.Parse(stream);
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Could not parse the WebAssembly boot manifest at {Path}; WebAssembly features are off.", path);

            return null;
        }

        if (manifest.CacheBootResources && _options.Preload is { Enabled: true, PreloadWebAssembly: true })
            _logger.LogWarning(
                "The WebAssembly runtime keeps its files in Cache Storage (cacheBootResources is true in {Path}), so BetterCache does not preload them: "
                + "the runtime would read that copy and leave the preloads unused. Set <BlazorCacheBootResources>false</BlazorCacheBootResources> "
                + "in the client project to load them through the HTTP cache, as .NET 10 does, or set BetterCache:Preload:PreloadWebAssembly "
                + "to false to keep Cache Storage.",
                path);

        return manifest;
    }
}
