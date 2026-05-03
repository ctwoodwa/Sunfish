using Sunfish.Foundation.Blobs;
using Sunfish.Kernel.Schema;

namespace Sunfish.Kernel.SchemaRegistry.Tests;

/// <summary>
/// Asserts that the migration half of <see cref="ISchemaRegistry"/> throws
/// <see cref="NotSupportedException"/> with the exact G2-follow-up reference
/// message. These tests guard the "Option B" scope boundary of the
/// validation-only PR: if someone lands a jsonata implementation without
/// updating the exception message, these tests prompt them to also delete
/// the stub tests.
/// </summary>
public class MigrationStubTests
{
    private const string ExpectedMessage =
        "Migration half of ISchemaRegistry is deferred — see gap analysis G2 follow-up.";

    private static InMemorySchemaRegistry NewRegistry()
        => new(new NoopBlobStore());

    [Fact]
    public async Task PlanMigrationAsync_ThrowsNotSupportedException_WithG2FollowUpMessage()
    {
        var registry = NewRegistry();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            async () => await registry.PlanMigrationAsync(new("schema:a"), new("schema:b")));

        Assert.Equal(ExpectedMessage, ex.Message);
    }

    [Fact]
    public async Task MigrateAsync_ThrowsNotSupportedException_WithG2FollowUpMessage()
    {
        var registry = NewRegistry();

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            async () => await registry.MigrateAsync(new("schema:a"), new("schema:b"), ReadOnlyMemory<byte>.Empty));

        Assert.Equal(ExpectedMessage, ex.Message);
    }

    private sealed class NoopBlobStore : IBlobStore
    {
        public ValueTask<Cid> PutAsync(ReadOnlyMemory<byte> content, CancellationToken ct = default)
            => new(Cid.FromBytes(content.Span));
        public ValueTask<ReadOnlyMemory<byte>?> GetAsync(Cid cid, CancellationToken ct = default)
            => new((ReadOnlyMemory<byte>?)null);
        public ValueTask<bool> ExistsLocallyAsync(Cid cid, CancellationToken ct = default) => new(false);
        public ValueTask PinAsync(Cid cid, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask UnpinAsync(Cid cid, CancellationToken ct = default) => ValueTask.CompletedTask;
    }
}
