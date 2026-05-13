namespace BetterCache.Tasks
{
    /// <summary>
    /// MSBuild task: produces Brotli (.br) and Gzip (.gz) siblings for the configured
    /// static assets. Cross-platform — no external brotli/gzip binaries required.
    /// </summary>
    public sealed class CompressAssets : Task
    {
        // #6 — explicit 256 KB I/O buffer for better throughput on large files (e.g. WASM).
        private const int IoBufferSize = 256 * 1024;

        public override bool Execute()
        {
            if (!Directory.Exists(RootDirectory))
            {
                Log.LogMessage(MessageImportance.Normal, $"[BetterCache] Skipping compression, directory not found: {RootDirectory}");

                return true;
            }

            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var e in Extensions.Split([';', ','], StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = e.Trim();

                extensions.Add(trimmed.StartsWith('.') ? trimmed : "." + trimmed);
            }

            // #5 — thread-safe counters for parallel compression.
            int compressedCount = 0;
            long savedBytes = 0;

            var files = Directory.EnumerateFiles(RootDirectory, "*", SearchOption.AllDirectories)
                .Select(file => (file, info: new FileInfo(file)))
                .Where(t =>
                {
                    var ext = Path.GetExtension(t.file);

                    return extensions.Contains(ext)
                        && !t.file.EndsWith(".br", StringComparison.OrdinalIgnoreCase)
                        && !t.file.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                        && t.info.Length >= MinBytes;
                })
                .ToList();

            // #5 — compress files in parallel to utilise all available cores.
            System.Threading.Tasks.Parallel.ForEach(files, t =>
            {
                var (file, info) = t;

                if (WriteBrotli)
                {
                    var brPath = file + ".br";

                    if (IsStale(brPath, info))
                    {
                        CompressFile(file, brPath, stream => new BrotliStream(stream, CompressionLevel.SmallestSize));

                        Interlocked.Add(ref savedBytes, Math.Max(0, info.Length - new FileInfo(brPath).Length));
                        Interlocked.Increment(ref compressedCount);
                    }
                }

                if (WriteGzip)
                {
                    var gzPath = file + ".gz";

                    if (IsStale(gzPath, info))
                        CompressFile(file, gzPath, stream => new GZipStream(stream, CompressionLevel.SmallestSize));
                }
            });

            Log.LogMessage(MessageImportance.High, $"[BetterCache] Compressed {compressedCount} files, saved ~{savedBytes / 1024} KB.");

            return !Log.HasLoggedErrors;
        }

        public string Extensions { get; set; } = ".js;.mjs;.css;.wasm;.json";

        public int MinBytes { get; set; } = 1024;

        [Required]
        public string RootDirectory { get; set; } = string.Empty;

        public bool WriteBrotli { get; set; } = true;

        public bool WriteGzip { get; set; } = true;

        #region PRIVATE METHODS
        // #6 — 256 KB buffer passed to CopyTo for better I/O throughput.
        private static void CompressFile(string source, string destination, Func<Stream, Stream> wrap)
        {
            using var input = File.OpenRead(source);
            using var output = File.Create(destination);
            using var compressor = wrap(output);

            input.CopyTo(compressor, IoBufferSize);
        }

        private static bool IsStale(string outputPath, FileInfo source)
        {
            if (!File.Exists(outputPath))
                return true;

            return new FileInfo(outputPath).LastWriteTimeUtc < source.LastWriteTimeUtc;
        }
        #endregion
    }
}
