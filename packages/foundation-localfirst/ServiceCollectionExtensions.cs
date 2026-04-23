using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.LocalFirst.Encryption;

namespace Sunfish.Foundation.LocalFirst;

/// <summary>DI conveniences for <see cref="Sunfish.Foundation.LocalFirst"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory <see cref="IOfflineStore"/>, <see cref="IOfflineQueue"/>,
    /// and the default <see cref="LastWriterWinsConflictResolver"/>. Sync engine,
    /// export, and import services are bundle / accelerator concerns and are
    /// not registered here.
    /// </summary>
    public static IServiceCollection AddSunfishLocalFirst(this IServiceCollection services)
    {
        services.AddSingleton<IOfflineStore, InMemoryOfflineStore>();
        services.AddSingleton<IOfflineQueue, InMemoryOfflineQueue>();
        services.AddSingleton<ISyncConflictResolver, LastWriterWinsConflictResolver>();
        return services;
    }

    /// <summary>
    /// Registers the paper §11.2 Layer 1 encrypted local store stack:
    /// <see cref="IEncryptedStore"/> (SQLCipher), <see cref="IKeyDerivation"/>
    /// (Argon2id), and the platform-appropriate <see cref="IKeystore"/>
    /// (Windows DPAPI; macOS/Linux stubs). All are singletons.
    /// </summary>
    /// <remarks>
    /// The encrypted store itself is returned <em>unopened</em>. Applications
    /// are responsible for calling <see cref="IEncryptedStore.OpenAsync"/> with
    /// the derived key once during startup (typically after the user has
    /// authenticated and the key is available from the keystore).
    /// </remarks>
    public static IServiceCollection AddSunfishEncryptedStore(
        this IServiceCollection services,
        Action<EncryptionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            // Ensure IOptions<EncryptionOptions> resolves even when no configure callback was given.
            services.AddOptions<EncryptionOptions>();
        }

        services.AddSingleton<IKeyDerivation>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<EncryptionOptions>>().Value;
            return new Argon2idKeyDerivation(options.Argon2Options);
        });

        services.AddSingleton<IKeystore>(_ => Keystore.CreateForCurrentPlatform());
        services.AddSingleton<IEncryptedStore>(_ => new SqlCipherEncryptedStore());

        return services;
    }
}
