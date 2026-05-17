using Sunfish.Blocks.WorkProjects.Events;
using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>W#60 P4 — coverage for <see cref="JournalEntryPostedHandler"/>.</summary>
public sealed class JournalEntryPostedHandlerTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");

    private static DomainEventEnvelope<JournalEntryPostedPayload> Envelope(
        Guid entryId, string sourceKind, params JournalEntryPostedLine[] lines)
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
                Lines:      lines),
        };

    private static JournalEntryPostedLine Line(decimal debit, decimal credit, ProjectId? projectId = null, string? currency = "USD")
        => new(
            AccountId: Guid.NewGuid(),
            Debit:     debit,
            Credit:    credit,
            Currency:  currency,
            Dimensions: projectId is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["projectId"] = projectId.Value.Value.ToString() });

    [Fact]
    public async Task Handle_JeWithoutProjectDimension_Skips()
    {
        var repo = new InMemoryProjectActualRepository();
        var handler = new JournalEntryPostedHandler(repo);
        await handler.HandleAsync(Envelope(Guid.NewGuid(), "Manual", Line(100m, 0m, projectId: null)));
        var pid = ProjectId.NewId();
        Assert.Empty(await repo.GetByProjectAsync(Tenant, pid));
    }

    [Fact]
    public async Task Handle_JeWithProjectDimension_CreatesProjectActual()
    {
        var repo = new InMemoryProjectActualRepository();
        var handler = new JournalEntryPostedHandler(repo);
        var pid = ProjectId.NewId();
        var entryId = Guid.NewGuid();
        await handler.HandleAsync(Envelope(entryId, "Invoice", Line(250m, 0m, pid)));
        var rows = await repo.GetByProjectAsync(Tenant, pid);
        var row = Assert.Single(rows);
        Assert.Equal(250m, row.PostedAmount);
        Assert.Equal(ActualSourceKind.Invoice, row.SourceKind);
        Assert.Equal(entryId, row.SourceRefId);
    }

    [Fact]
    public async Task Handle_SameJeProjected_Idempotent()
    {
        var repo = new InMemoryProjectActualRepository();
        var handler = new JournalEntryPostedHandler(repo);
        var pid = ProjectId.NewId();
        var entryId = Guid.NewGuid();
        var envelope = Envelope(entryId, "Invoice", Line(100m, 0m, pid));
        await handler.HandleAsync(envelope);
        await handler.HandleAsync(envelope);   // replay — must not double-project
        var rows = await repo.GetByProjectAsync(Tenant, pid);
        Assert.Single(rows);
    }

    [Fact]
    public async Task Handle_MapsSourceKindTimeEntry_Correctly()
    {
        var repo = new InMemoryProjectActualRepository();
        var handler = new JournalEntryPostedHandler(repo);
        var pid = ProjectId.NewId();
        await handler.HandleAsync(Envelope(Guid.NewGuid(), "TimeEntry", Line(80m, 0m, pid)));
        var rows = await repo.GetByProjectAsync(Tenant, pid);
        Assert.Equal(ActualSourceKind.TimeEntry, rows.Single().SourceKind);
    }

    [Fact]
    public async Task Handle_MapsUnknownSourceKind_FallsBackToJournalEntry()
    {
        var repo = new InMemoryProjectActualRepository();
        var handler = new JournalEntryPostedHandler(repo);
        var pid = ProjectId.NewId();
        await handler.HandleAsync(Envelope(Guid.NewGuid(), "RandomUnknown", Line(50m, 0m, pid)));
        var rows = await repo.GetByProjectAsync(Tenant, pid);
        Assert.Equal(ActualSourceKind.JournalEntry, rows.Single().SourceKind);
    }

    [Fact]
    public async Task Handle_MultipleLinesSameProjectDifferentAccounts_CreatesOneRowPerAccount()
    {
        // Composite idempotency key includes GlAccountId so that a JE
        // splitting cost across (e.g.) Labor + Materials on the same
        // project preserves per-line granularity — both lines project,
        // and GetTotalsAsync reflects the full posting.
        var repo = new InMemoryProjectActualRepository();
        var handler = new JournalEntryPostedHandler(repo);
        var pid = ProjectId.NewId();
        await handler.HandleAsync(Envelope(Guid.NewGuid(), "Manual",
            Line(100m, 0m, pid),
            Line(50m, 0m, pid)));
        var rows = await repo.GetByProjectAsync(Tenant, pid);
        Assert.Equal(2, rows.Count);
        Assert.Equal(150m, rows.Sum(r => r.PostedAmount));
    }

    [Fact]
    public async Task Handle_SameProjectSameAccountSameJe_DedupsToOneRow()
    {
        // Defense-in-depth idempotency: replay of a JE with the same
        // (project, account, JE id) tuple must not double-project.
        var repo = new InMemoryProjectActualRepository();
        var handler = new JournalEntryPostedHandler(repo);
        var pid = ProjectId.NewId();
        var entryId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var line = new JournalEntryPostedLine(
            AccountId: accountId, Debit: 75m, Credit: 0m, Currency: "USD",
            Dimensions: new Dictionary<string, string> { ["projectId"] = pid.Value.ToString() });
        var env = new DomainEventEnvelope<JournalEntryPostedPayload>
        {
            EventId              = EventId.New(),
            EventType            = "Financial.JournalEntryPosted",
            SchemaVersion        = 1,
            OccurredAt           = DateTimeOffset.UtcNow,
            TenantId             = Tenant,
            OriginatingReplicaId = ReplicaId.System,
            IdempotencyKey       = $"je-posted:{entryId}",
            Payload              = new JournalEntryPostedPayload(entryId, new DateOnly(2026, 5, 16), "Manual", new[] { line, line }),
        };
        await handler.HandleAsync(env);
        Assert.Single(await repo.GetByProjectAsync(Tenant, pid));
    }

    [Fact]
    public async Task Handle_DebitMinusCreditComputed_NegativeForCreditSide()
    {
        var repo = new InMemoryProjectActualRepository();
        var handler = new JournalEntryPostedHandler(repo);
        var pid = ProjectId.NewId();
        await handler.HandleAsync(Envelope(Guid.NewGuid(), "Manual", Line(0m, 30m, pid)));
        var rows = await repo.GetByProjectAsync(Tenant, pid);
        Assert.Equal(-30m, rows.Single().PostedAmount);
    }

    [Fact]
    public async Task Handle_CategoryResolverReturnsLabor_RowCarriesLaborCategory()
    {
        var repo = new InMemoryProjectActualRepository();
        var resolver = new StubCategoryResolver(BudgetCategory.Labor);
        var handler = new JournalEntryPostedHandler(repo, resolver);
        var pid = ProjectId.NewId();
        await handler.HandleAsync(Envelope(Guid.NewGuid(), "TimeEntry", Line(75m, 0m, pid)));
        var rows = await repo.GetByProjectAsync(Tenant, pid);
        Assert.Equal(BudgetCategory.Labor, rows.Single().Category);
    }

    private sealed class StubCategoryResolver : IGlAccountCategoryResolver
    {
        private readonly BudgetCategory _category;
        public StubCategoryResolver(BudgetCategory category) => _category = category;
        public Task<BudgetCategory> ResolveAsync(TenantId t, Guid a, CancellationToken c = default)
            => Task.FromResult(_category);
    }
}
