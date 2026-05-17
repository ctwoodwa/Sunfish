using System.Collections.Concurrent;
using Sunfish.Blocks.WorkProjects.Migration;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>W#60 P4 — coverage for <see cref="ErpnextProjectImporter"/>.</summary>
public sealed class ErpnextProjectImporterTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Owner = Guid.NewGuid();

    private sealed class NoopPub : IDomainEventPublisher
    {
        public Task PublishAsync<T>(DomainEventEnvelope<T> envelope, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private static (ErpnextProjectImporter Importer, InMemoryProjectRepository Repo) Build()
    {
        var repo = new InMemoryProjectRepository();
        var mile = new InMemoryProjectMilestoneRepository();
        var svc  = new InMemoryProjectService(repo, mile, new InMemoryProjectCodeGenerator(), new NoopPub());
        return (new ErpnextProjectImporter(svc, repo), repo);
    }

    private static ErpnextProjectSource Source(string name, string modified, string? projectType = null)
        => new(Name: name, Modified: modified, ProjectName: $"erpnext-{name}",
            Status: "Open",
            ExpectedStartDate: new DateOnly(2026, 5, 1),
            ExpectedEndDate:   new DateOnly(2026, 6, 1),
            ActualStartDate:   null, ActualEndDate: null,
            Customer: null, CostCenter: null,
            EstimatedCosting: 12_000m,
            ProjectType: projectType);

    [Fact]
    public async Task UpsertFromErpnextAsync_NewSource_InsertsProject()
    {
        var (importer, repo) = Build();
        var result = await importer.UpsertFromErpnextAsync(Source("PROJ-0001", "v1"), Tenant, Owner);
        Assert.Equal(ImportOutcomeKind.Inserted, result.Kind);
        Assert.NotNull(result.Entity);
        Assert.Single(repo.ListByTenant(Tenant));
    }

    [Fact]
    public async Task UpsertFromErpnextAsync_SameModified_ReturnsSkipped()
    {
        var (importer, _) = Build();
        var src = Source("PROJ-0001", "v1");
        await importer.UpsertFromErpnextAsync(src, Tenant, Owner);
        var second = await importer.UpsertFromErpnextAsync(src, Tenant, Owner);
        Assert.Equal(ImportOutcomeKind.Skipped, second.Kind);
    }

    [Fact]
    public async Task UpsertFromErpnextAsync_ModifiedAdvanced_ReturnsUpdated()
    {
        var (importer, _) = Build();
        await importer.UpsertFromErpnextAsync(Source("PROJ-0001", "v1"), Tenant, Owner);
        var result = await importer.UpsertFromErpnextAsync(Source("PROJ-0001", "v2"), Tenant, Owner);
        Assert.Equal(ImportOutcomeKind.Updated, result.Kind);
    }

    [Fact]
    public async Task UpsertFromErpnextAsync_RemodelType_PromotesProjectKindRemodel()
    {
        var (importer, _) = Build();
        var result = await importer.UpsertFromErpnextAsync(
            Source("PROJ-REMODEL-1", "v1", projectType: "Remodel"), Tenant, Owner);
        Assert.Equal(ProjectKind.Remodel, result.Entity!.Kind);
    }

    [Fact]
    public async Task UpsertFromErpnextAsync_EmptyName_ReturnsFailed()
    {
        var (importer, _) = Build();
        var result = await importer.UpsertFromErpnextAsync(Source("  ", "v1"), Tenant, Owner);
        Assert.Equal(ImportOutcomeKind.Failed, result.Kind);
    }

    [Fact]
    public async Task UpsertFromErpnextAsync_NameWithNewline_RejectedByTagGuard()
    {
        var (importer, _) = Build();
        // \n in source.Name would flow into the externalRef tag; OverwriteTags must reject.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            importer.UpsertFromErpnextAsync(Source("foo\nbar", "v1"), Tenant, Owner));
    }
}
