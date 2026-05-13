namespace BetterCache
{
    /// <summary>
    /// Rewrites static asset requests to pre-compressed .br / .gz siblings when the client
    /// supports the encoding and the file exists. Sets Content-Encoding + Vary appropriately.
    /// </summary>
    public sealed class PrecompressedStaticFileMiddleware(RequestDelegate next, IOptions<BetterCacheOptions> options, IWebHostEnvironment env)
    {
#pragma warning disable IDE0028 // Semplifica l'inizializzazione della raccolta
        private static readonly HashSet<string> CompressibleExtensions = new(StringComparer.OrdinalIgnoreCase) { ".js", ".mjs", ".css", ".wasm", ".json" };
#pragma warning restore IDE0028 // Semplifica l'inizializzazione della raccolta
        private readonly FileExtensionContentTypeProvider _mime = new();
        private readonly BetterCacheOptions _options = options.Value;
        private readonly string _wwwroot = env.WebRootPath;

        public async Task InvokeAsync(HttpContext context)
        {
            if (!_options.ServePrecompressedAssets)
            {
                await next(context);

                return;
            }

            if (!HttpMethods.IsGet(context.Request.Method)
                && !HttpMethods.IsHead(context.Request.Method))
            {
                await next(context);

                return;
            }

            var path = context.Request.Path.Value;

            if (string.IsNullOrEmpty(path)
                || !IsCompressibleAsset(path))
            {
                await next(context);

                return;
            }

            var accept = context.Request.Headers.AcceptEncoding;
            var (encoding, extension) = PickEncoding(accept);

            if (encoding is null)
            {
                await next(context);

                return;
            }

            var compressed = Path.Combine(_wwwroot, path!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar) + extension);
            var fi = new FileInfo(compressed);

            if (!fi.Exists)
            {
                await next(context);

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
        private static bool IsCompressibleAsset(string path)
        {
            var ext = Path.GetExtension(path.AsSpan());

            if (ext.IsEmpty || !CompressibleExtensions.Contains(ext.ToString()))
                return false;

            if (ext.Equals(".wasm", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = Path.GetFileName(path.AsSpan());

                if (fileName.IndexOf(".resources.".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }

            return true;
        }

        private static (string? Encoding, string Ext) PickEncoding(StringValues accept)
        {
            bool hasBr = false;
            bool hasGzip = false;

            foreach (var header in accept)
            {
                if (header is null)
                    continue;

                if (!hasBr
                    && header.Contains("br", StringComparison.OrdinalIgnoreCase))
                    hasBr = true;

                if (!hasGzip
                    && header.Contains("gzip", StringComparison.OrdinalIgnoreCase))
                    hasGzip = true;

                if (hasBr
                    && hasGzip)
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
