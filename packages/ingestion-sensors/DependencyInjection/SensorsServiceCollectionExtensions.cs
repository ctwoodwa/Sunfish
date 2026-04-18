using Microsoft.Extensions.DependencyInjection;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Core.DependencyInjection;

namespace Sunfish.Ingestion.Sensors.DependencyInjection;

/// <summary>
/// DI extensions for registering the sensor ingestion pipeline.
/// </summary>
public static class SensorsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="SensorIngestionPipeline"/> on the given <paramref name="builder"/>.
    /// </summary>
    public static IngestionBuilder WithSensors(this IngestionBuilder builder)
    {
        builder.Services.AddSingleton<IIngestionPipeline<SensorBatch>, SensorIngestionPipeline>();
        return builder;
    }
}
