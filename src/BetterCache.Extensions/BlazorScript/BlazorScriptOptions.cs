namespace BetterCache;

/// <summary>Options for serving <c>blazor.web.js</c> compressed and fingerprinted, as .NET 10 does.</summary>
public sealed class BlazorScriptOptions
{
    /// <summary>
    /// Serve <c>blazor.web.js</c> Brotli/gzip-compressed, and under a fingerprinted immutable URL when the page
    /// uses <see cref="BlazorWebScript"/>. Default <c>true</c>. .NET 9 sends it uncompressed (~188 KB) and
    /// revalidates it on every visit.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
