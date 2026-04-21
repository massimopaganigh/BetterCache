namespace BetterCache
{
    /// <summary>
    /// Emits &lt;link rel="preload"&gt; tags for configured static assets, plus an optional inline
    /// script that parses <c>blazor.boot.json</c> and preloads the real fingerprinted framework
    /// URLs. Place inside the &lt;head&gt; of your App.razor, gated on the routes where the WASM
    /// runtime will actually boot.
    /// </summary>
    public sealed class BetterCachePreload : ComponentBase
    {
        /// <summary>
        /// Inline script: fetches the boot manifest, then injects a
        /// <c>&lt;link rel="preload" as="fetch" crossorigin="anonymous"&gt;</c> element for
        /// every listed framework resource. The browser deduplicates the preload with
        /// Blazor's later <c>fetch()</c> of the same URL (matching <c>as</c>/<c>crossorigin</c>),
        /// so each asset is downloaded exactly once and the runtime reuses the preloaded
        /// response. A plain <c>fetch()</c> would race against Blazor's own fetch — browsers
        /// do not coalesce concurrent fetches on the same URL, so the asset would be
        /// downloaded twice (once for the warm, once for Blazor).
        /// </summary>
        private static string BuildBootPreloadScript(string bootPath)
        {
            var escaped = bootPath.Replace("\"", "\\\"");

            return $$"""
        (function(){
          var url = "{{escaped}}";
          var frameworkBase = url.substring(0, url.lastIndexOf('/') + 1);
          var seen = new Set();
          function isSatelliteAssembly(name){
            return /\.resources\.\w+\.wasm$/i.test(name);
          }
          function warm(name){
            if (isSatelliteAssembly(name)) return;
            var href = frameworkBase + name;
            if (seen.has(href)) return;
            seen.add(href);
            var link = document.createElement('link');
            link.rel = 'preload';
            link.as = 'fetch';
            link.crossOrigin = 'anonymous';
            link.href = href;
            if (/\.wasm$/i.test(name)) link.type = 'application/wasm';
            document.head.appendChild(link);
          }
          function walk(node, prefix){
            if (!node || typeof node !== 'object') return;
            Object.keys(node).forEach(function(k){
              var v = node[k];
              if (typeof v === 'string') {
                warm(prefix + k);
              } else if (v && typeof v === 'object') {
                walk(v, prefix + k + '/');
              }
            });
          }
          fetch(url, { cache: 'no-cache', credentials: 'same-origin' })
            .then(function(r){ return r.ok ? r.json() : null; })
            .then(function(boot){
              if (!boot || !boot.resources) return;
              var skipBuckets = new Set(['satelliteResources']);
              Object.keys(boot.resources).forEach(function(bucketKey){
                if (skipBuckets.has(bucketKey)) return;
                var bucket = boot.resources[bucketKey];
                if (!bucket || typeof bucket !== 'object') return;
                Object.keys(bucket).forEach(function(k){
                  var v = bucket[k];
                  if (typeof v === 'string') {
                    warm(k);
                  } else if (v && typeof v === 'object') {
                    walk(v, k + '/');
                  }
                });
              });
            })
            .catch(function(){});
        })();
        """;
        }

        #region PRIVATE METHODS
        private static string GuessAs(string path)
        {
            if (path.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
                return "fetch";

            if (path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase))
                return "script";

            if (path.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                return "style";

            return "fetch";
        }
        #endregion

        [Inject]
        private IOptions<BetterCacheOptions> OptionsAccessor { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            var opts = OptionsAccessor.Value;

            if (!opts.EnablePreloadHints)
                return;

            foreach (var asset in opts.PreloadAssets)
            {
                builder.OpenElement(0, "link");
                builder.AddAttribute(1, "rel", "preload");
                builder.AddAttribute(2, "as", GuessAs(asset));
                builder.AddAttribute(3, "href", asset);
                builder.AddAttribute(4, "crossorigin", "anonymous");

                if (asset.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
                    builder.AddAttribute(5, "type", "application/wasm");

                builder.CloseElement();
            }

            if (opts.PreloadFrameworkFromBootManifest)
            {
                builder.OpenElement(10, "script");
                builder.AddMarkupContent(11, BuildBootPreloadScript(opts.BootManifestPath));
                builder.CloseElement();
            }
        }
    }
}
