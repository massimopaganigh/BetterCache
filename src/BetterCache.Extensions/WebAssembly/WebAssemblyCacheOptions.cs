namespace BetterCache;

/// <summary>Options for the Blazor WebAssembly runtime.</summary>
public sealed class WebAssemblyCacheOptions
{
    /// <summary>
    /// Let the runtime load its files through the browser HTTP cache, as .NET 10 does. Default <c>true</c>.
    /// </summary>
    /// <remarks>
    /// The .NET 9 runtime fetches every file with <c>cache: "no-cache"</c>, forcing a revalidation round trip
    /// even for immutable fingerprinted files. With this on, <see cref="BetterCacheHead"/> sets the runtime's
    /// <c>disableNoCacheFetch</c> so those files load from disk with no request at all. Pair it with
    /// <c>&lt;BlazorCacheBootResources&gt;false&lt;/BlazorCacheBootResources&gt;</c> in the client project,
    /// otherwise the runtime keeps reading its own Cache Storage copy instead.
    /// </remarks>
    public bool UseHttpCache { get; set; } = true;

    /// <summary>Boot manifest path relative to the web root and base href. Change it when the WebAssembly path prefix is customized.</summary>
    public string BootManifestPath { get; set; } = "_framework/blazor.boot.json";

    internal string FrameworkDirectory => BootManifestPath[..(BootManifestPath.LastIndexOf('/') + 1)];
}
