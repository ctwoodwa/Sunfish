using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Kernel.Buckets.LazyFetch;
using Sunfish.Kernel.Buckets.Storage;

namespace Sunfish.Kernel.Buckets.DependencyInjection;

/// <summary>
/// DI extensions for registering the Sunfish kernel-buckets surface (paper §10).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IBucketRegistry"/>, <see cref="IBucketYamlLoader"/>,
    /// <see cref="IBucketFilterEvaluator"/>, <see cref="IBucketStubStore"/>, and
    /// <see cref="IStorageBudgetManager"/> as singletons.
    /// </summary>
    /// <remarks>
    /// Uses <c>TryAddSingleton</c> so test doubles registered earlier win. The call is
    /// idempotent: repeated invocations do not stack registrations.
    /// </remarks>
    public static IServiceCollection AddSunfishKernelBuckets(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IBucketRegistry, BucketRegistry>();
        services.TryAddSingleton<IBucketYamlLoader, BucketYamlLoader>();
        services.TryAddSingleton<IBucketFilterEvaluator, SimpleBucketFilterEvaluator>();
        services.TryAddSingleton<IBucketStubStore, InMemoryBucketStubStore>();
        services.TryAddSingleton<IStorageBudgetManager, InMemoryStorageBudgetManager>();

        return services;
    }
}
