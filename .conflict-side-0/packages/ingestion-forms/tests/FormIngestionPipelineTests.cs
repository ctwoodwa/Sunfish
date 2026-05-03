using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Forms;
using Xunit;

namespace Sunfish.Ingestion.Forms.Tests;

public class FormIngestionPipelineTests
{
    private sealed class WorkOrderModel
    {
        public string Title { get; set; } = "";
        public string Priority { get; set; } = "";
        public int? RequestedBedrooms { get; set; }
    }

    private static IngestionContext Ctx() => IngestionContext.NewCorrelation("tenant-1", "actor-1");

    [Fact]
    public async Task IngestAsync_ReturnsSuccess_WithEntityBodyMatchingModelProperties()
    {
        var submission = new FormSubmission<WorkOrderModel>(
            Model: new WorkOrderModel { Title = "Leak in unit 42", Priority = "high", RequestedBedrooms = 2 },
            SchemaId: "sunfish.pm.workorder/1",
            SubmittedAtUtc: DateTime.UtcNow);

        var pipeline = new FormIngestionPipeline<WorkOrderModel>();
        var result = await pipeline.IngestAsync(submission, Ctx());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Contains("Title", result.Value!.Body.Keys);
        Assert.Contains("Priority", result.Value.Body.Keys);
    }

    [Fact]
    public async Task IngestAsync_EmitsEntityCreatedEvent()
    {
        var submission = new FormSubmission<WorkOrderModel>(
            new WorkOrderModel { Title = "Annual inspection" },
            "sunfish.pm.workorder/1",
            DateTime.UtcNow);

        var pipeline = new FormIngestionPipeline<WorkOrderModel>();
        var result = await pipeline.IngestAsync(submission, Ctx());

        Assert.Single(result.Value!.Events);
        Assert.Equal("entity.created", result.Value.Events[0].Kind);
    }

    [Fact]
    public async Task IngestAsync_BlobCidsIsEmpty_FormDataIsInline()
    {
        var submission = new FormSubmission<WorkOrderModel>(
            new WorkOrderModel(), "sunfish.pm.workorder/1", DateTime.UtcNow);

        var pipeline = new FormIngestionPipeline<WorkOrderModel>();
        var result = await pipeline.IngestAsync(submission, Ctx());

        Assert.Empty(result.Value!.BlobCids);
    }

    [Fact]
    public async Task IngestAsync_SchemaIdPropagatesToEntity()
    {
        var submission = new FormSubmission<WorkOrderModel>(
            new WorkOrderModel(), "sunfish.pm.inspection/3", DateTime.UtcNow);

        var pipeline = new FormIngestionPipeline<WorkOrderModel>();
        var result = await pipeline.IngestAsync(submission, Ctx());

        Assert.Equal("sunfish.pm.inspection/3", result.Value!.SchemaId);
    }
}
