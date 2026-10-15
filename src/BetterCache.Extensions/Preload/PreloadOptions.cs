namespace BetterCache;

/// <summary>Options for the preload links rendered by <see cref="BetterCacheHead"/>.</summary>
public sealed class PreloadOptions
{
    /// <summary>
    /// Boot manifest sections preloaded when <see cref="WebAssemblySections"/> is empty: the runtime modules,
    /// the native runtime and every eagerly loaded assembly. ICU shards are left out because the runtime
    /// picks one from the browser culture; lazy, satellite and PDB files are not needed at startup.
    /// </summary>
    public static IReadOnlyList<string> DefaultWebAssemblySections { get; } = ["jsModuleRuntime", "jsModuleNative", "wasmNative", "coreAssembly", "assembly"];

    /// <summary>Render preload links. Default <c>true</c>.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Extra links rendered on every page, for example your own fingerprinted ES modules.</summary>
    public IList<PreloadLink> Links { get; set; } = [];

    /// <summary>Preload the WebAssembly runtime and assemblies listed in the boot manifest. Default <c>true</c>.</summary>
    public bool PreloadWebAssembly { get; set; } = true;

    /// <summary>Boot manifest <c>resources</c> sections to preload. Empty means <see cref="DefaultWebAssemblySections"/>.</summary>
    public IList<string> WebAssemblySections { get; set; } = [];

    internal IReadOnlyList<string> EffectiveWebAssemblySections => WebAssemblySections.Count > 0 ? [.. WebAssemblySections] : DefaultWebAssemblySections;
}
