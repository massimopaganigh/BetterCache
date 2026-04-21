namespace BetterCache
{
    /// <summary>
    /// Options for <see cref="BetterCacheMiddleware"/>.
    /// </summary>
    public sealed class BetterCacheOptions
    {
        /// <summary>File name of the Blazor boot manifest (always revalidated).</summary>
        public string BootManifestFileName { get; set; } = "blazor.boot.json";

        /// <summary>Path (relative to base href) to the Blazor boot manifest.</summary>
        public string BootManifestPath { get; set; } = "_framework/blazor.boot.json";

        /// <summary>
        /// Emit &lt;link rel="preload"&gt; hints. Off by default. When enabled alongside
        /// <see cref="PreloadFrameworkFromBootManifest"/>, a small inline script parses
        /// <c>blazor.boot.json</c> in the browser and creates preload links with the real
        /// fingerprinted URLs — avoiding the "preloaded but not used" warning caused by
        /// mismatched hardcoded paths.
        /// </summary>
        public bool EnablePreloadHints { get; set; }

        /// <summary>Path segment identifying framework assets. Default "/_framework/".</summary>
        public string FrameworkPathSegment { get; set; } = "/_framework/";

        /// <summary>Max-age (seconds) for fingerprinted framework assets. Default 1 year.</summary>
        public int ImmutableMaxAgeSeconds { get; set; } = 31536000;

        /// <summary>
        /// Static entries in <see cref="PreloadAssets"/> emitted verbatim. Use only for
        /// fingerprint-stable URLs (e.g. your own ES modules). Fingerprinted framework
        /// runtime files must go through <see cref="PreloadFrameworkFromBootManifest"/>.
        /// </summary>
        public IList<string> PreloadAssets { get; set; } = [];

        /// <summary>
        /// When true, injects a small inline script that fetches <c>_framework/blazor.boot.json</c>
        /// and creates <c>&lt;link rel="preload"&gt;</c> tags for every resource it lists,
        /// using the real fingerprinted URL each time. Pair with <see cref="EnablePreloadHints"/>.
        /// Only injected on pages that include <see cref="BetterCachePreload"/> in their head.
        /// </summary>
        public bool PreloadFrameworkFromBootManifest { get; set; }

        /// <summary>Serve pre-compressed .br / .gz variants produced at publish time.</summary>
        public bool ServePrecompressedAssets { get; set; } = true;
    }
}
