using Microsoft.Extensions.FileProviders;

namespace BetterCache;

/// <summary>Options for serving pre-compressed sibling files.</summary>
public sealed class PrecompressedFilesOptions
{
    /// <summary>Used when <see cref="Encodings"/> is empty: Brotli first, then gzip.</summary>
    public static IReadOnlyList<ContentEncoding> DefaultEncodings { get; } = [new("br", ".br"), new("gzip", ".gz")];

    /// <summary>
    /// Adds the middleware in <c>UseBetterCache</c>. Default <c>true</c>. It only handles requests no endpoint
    /// matched, so it never interferes with <c>MapStaticAssets</c>, which negotiates compression itself.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Supported encodings in server preference order, used to break q-value ties. Empty means <see cref="DefaultEncodings"/>.</summary>
    public IList<ContentEncoding> Encodings { get; set; } = [];

    /// <summary>Where sibling files are looked up. Default: <c>IWebHostEnvironment.WebRootFileProvider</c>.</summary>
    public IFileProvider? FileProvider { get; set; }

    internal IReadOnlyList<ContentEncoding> EffectiveEncodings => Encodings.Count > 0 ? [.. Encodings] : DefaultEncodings;
}
