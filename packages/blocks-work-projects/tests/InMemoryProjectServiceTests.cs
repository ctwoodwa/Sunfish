using System.Collections.Concurrent;
using Sunfish.Blocks.WorkProjects.Events;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>W#60 P4 — coverage for <see cref="InMemoryProjectService"/> (PR 6 orchestrator).</summary>
public sealed class InMemoryProjectServiceTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Owner = Guid.NewGuid();

    private sealed class Recorder : IDomainEventPublisher
    {
        public ConcurrentQueue<(string EventType, string IdempotencyKey)> Published { get; } = new();
        public Task PublishAsync<T>(DomainEventEnvelope<T> envelope, CancellationToken cancellationToken = default)
        {
            Published.Enqueue((envelope.EventType, envelope.IdempotencyKey));
            return Task.CompletedTask;
        }
    }

    private static (InMemoryProjectService Svc, InMemoryProjectRepository Repo, InMemoryProjectMilestoneRepository Mile, Recorder Pub) Build()
    {
        var pr   = new InMemoryProjectRepository();
        var mr   = new InMemoryProjectMilestoneRepository();
        var pub  = new Recorder();
        var svc  = new InMemoryProjectService(pr, mr, new InMemoryProjectCodeGenerator(), pub);
        return (svc, pr, mr, pub);
    }

    [Fact]
    public async Task CreateAsync_AssignsCodeAndEmitsProjectCreated()
    {
        var (svc, repo, _, pub) = Build();
        var p = await svc.CreateAsync(Tenant, "Whitney Unit 5B Remodel", ProjectKind.Remodel,
            Priority.High, Owner, Owner);
        Assert.False(string.IsNullOrWhiteSpace(p.Code));
        Assert.NotNull(repo.GetById(Tenant, p.Id));
        Assert.Single(pub.Published);
        var (eventType, key) = pub.Published.First();
        Assert.Equal("Work.ProjectCreated", eventType);
        Assert.Equal($"project-created:{p.Id.Value}", key);
    }

    [Fact]
    public async Task TransitionStatusAsync_NonOwner_Throws_NotProjectOwnerException()
    {
        var (svc, _, _, _) = Build();
        var p = await svc.CreateAsync(Tenant, "P1", ProjectKind.Generic, Priority.Normal, Owner, Owner);
        var stranger = Guid.NewGuid();
        await Assert.ThrowsAsync<NotProjectOwnerException>(() =>
            svc.TransitionStatusAsync(Tenant, p.Id, ProjectStatus.Planned, stranger, stranger));
    }

    [Fact]
    public async Task TransitionStatusAsync_ValidByOwner_EmitsProjectStatusChanged()
    {
        var (svc, _, _, pub) = Build();
        var p = await svc.CreateAsync(Tenant, "P1", ProjectKind.Generic, Priority.Normal, Owner, Owner);
        await svc.TransitionStatusAsync(Tenant, p.Id, ProjectStatus.Planned, Owner, Owner);
        Assert.Contains(pub.Published, e => e.EventType == "Work.ProjectStatusChanged");
        var key = pub.Published.First(e => e.EventType == "Work.ProjectStatusChanged").IdempotencyKey;
        Assert.StartsWith($"project-status:{p.Id.Value}:", key);
    }

    [Fact]
    public async Task TransitionStatusAsync_CrossTenant_Throws()
    {
        var (svc, _, _, _) = Build();
        var p = await svc.CreateAsync(Tenant, "P1", ProjectKind.Generic, Priority.Normal, Owner, Owner);
        var other = new TenantId("other-tenant");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.TransitionStatusAsync(other, p.Id, ProjectStatus.Planned, Owner, Owner));
    }

    [Fact]
    public async Task AchieveMilestoneAsync_TriggersInvoice_EmitsBothEvents()
    {
        var (svc, _, _, pub) = Build();
        var p = await svc.CreateAsync(Tenant, "P1", ProjectKind.Generic, Priority.Normal, Owner, Owner);
        var customer = Guid.NewGuid();
        var m = await svc.AddMilestoneAsync(Tenant, p.Id, "M1", "Final payment",
            MilestoneKind.Payment, new DateOnly(2026, 6, 1), Owner,
            paymentAmount: 5_000m, paymentCurrency: "USD", triggersInvoice: true, customerPartyId: customer);
        await svc.AchieveMilestoneAsync(Tenant, m.Id, new DateOnly(2026, 6, 1), Owner);

        Assert.Contains(pub.Published, e => e.EventType == "Work.MilestoneCreated");
        Assert.Contains(pub.Published, e => e.EventType == "Work.MilestoneAchieved");
        Assert.Contains(pub.Published, e => e.EventType == "Work.MilestoneInvoiceTriggered");
        var invoiceKey = pub.Published.First(e => e.EventType == "Work.MilestoneInvoiceTriggered").IdempotencyKey;
        Assert.Equal($"milestone-invoice:{m.Id.Value}", invoiceKey);
    }

    [Fact]
    public async Task AchieveMilestoneAsync_NoTrigger_OnlyEmitsAchieved()
    {
        var (svc, _, _, pub) = Build();
        var p = await svc.CreateAsync(Tenant, "P1", ProjectKind.Generic, Priority.Normal, Owner, Owner);
        var m = await svc.AddMilestoneAsync(Tenant, p.Id, "M1", "Schedule landmark",
            MilestoneKind.Schedule, new DateOnly(2026, 6, 1), Owner);
        await svc.AchieveMilestoneAsync(Tenant, m.Id, new DateOnly(2026, 6, 1), Owner);

        Assert.DoesNotContain(pub.Published, e => e.EventType == "Work.MilestoneInvoiceTriggered");
        Assert.Contains(pub.Published, e => e.EventType == "Work.MilestoneAchieved");
    }

    [Fact]
    public async Task CreateAsync_NameExceedsMax_Throws()
    {
        var (svc, _, _, _) = Build();
        var huge = new string('x', Project.MaxNameLength + 1);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateAsync(Tenant, huge, ProjectKind.Generic, Priority.Normal, Owner, Owner));
    }
}
