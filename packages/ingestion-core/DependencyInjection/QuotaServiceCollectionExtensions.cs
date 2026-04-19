using Microsoft.Extensions.DependencyInjection;
using Sunfish.Ingestion.Core.Quota;

namespace Sunfish.Ingestion.Core.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering the in-memory ingestion quota
/// middleware and its backing store.
/// </summary>
public static class QuotaServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="InMemoryIngestionQuotaStore"/> as the singleton
    /// <see cref="IIngestionQuotaStore"/> using the supplied <paramref name="policy"/> for all
    /// tenants.
    /// </summary>
    /// <param name="builder">The <see cref="IngestionBuilder"/> to extend.</param>
    /// <param name="policy">The token-bucket policy applied globally to every tenant.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    /// <example>
    /// <code>
    /// services
    ///     .AddSunfishIngestion()
    ///     .AddInMemoryQuotaMiddleware(new QuotaPolicy(
    ///         Capacity: 100,
    ///         RefillTokens: 10,
    ///         RefillInterval: TimeSpan.FromSeconds(1)));
    /// </code>
    /// </example>
    public static IngestionBuilder AddInMemoryQuotaMiddleware(
        this IngestionBuilder builder,
        QuotaPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(policy);

        builder.Services.AddSingleton<IIngestionQuotaStore>(sp =>
            new InMemoryIngestionQuotaStore(
                policy,
                sp.GetService<TimeProvider>()));

        return builder;
    }

    /// <summary>
    /// Registers <see cref="InMemoryIngestionQuotaStore"/> as the singleton
    /// <see cref="IIngestionQuotaStore"/> using a per-tenant policy resolver. Tenants not covered
    /// by the resolver fall back to <paramref name="defaultPolicy"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IngestionBuilder"/> to extend.</param>
    /// <param name="defaultPolicy">Fallback policy for tenants the resolver does not cover.</param>
    /// <param name="policyResolver">
    /// Returns a <see cref="QuotaPolicy"/> for the given tenant id, or <c>null</c> to use
    /// <paramref name="defaultPolicy"/>.
    /// </param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static IngestionBuilder AddInMemoryQuotaMiddleware(
        this IngestionBuilder builder,
        QuotaPolicy defaultPolicy,
        Func<string, QuotaPolicy?> policyResolver)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(defaultPolicy);
        ArgumentNullException.ThrowIfNull(policyResolver);

        builder.Services.AddSingleton<IIngestionQuotaStore>(sp =>
            new InMemoryIngestionQuotaStore(
                defaultPolicy,
                policyResolver,
                sp.GetService<TimeProvider>()));

        return builder;
    }
}
