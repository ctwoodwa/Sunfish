using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Kernel.Buckets.LazyFetch;
using Sunfish.Kernel.Buckets.Storage;

namespace Sunfish.Kernel.Buckets.DependencyInjection;

/// <summary>
/// Options for <see cref="ServiceCollectionExtensions.AddSunfishKernelBuckets(IServiceCollection, Action{BucketLoaderOptions})"/>.
/// Controls how the <see cref="IBucketRegistry"/> is eagerly populated at DI-resolution time.
/// </summary>
/// <remarks>
/// <para>
/// The default loader (<see cref="BucketYamlLoader"/>) is filesystem-agnostic — it only parses
/// strings. Manifest <em>discovery</em> (which YAML files to load, from where) is a caller concern.
/// Wave 6.3.D introduced this options type so the per-team registrar can point each team's
/// <see cref="IBucketRegistry"/> at a team-scoped manifest directory
/// (<c>{DataDirectory}/teams/{team_id}/buckets/</c>) without touching the loader itself.
/// </para>
/// <para>
/// When <see cref="SourceDirectory"/> is <c>null</c> or empty, <see cref="IBucketRegistry"/> is
/// registered as an empty <see cref="BucketRegistry"/> — callers may register definitions
/// imperatively. When <see cref="SourceDirectory"/> is set, every <c>*.yaml</c> and <c>*.yml</c>
/// file directly inside that directory is parsed at first resolution, and each parsed
/// <see cref="BucketDefinition"/> is registered. A missing directory is treated the same as an
/// empty one — no crash, empty registry.
/// </para>
/// </remarks>
public sealed class BucketLoaderOptions
{
    /// <summary>
    /// Filesystem directory containing bucket manifest YAML files. When set and non-empty,
    /// <see cref="ServiceCollectionExtensions.AddSunfishKernelBuckets(IServiceCollection, Action{BucketLoaderOptions})"/>
    /// registers an <see cref="IBucketRegistry"/> factory that eagerly populates the registry
    /// from every <c>*.yaml</c> / <c>*.yml</c> file directly inside this directory (non-recursive).
    /// </summary>
    /// <remarks>
    /// Per Wave 6.3.D convention, the per-team registrar sets this to
    /// <c>TeamPaths.BucketsDirectory(dataDirectory, teamId)</c>. The convention is "proposed,
    /// subject to block-developer-experience review" (see <c>TeamPaths.BucketsDirectory</c>
    /// remarks) — callers that need a different layout may supply any absolute path here.
    /// </remarks>
    public string? SourceDirectory { get; set; }
}

/// <summary>
/// DI extensions for registering the Sunfish kernel-buckets surface (paper §10).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IBucketRegistry"/>, <see cref="IBucketYamlLoader"/>,
    /// <see cref="IBucketFilterEvaluator"/>, <see cref="IBucketStubStore"/>, and
    /// <see cref="IStorageBudgetManager"/> as singletons, with no manifest pre-load.
    /// The registry starts empty; callers that want file-backed manifests should use the
    /// <see cref="AddSunfishKernelBuckets(IServiceCollection, Action{BucketLoaderOptions})"/>
    /// overload and set <see cref="BucketLoaderOptions.SourceDirectory"/>.
    /// </summary>
    /// <remarks>
    /// Uses <c>TryAddSingleton</c> so test doubles registered earlier win. The call is
    /// idempotent: repeated invocations do not stack registrations.
    /// </remarks>
    public static IServiceCollection AddSunfishKernelBuckets(this IServiceCollection services)
        => AddSunfishKernelBuckets(services, configure: null);

    /// <summary>
    /// Registers the kernel-buckets surface as singletons and, when
    /// <see cref="BucketLoaderOptions.SourceDirectory"/> is set, eagerly populates
    /// <see cref="IBucketRegistry"/> from every <c>*.yaml</c> / <c>*.yml</c> file in that
    /// directory at first resolution.
    /// </summary>
    /// <param name="services">DI collection.</param>
    /// <param name="configure">Optional callback that mutates the <see cref="BucketLoaderOptions"/>
    /// captured by the factory. Pass <c>null</c> (or use the parameterless overload) for the
    /// classic empty-registry behaviour.</param>
    /// <remarks>
    /// <para>
    /// Uses <c>TryAddSingleton</c> so test doubles registered earlier win. Manifest loading is
    /// <em>eager at first resolution</em>, not at <c>AddSunfishKernelBuckets</c> call time — so
    /// the DI container's service-provider lifetime controls when I/O happens. A missing or
    /// empty directory is silent (empty registry, no exception). Parsing errors in any one file
    /// bubble up as <see cref="BucketYamlException"/> wrapped by the DI factory — failing fast
    /// is intentional because a malformed manifest is a deployment bug, not a runtime condition.
    /// </para>
    /// <para>
    /// Enumeration is non-recursive (<see cref="SearchOption.TopDirectoryOnly"/>). Files are
    /// processed in invariant-culture ordinal order of their full path so tests can assert
    /// deterministic registration order when it matters.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddSunfishKernelBuckets(
        this IServiceCollection services,
        Action<BucketLoaderOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new BucketLoaderOptions();
        configure?.Invoke(options);

        services.TryAddSingleton<IBucketYamlLoader, BucketYamlLoader>();
        services.TryAddSingleton<IBucketFilterEvaluator, SimpleBucketFilterEvaluator>();
        services.TryAddSingleton<IBucketStubStore, InMemoryBucketStubStore>();
        services.TryAddSingleton<IStorageBudgetManager, InMemoryStorageBudgetManager>();

        if (string.IsNullOrEmpty(options.SourceDirectory))
        {
            services.TryAddSingleton<IBucketRegistry, BucketRegistry>();
        }
        else
        {
            var sourceDirectory = options.SourceDirectory;
            services.TryAddSingleton<IBucketRegistry>(sp =>
            {
                var loader = sp.GetRequiredService<IBucketYamlLoader>();
                var registry = new BucketRegistry();
                PopulateFromDirectory(registry, loader, sourceDirectory);
                return registry;
            });
        }

        return services;
    }

    private static void PopulateFromDirectory(
        IBucketRegistry registry,
        IBucketYamlLoader loader,
        string sourceDirectory)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            // Empty/missing directory is silent by design (e.g. a new team with no manifests).
            return;
        }

        var yamlFiles = Directory
            .EnumerateFiles(sourceDirectory, "*.yaml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(sourceDirectory, "*.yml", SearchOption.TopDirectoryOnly))
            .OrderBy(p => p, StringComparer.Ordinal);

        foreach (var path in yamlFiles)
        {
            var defs = loader.LoadFromFile(path);
            for (var i = 0; i < defs.Count; i++)
            {
                registry.Register(defs[i]);
            }
        }
    }
}
