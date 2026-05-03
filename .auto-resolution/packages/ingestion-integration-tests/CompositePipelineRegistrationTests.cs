using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Blobs;
using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Core.DependencyInjection;
using Sunfish.Ingestion.Forms;
using Sunfish.Ingestion.Forms.DependencyInjection;
using Sunfish.Ingestion.Imagery;
using Sunfish.Ingestion.Imagery.DependencyInjection;
using Sunfish.Ingestion.Satellite;
using Sunfish.Ingestion.Satellite.DependencyInjection;
using Sunfish.Ingestion.Sensors;
using Sunfish.Ingestion.Sensors.DependencyInjection;
using Sunfish.Ingestion.Spreadsheets;
using Sunfish.Ingestion.Spreadsheets.DependencyInjection;
using Sunfish.Ingestion.Voice;
using Sunfish.Ingestion.Voice.DependencyInjection;
using Sunfish.Ingestion.Voice.Transcribers;
using Xunit;

namespace Sunfish.Ingestion.IntegrationTests;

public class CompositePipelineRegistrationTests
{
    private sealed class SampleFormModel { public string Title { get; set; } = ""; }

    private static ServiceCollection NewServicesWithBlobs()
    {
        var services = new ServiceCollection();
        // IBlobStore is required by voice/sensors/imagery/satellite pipelines.
        var tempRoot = Path.Combine(Path.GetTempPath(), "sunfish-ingestion-composite-" + Path.GetRandomFileName());
        services.AddSingleton<IBlobStore>(new FileSystemBlobStore(tempRoot));
        // HttpClient is required by voice transcribers (even when kind=NoOp the factory resolves it when needed).
        services.AddSingleton(new HttpClient());
        return services;
    }

    [Fact]
    public void AllSixModalitiesResolve_WhenEveryWithXIsCalled()
    {
        var services = NewServicesWithBlobs();

        services.AddSunfishIngestion()
            .WithForms()
            .WithFormModel<SampleFormModel>()
            .WithSpreadsheets()
            .WithVoice(o => { o.Kind = TranscriberKind.NoOp; o.NoOpTranscript = ""; })
            .WithSensors()
            .WithImagery()
            .WithSatellite();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IIngestionPipeline<FormSubmission<SampleFormModel>>>());
        Assert.NotNull(provider.GetService<IIngestionPipeline<SpreadsheetUpload>>());
        Assert.NotNull(provider.GetService<IIngestionPipeline<AudioBlob>>());
        Assert.NotNull(provider.GetService<IIngestionPipeline<SensorBatch>>());
        Assert.NotNull(provider.GetService<IIngestionPipeline<ImageUpload>>());
        Assert.NotNull(provider.GetService<IIngestionPipeline<SatelliteAcquisition>>());
    }

    [Fact]
    public void DoubleRegistration_IsIdempotent()
    {
        var services = NewServicesWithBlobs();

        services.AddSunfishIngestion()
            .WithForms().WithForms().WithFormModel<SampleFormModel>().WithFormModel<SampleFormModel>()
            .WithSpreadsheets().WithSpreadsheets()
            .WithSensors().WithSensors()
            .WithImagery().WithImagery()
            .WithSatellite().WithSatellite();

        using var provider = services.BuildServiceProvider();

        // Resolution still works; no InvalidOperationException from ambiguous registrations.
        Assert.NotNull(provider.GetService<IIngestionPipeline<FormSubmission<SampleFormModel>>>());
        Assert.NotNull(provider.GetService<IIngestionPipeline<SpreadsheetUpload>>());
        Assert.NotNull(provider.GetService<IIngestionPipeline<SensorBatch>>());
        Assert.NotNull(provider.GetService<IIngestionPipeline<ImageUpload>>());
        Assert.NotNull(provider.GetService<IIngestionPipeline<SatelliteAcquisition>>());
    }

    [Fact]
    public void PerModalityToggleIsIndependent_OnlyFormsRegistered_OnlyFormsResolves()
    {
        var services = NewServicesWithBlobs();

        services.AddSunfishIngestion().WithForms().WithFormModel<SampleFormModel>();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IIngestionPipeline<FormSubmission<SampleFormModel>>>());
        Assert.Null(provider.GetService<IIngestionPipeline<SpreadsheetUpload>>());
        Assert.Null(provider.GetService<IIngestionPipeline<AudioBlob>>());
        Assert.Null(provider.GetService<IIngestionPipeline<SensorBatch>>());
        Assert.Null(provider.GetService<IIngestionPipeline<ImageUpload>>());
        Assert.Null(provider.GetService<IIngestionPipeline<SatelliteAcquisition>>());
    }
}
