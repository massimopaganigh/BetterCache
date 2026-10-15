namespace BetterCache;

/// <summary>
/// Root options. Bound automatically from the <see cref="SectionName"/> configuration section;
/// code passed to <see cref="BetterCacheServiceCollectionExtensions.AddBetterCache"/> runs after it.
/// </summary>
public sealed class BetterCacheOptions
{
    /// <summary>Configuration section bound by <see cref="BetterCacheServiceCollectionExtensions.AddBetterCache"/>.</summary>
    public const string SectionName = "BetterCache";

    /// <summary>Compressed, fingerprinted <c>blazor.web.js</c>.</summary>
    public BlazorScriptOptions BlazorScript { get; set; } = new();

    /// <summary>Cache-Control rules applied to responses.</summary>
    public CacheControlOptions CacheControl { get; set; } = new();

    /// <summary>Serving of pre-compressed sibling files (<c>app.js.br</c>) for requests no endpoint handles.</summary>
    public PrecompressedFilesOptions PrecompressedFiles { get; set; } = new();

    /// <summary>Preload hints rendered by <see cref="BetterCacheHead"/>.</summary>
    public PreloadOptions Preload { get; set; } = new();

    /// <summary>How the Blazor WebAssembly runtime caches its boot resources.</summary>
    public WebAssemblyCacheOptions WebAssembly { get; set; } = new();
}
