using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Ingestion.Core.DependencyInjection;

/// <summary>
/// Entry-point extensions for registering Sunfish ingestion services in a DI container.
/// Modality packages (forms, spreadsheets, voice, sensors, imagery, satellite) add their
/// registrations onto the returned <see cref="IngestionBuilder"/> via their own extension methods.
/// </summary>
public static class SunfishIngestionServiceCollectionExtensions
{
    /// <summary>
    /// Begins registration of Sunfish ingestion services against the given
    /// <paramref name="services"/> and returns an <see cref="IngestionBuilder"/> that modality
    /// packages can extend.
    /// </summary>
    public static IngestionBuilder AddSunfishIngestion(this IServiceCollection services)
        => new IngestionBuilder(services);
}

/// <summary>
/// Fluent builder returned by <see cref="SunfishIngestionServiceCollectionExtensions.AddSunfishIngestion"/>.
/// Exposes the underlying <see cref="IServiceCollection"/> so modality packages can register
/// their own pipelines, middlewares, and handlers without re-implementing the seam.
/// </summary>
public sealed class IngestionBuilder(IServiceCollection services)
{
    /// <summary>The underlying <see cref="IServiceCollection"/>.</summary>
    public IServiceCollection Services { get; } = services;
}
