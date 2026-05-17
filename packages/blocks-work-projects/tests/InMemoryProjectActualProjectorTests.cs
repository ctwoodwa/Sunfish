using Sunfish.Blocks.WorkProjects.Events;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>W#60 P4 — coverage for <see cref="InMemoryProjectActualProjector"/>.</summary>
public sealed class InMemoryProjectActualProjectorTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");

    [Fact]
    public async Task RebuildFromCursor_ReplaysAllEventsFromCursor_IdempotentResult()
    {
        var repo = new InMemoryProjectActualRepository();
        var handler = new JournalEntryPostedHandler(repo);
        var projector = new InMemoryProjectActualProjector(handler);

        var pid = ProjectId.NewId();
        var envelopes = new[]
        {
            Envelope(Guid.NewGuid(), "Invoice", pid, 100m),
            Envelope(Guid.NewGuid(), "TimeEntry", pid, 50m),
            Envelope(Guid.NewGuid(), "Manual", pid, 25m),
        };

        await projector.RebuildFromCursorAsync(envelopes);
        // Re-running the same set must produce no new rows (per-event idempotency).
        await projector.RebuildFromCursorAsync(envelopes);

        var rows = await repo.GetByProjectAsync(Tenant, pid);
        Assert.Equal(3, rows.Count);
        Assert.Equal(175m, rows.Sum(r => r.PostedAmount));
    }

    [Fact]
    public async Task RebuildFromCursor_RespectsCancellation()
    {
        var repo = new InMemoryProjectActualRepository();
        var projector = new InMemoryProjectActualProjector(new JournalEntryPostedHandler(repo));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            projector.RebuildFromCursorAsync(new[] { Envelope(Guid.NewGuid(), "Manual", ProjectId.NewId(), 1m) }, cts.Token));
    }

    private static DomainEventEnvelope<JournalEntryPostedPayload> Envelope(
        Guid entryId, string sourceKind, ProjectId pid, decimal debit)
        => new()
        {
            EventId              = EventId.New(),
            EventType            = "Financial.JournalEntryPosted",
            SchemaVersion        = 1,
            OccurredAt           = DateTimeOffset.UtcNow,
            TenantId             = Tenant,
            OriginatingReplicaId = ReplicaId.System,
            IdempotencyKey       = $"je-posted:{entryId}",
            Payload              = new JournalEntryPostedPayload(
                EntryId:   entryId,
                EntryDate: new DateOnly(2026, 5, 16),
                SourceKind: sourceKind,
                Lines: new[]
                {
                    new JournalEntryPostedLine(
                        AccountId: Guid.NewGuid(),
                        Debit:     debit,
                        Credit:    0m,
                        Currency:  "USD",
                        Dimensions: new Dictionary<string, string> { ["projectId"] = pid.Value.ToString() }),
                }),
        };
}
