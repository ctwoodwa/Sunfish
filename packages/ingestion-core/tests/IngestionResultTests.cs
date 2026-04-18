using Sunfish.Ingestion.Core;
using Xunit;

namespace Sunfish.Ingestion.Core.Tests;

public class IngestionResultTests
{
    private static IngestedEntity StubEntity() =>
        new("e1", "schema/v1",
            new Dictionary<string, object?>(),
            Array.Empty<IngestedEvent>(),
            Array.Empty<Sunfish.Foundation.Blobs.Cid>());

    [Fact]
    public void Success_Factory_SetsOutcomeAndValue_NoFailure()
    {
        var entity = StubEntity();

        var r = IngestionResult<IngestedEntity>.Success(entity);

        Assert.Equal(IngestOutcome.Success, r.Outcome);
        Assert.Same(entity, r.Value);
        Assert.Null(r.Failure);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void Fail_Factory_SetsOutcomeAndFailure_NoValue()
    {
        var r = IngestionResult<IngestedEntity>.Fail(IngestOutcome.ValidationFailed, "bad input");

        Assert.Equal(IngestOutcome.ValidationFailed, r.Outcome);
        Assert.Null(r.Value);
        Assert.NotNull(r.Failure);
        Assert.Equal(IngestOutcome.ValidationFailed, r.Failure!.Outcome);
        Assert.Equal("bad input", r.Failure.Message);
        Assert.Empty(r.Failure.Details);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void IsSuccess_IsTrue_OnlyForSuccessOutcome()
    {
        foreach (IngestOutcome outcome in Enum.GetValues(typeof(IngestOutcome)))
        {
            var r = outcome == IngestOutcome.Success
                ? IngestionResult<IngestedEntity>.Success(StubEntity())
                : IngestionResult<IngestedEntity>.Fail(outcome, "msg");

            Assert.Equal(outcome == IngestOutcome.Success, r.IsSuccess);
        }
    }

    [Fact]
    public void Fail_WithDetails_PropagatesDetailsThroughFailure()
    {
        var details = new[] { "field A is required", "field B must be positive" };

        var r = IngestionResult<IngestedEntity>.Fail(
            IngestOutcome.ValidationFailed, "validation failed", details);

        Assert.NotNull(r.Failure);
        Assert.Equal(details, r.Failure!.Details);
    }
}
