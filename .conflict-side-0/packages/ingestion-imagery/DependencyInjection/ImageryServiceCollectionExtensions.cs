using Microsoft.Extensions.DependencyInjection;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Core.DependencyInjection;
using Sunfish.Ingestion.Imagery.Metadata;

namespace Sunfish.Ingestion.Imagery.DependencyInjection;

/// <summary>
/// DI extensions that register the imagery modality onto an <see cref="IngestionBuilder"/>.
/// </summary>
public static class ImageryServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ExifImageryMetadataExtractor"/> and <see cref="ImageryIngestionPipeline"/>
    /// onto the supplied <paramref name="builder"/>, and returns the builder for chaining.
    /// </summary>
    public static IngestionBuilder WithImagery(this IngestionBuilder builder)
    {
        builder.Services.AddSingleton<IImageryMetadataExtractor, ExifImageryMetadataExtractor>();
        builder.Services.AddSingleton<IIngestionPipeline<ImageUpload>, ImageryIngestionPipeline>();
        return builder;
    }
}
