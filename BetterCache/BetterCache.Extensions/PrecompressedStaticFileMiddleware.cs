namespace BetterCache
{
    /// <summary>
    /// Rewrites static asset requests to pre-compressed .br / .gz siblings when the client
    /// supports the encoding and the file exists. Sets Content-Encoding + Vary appropriately.
    /// </summary>
    public sealed class PrecompressedStaticFileMiddleware(RequestDelegate next, IOptions<BetterCacheOptions> options, IHostEnvironment env)
    {
        private readonly FileExtensionContentTypeProvider _mime = new();
        private readonly BetterCacheOptions _options = options.Value;

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

            var webRoot = env.ContentRootPath;
            var wwwroot = Path.Combine(webRoot, "wwwroot");
            var compressed = Path.Combine(wwwroot, path!.TrimStart('/').Replace('/', Path.DirectorySeparatorChar) + extension);

            if (!File.Exists(compressed))
            {
                await next(context);

                return;
            }

            if (_mime.TryGetContentType(path, out var contentType))
                context.Response.ContentType = contentType;

            context.Response.Headers.ContentEncoding = encoding;

            context.Response.Headers.Append("Vary", "Accept-Encoding");

            context.Response.ContentLength = new FileInfo(compressed).Length;

            await context.Response.SendFileAsync(compressed);
        }

        #region PRIVATE METHODS
        private static bool IsCompressibleAsset(string path)
        {
            if (IsSatelliteAssembly(path))
                return false;

            return path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSatelliteAssembly(string path)
        {
            if (!path.EndsWith(".wasm", StringComparison.OrdinalIgnoreCase))
                return false;

            var fileName = Path.GetFileName(path.AsSpan());

            return fileName.IndexOf(".resources.".AsSpan(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static (string? Encoding, string Ext) PickEncoding(StringValues accept)
        {
            foreach (var header in accept)
            {
                if (header is null)
                    continue;

                if (header.Contains("br", StringComparison.OrdinalIgnoreCase))
                    return ("br", ".br");
            }

            foreach (var header in accept)
            {
                if (header is null)
                    continue;

                if (header.Contains("gzip", StringComparison.OrdinalIgnoreCase))
                    return ("gzip", ".gz");
            }

            return (null, string.Empty);
        }
        #endregion
    }
}
