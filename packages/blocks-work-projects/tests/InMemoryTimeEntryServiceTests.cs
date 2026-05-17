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
/// <see cref="InMemoryTimeApprovalService"/> event-emission +
/// H5 tenant-isolation contract.
/// </summary>
public sealed class InMemoryTimeEntryServiceTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly TenantId OtherTenant = new("test-tenant-2");
    private static readonly Guid Worker = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();

    private sealed class RecordingPublisher : IDomainEventPublisher
    {
        public ConcurrentQueue<(string EventType, string IdempotencyKey, TenantId TenantId)> Published { get; } = new();

        public Task PublishAsync<T>(DomainEventEnvelope<T> envelope, CancellationToken cancellationToken = default)
        {
            Published.Enqueue((envelope.EventType, envelope.IdempotencyKey, envelope.TenantId));
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
    public async Task SubmitAsync_EmitsTimeEntrySubmitted_EnvelopeTenantMatchesEntry()
    {
        var (_, svc, _, pub) = Build();
        var entry = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor,
            new Instant(new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero)),
            Worker, projectId: ProjectId.NewId());
        await svc.StopAsync(Tenant, entry.Id, new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            hourlyRate: 100m, rateCurrency: "USD", updatedBy: Worker);
        await svc.SubmitAsync(Tenant, entry.Id, Instant.Now, Worker);

        Assert.Single(pub.Published);
        var (eventType, key, envTenant) = pub.Published.First();
        Assert.Equal("Work.TimeEntrySubmitted", eventType);
        Assert.StartsWith("time-entry-submitted:", key);
        Assert.Equal(Tenant.Value, envTenant.Value);
    }

    [Fact]
    public async Task SubmitAsync_MultiTenantSingleton_EnvelopeTenantPerEntry()
    {
        // Regression: singleton service publishing across tenants must
        // route envelope TenantId per entry — NOT a ctor-bound default.
        var (_, svc, _, pub) = Build();
        var a = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor,
            new Instant(new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero)),
            Worker, projectId: ProjectId.NewId());
        var b = await svc.OpenAsync(OtherTenant, Worker, ActivityKind.Labor,
            new Instant(new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero)),
            Worker, projectId: ProjectId.NewId());
        await svc.StopAsync(Tenant,      a.Id, new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)), null, null, Worker);
        await svc.StopAsync(OtherTenant, b.Id, new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)), null, null, Worker);
        await svc.SubmitAsync(Tenant,      a.Id, Instant.Now, Worker);
        await svc.SubmitAsync(OtherTenant, b.Id, Instant.Now, Worker);

        var envelopes = pub.Published.ToArray();
        Assert.Equal(2, envelopes.Length);
        Assert.Contains(envelopes, e => e.TenantId.Value == Tenant.Value);
        Assert.Contains(envelopes, e => e.TenantId.Value == OtherTenant.Value);
    }

    [Fact]
    public async Task ApproveAsync_EmitsTimeEntryApproved_OneShot()
    {
        var (_, svc, approve, pub) = Build();
        var entry = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor,
            new Instant(new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero)),
            Worker, projectId: ProjectId.NewId());
        await svc.StopAsync(Tenant, entry.Id, new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            hourlyRate: 100m, rateCurrency: "USD", updatedBy: Worker);
        await svc.SubmitAsync(Tenant, entry.Id, Instant.Now, Worker);
        await approve.ApproveAsync(Tenant, entry.Id, Approver, Instant.Now);

        Assert.Equal(2, pub.Published.Count);
        Assert.Contains(pub.Published, e => e.EventType == "Work.TimeEntryApproved");
    }

    [Fact]
    public async Task GetByIdAsync_CrossTenant_ReturnsNull()
    {
        var (_, svc, _, _) = Build();
        var entry = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor, Instant.Now, Worker,
            projectId: ProjectId.NewId());
        Assert.Null(await svc.GetByIdAsync(OtherTenant, entry.Id));
    }

    [Fact]
    public async Task SubmitAsync_CrossTenant_ThrowsAndDoesNotMutate()
    {
        // H5 regression: a caller authenticated to OtherTenant must NOT
        // be able to mutate Tenant's entry by knowing its id.
        var (_, svc, _, pub) = Build();
        var entry = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor,
            new Instant(new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero)),
            Worker, projectId: ProjectId.NewId());
        await svc.StopAsync(Tenant, entry.Id, new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            null, null, Worker);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SubmitAsync(OtherTenant, entry.Id, Instant.Now, Worker));

        Assert.Empty(pub.Published);
        var fetched = await svc.GetByIdAsync(Tenant, entry.Id);
        Assert.NotNull(fetched);
        Assert.Equal(TimeEntryStatus.Open, fetched!.Status);
    }

    [Fact]
    public async Task ApproveAsync_CrossTenant_Throws()
    {
        var (_, svc, approve, _) = Build();
        var entry = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor,
            new Instant(new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero)),
            Worker, projectId: ProjectId.NewId());
        await svc.StopAsync(Tenant, entry.Id, new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            null, null, Worker);
        await svc.SubmitAsync(Tenant, entry.Id, Instant.Now, Worker);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            approve.ApproveAsync(OtherTenant, entry.Id, Approver, Instant.Now));
    }

    [Fact]
    public async Task RejectAsync_DoesNotEmitEvent()
    {
        var (_, svc, approve, pub) = Build();
        var entry = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor,
            new Instant(new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero)),
            Worker, projectId: ProjectId.NewId());
        await svc.StopAsync(Tenant, entry.Id, new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            null, null, Worker);
        await svc.SubmitAsync(Tenant, entry.Id, Instant.Now, Worker);
        await approve.RejectAsync(Tenant, entry.Id, Approver, Instant.Now, "incomplete description");

        Assert.Single(pub.Published);
        Assert.Equal("Work.TimeEntrySubmitted", pub.Published.First().EventType);
    }

    [Fact]
    public async Task SubmitAsync_RunningEntry_Throws()
    {
        var (_, svc, _, _) = Build();
        var entry = await svc.OpenAsync(Tenant, Worker, ActivityKind.Labor, Instant.Now, Worker,
            projectId: ProjectId.NewId());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.SubmitAsync(Tenant, entry.Id, Instant.Now, Worker));
    }
}
