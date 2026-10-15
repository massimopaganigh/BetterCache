namespace BetterCache;

/// <summary>A <c>&lt;link&gt;</c> element rendered by <see cref="BetterCacheHead"/>. Null attributes are omitted.</summary>
public sealed class PreloadLink
{
    /// <summary>URL, relative to the document base href or absolute.</summary>
    public string Href { get; set; } = string.Empty;

    /// <summary><c>preload</c> (default), <c>modulepreload</c>, <c>prefetch</c>, <c>preconnect</c>…</summary>
    public string Rel { get; set; } = "preload";

    /// <summary>Request destination: <c>fetch</c>, <c>script</c>, <c>style</c>, <c>font</c>…</summary>
    public string? As { get; set; }

    /// <summary><c>anonymous</c> or <c>use-credentials</c>; must match how the resource is requested later.</summary>
    public string? CrossOrigin { get; set; }

    /// <summary>Subresource integrity hash; must match the integrity of the later request.</summary>
    public string? Integrity { get; set; }

    /// <summary>MIME type hint.</summary>
    public string? Type { get; set; }

    /// <summary><c>high</c>, <c>low</c> or <c>auto</c>.</summary>
    public string? FetchPriority { get; set; }
}
