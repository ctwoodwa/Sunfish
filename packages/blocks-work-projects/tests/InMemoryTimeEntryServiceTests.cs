using System.Collections.Concurrent;
using Sunfish.Blocks.WorkProjects.Events;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="InMemoryTimeEntryService"/> +
/// <see cref="InMemoryTimeApprovalService"/> event-emission contract.
/// </summary>
public sealed class InMemoryTimeEntryServiceTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Worker = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();

    private sealed class RecordingPublisher : IDomainEventPublisher
    {
        public ConcurrentQueue<(string EventType, string IdempotencyKey)> Published { get; } = new();

        public Task PublishAsync<T>(DomainEventEnvelope<T> envelope, CancellationToken cancellationToken = default)
        {
            Published.Enqueue((envelope.EventType, envelope.IdempotencyKey));
            return Task.CompletedTask;
        }
    }

    private static (InMemoryTimeEntryRepository Repo, InMemoryTimeEntryService Svc, InMemoryTimeApprovalService Approve, RecordingPublisher Pub) Build()
    {
        var repo = new InMemoryTimeEntryRepository();
        var pub  = new RecordingPublisher();
        return (repo, new InMemoryTimeEntryService(repo, pub), new InMemoryTimeApprovalService(repo, pub), pub);
    }

    [Fact]
    public async Task SubmitAsync_EmitsTimeEntrySubmitted()
    {
        var (repo, svc, _, pub) = Build();
        var entry = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor,
            new Instant(new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero)),
            Worker, projectId: ProjectId.NewId());
        await svc.StopAsync(entry.Id, new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            hourlyRate: 100m, rateCurrency: "USD", updatedBy: Worker);
        await svc.SubmitAsync(entry.Id, Instant.Now, Worker);

        Assert.Single(pub.Published);
        var (eventType, key) = pub.Published.First();
        Assert.Equal("Work.TimeEntrySubmitted", eventType);
        Assert.StartsWith("time-entry-submitted:", key);
    }

    [Fact]
    public async Task ApproveAsync_EmitsTimeEntryApproved_OneShot()
    {
        var (_, svc, approve, pub) = Build();
        var entry = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor,
            new Instant(new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero)),
            Worker, projectId: ProjectId.NewId());
        await svc.StopAsync(entry.Id, new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            hourlyRate: 100m, rateCurrency: "USD", updatedBy: Worker);
        await svc.SubmitAsync(entry.Id, Instant.Now, Worker);
        await approve.ApproveAsync(entry.Id, Approver, Instant.Now);

        Assert.Equal(2, pub.Published.Count);
        Assert.Contains(pub.Published, e => e.EventType == "Work.TimeEntryApproved");
    }

    [Fact]
    public async Task GetByIdAsync_CrossTenant_ReturnsNull()
    {
        var (_, svc, _, _) = Build();
        var entry = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor, Instant.Now, Worker,
            projectId: ProjectId.NewId());
        var other = new TenantId("other-tenant");
        Assert.Null(await svc.GetByIdAsync(other, entry.Id));
    }

    [Fact]
    public async Task RejectAsync_DoesNotEmitEvent()
    {
        var (_, svc, approve, pub) = Build();
        var entry = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor,
            new Instant(new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero)),
            Worker, projectId: ProjectId.NewId());
        await svc.StopAsync(entry.Id, new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            null, null, Worker);
        await svc.SubmitAsync(entry.Id, Instant.Now, Worker);
        await approve.RejectAsync(entry.Id, Approver, Instant.Now, "incomplete description");

        Assert.Single(pub.Published);  // submit only — reject is a private signal
        Assert.Equal("Work.TimeEntrySubmitted", pub.Published.First().EventType);
    }

    [Fact]
    public async Task SubmitAsync_RunningEntry_Throws()
    {
        var (_, svc, _, _) = Build();
        var entry = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor, Instant.Now, Worker,
            projectId: ProjectId.NewId());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SubmitAsync(entry.Id, Instant.Now, Worker));
    }
}
