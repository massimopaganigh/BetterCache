using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BetterCache;

/// <summary>Registers BetterCache services.</summary>
public static class BetterCacheServiceCollectionExtensions
{
    /// <summary>
    /// Registers BetterCache. Options are bound from the <c>BetterCache</c> configuration section, then
    /// <paramref name="configure"/> runs, then they are validated at startup. Every service is added with
    /// <c>TryAdd</c>, so registering your own <see cref="ICacheControlPolicy"/> or
    /// <see cref="IContentEncodingNegotiator"/> first replaces the default.
    /// </summary>
    public static IServiceCollection AddBetterCache(this IServiceCollection services, Action<BetterCacheOptions>? configure = null)
    {
        var options = services.AddOptions<BetterCacheOptions>().BindConfiguration(BetterCacheOptions.SectionName).ValidateOnStart();

        if (configure is not null)
            options.Configure(configure);

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BetterCacheOptions>, BetterCacheOptionsValidator>());
        services.TryAddSingleton<ICacheControlPolicy, CacheControlPolicy>();
        services.TryAddSingleton<IContentEncodingNegotiator, AcceptEncodingNegotiator>();
        services.TryAddSingleton<BootManifestProvider>();
        services.TryAddSingleton<BlazorWebScriptAsset>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPreloadLinkSource, InitializerPreloadLinkSource>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPreloadLinkSource, WebAssemblyPreloadLinkSource>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPreloadLinkSource, ConfiguredPreloadLinkSource>());

        return services;
    }
}
