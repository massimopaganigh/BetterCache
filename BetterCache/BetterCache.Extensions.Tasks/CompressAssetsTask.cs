namespace BetterCache.Tasks
{
    /// <summary>
    /// MSBuild task: produces Brotli (.br) and Gzip (.gz) siblings for the configured
    /// static assets. Cross-platform — no external brotli/gzip binaries required.
    /// </summary>
    public sealed class CompressAssets : Task
    {
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

            int compressedCount = 0;
            long savedBytes = 0;

            foreach (var file in Directory.EnumerateFiles(RootDirectory, "*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(file);

                if (!extensions.Contains(ext))
                    continue;

                if (file.EndsWith(".br", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                    continue;

                var info = new FileInfo(file);

                if (info.Length < MinBytes)
                    continue;

                if (WriteBrotli)
                {
                    var brPath = file + ".br";

                    if (IsStale(brPath, info))
                    {
                        CompressFile(file, brPath, stream => new BrotliStream(stream, CompressionLevel.SmallestSize));

                        savedBytes += Math.Max(0, info.Length - new FileInfo(brPath).Length);

                        compressedCount++;
                    }
                }

                if (WriteGzip)
                {
                    var gzPath = file + ".gz";

                    if (IsStale(gzPath, info))
                        CompressFile(file, gzPath, stream => new GZipStream(stream, CompressionLevel.SmallestSize));
                }
            }

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
        private static void CompressFile(string source, string destination, Func<Stream, Stream> wrap)
        {
            using var input = File.OpenRead(source);
            using var output = File.Create(destination);
            using var compressor = wrap(output);

            input.CopyTo(compressor);
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
