namespace BetterCache
{
    /// <summary>
    /// Rewrites static asset requests to pre-compressed .br / .gz siblings when the client
    /// supports the encoding and the file exists. Sets Content-Encoding + Vary appropriately.
    /// </summary>
    public sealed class PrecompressedStaticFileMiddleware
    {
        // #4 — static readonly HashSet for O(1) extension lookup.
        private static readonly HashSet<string> CompressibleExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".js", ".mjs", ".css", ".wasm", ".json" };

        private readonly FileExtensionContentTypeProvider _mime = new();
        private readonly RequestDelegate _next;
        private readonly BetterCacheOptions _options;
        // #1 — wwwroot path resolved once in the constructor via IWebHostEnvironment.WebRootPath.
        private readonly string _wwwroot;

        public PrecompressedStaticFileMiddleware(RequestDelegate next, IOptions<BetterCacheOptions> options, IWebHostEnvironment env)
        {
            _next = next;
            _options = options.Value;
            _wwwroot = env.WebRootPath;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_options.ServePrecompressedAssets)
            {
                await _next(context);

                return;
            }

            if (!HttpMethods.IsGet(context.Request.Method)
                && !HttpMethods.IsHead(context.Request.Method))
            {
                await _next(context);

                return;
            }

            var path = context.Request.Path.Value;

            if (string.IsNullOrEmpty(path)
                || !IsCompressibleAsset(path))
            {
                await _next(context);

                return;
            }

            var accept = context.Request.Headers.AcceptEncoding;
            var (encoding, extension) = PickEncoding(accept);

            if (encoding is null)
            {
                await _next(context);

                return;
            }

            var compressed = Path.Combine(_wwwroot, path!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar) + extension);

            // #2 — single FileInfo to avoid two filesystem calls.
            var fi = new FileInfo(compressed);

            if (!fi.Exists)
            {
                await _next(context);

                return;
            }

            if (_mime.TryGetContentType(path, out var contentType))
                context.Response.ContentType = contentType;

            context.Response.Headers.ContentEncoding = encoding;

            context.Response.Headers.Append("Vary", "Accept-Encoding");

            context.Response.ContentLength = fi.Length;

            await context.Response.SendFileAsync(compressed);
        }

        #region PRIVATE METHODS
        // #4 — uses the static HashSet; also avoids the separate IsSatelliteAssembly call for non-.wasm paths.
        private static bool IsCompressibleAsset(string path)
        {
            var ext = Path.GetExtension(path.AsSpan());

            if (ext.IsEmpty || !CompressibleExtensions.Contains(ext.ToString()))
                return false;

            // Satellite assemblies (.resources.<hash>.wasm) must not be served pre-compressed.
            if (ext.Equals(".wasm", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(path.AsSpan());

                if (fileName.IndexOf(".resources.".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            return true;
        }

        // #3 — single-pass: collects support for both encodings in one loop, then decides.
        private static (string? Encoding, string Ext) PickEncoding(StringValues accept)
        {
            bool hasBr = false;
            bool hasGzip = false;

            foreach (var header in accept)
            {
                if (header is null)
                    continue;

                if (!hasBr && header.Contains("br", StringComparison.OrdinalIgnoreCase))
                    hasBr = true;

                if (!hasGzip && header.Contains("gzip", StringComparison.OrdinalIgnoreCase))
                    hasGzip = true;

                if (hasBr && hasGzip)
                    break;
            }

            if (hasBr)
                return ("br", ".br");

            if (hasGzip)
                return ("gzip", ".gz");

            return (null, string.Empty);
        }
        #endregion
    }
}
