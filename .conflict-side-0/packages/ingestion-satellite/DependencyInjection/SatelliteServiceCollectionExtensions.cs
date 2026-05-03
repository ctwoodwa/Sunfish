using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Core.DependencyInjection;
using Sunfish.Ingestion.Satellite.Providers;

namespace Sunfish.Ingestion.Satellite.DependencyInjection;

/// <summary>
/// DI extensions that register the satellite modality onto an <see cref="IngestionBuilder"/>.
/// </summary>
public static class SatelliteServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SatelliteIngestionPipeline"/> and a fallback
    /// <see cref="NoOpSatelliteImageryProvider"/>. The provider uses <c>TryAddSingleton</c>, so
    /// consumers that pre-register a real <see cref="ISatelliteImageryProvider"/> keep their
    /// registration intact.
    /// </summary>
    public static IngestionBuilder WithSatellite(this IngestionBuilder builder)
    {
        builder.Services.TryAddSingleton<ISatelliteImageryProvider, NoOpSatelliteImageryProvider>();
        builder.Services.AddSingleton<IIngestionPipeline<SatelliteAcquisition>, SatelliteIngestionPipeline>();
        return builder;
    }
}
