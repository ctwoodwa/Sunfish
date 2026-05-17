using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>W#60 P4 — coverage for <see cref="ProjectActual"/> per Stage 02 §2.22.</summary>
public sealed class ProjectActualTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");

    [Fact]
    public void Create_SetsAllFields_FromHandlerInputs()
    {
        var pid     = ProjectId.NewId();
        var glAcct  = Guid.NewGuid();
        var srcRef  = Guid.NewGuid();
        var created = Instant.Now;

        var actual = ProjectActual.Create(
            tenantId:     Tenant,
            id:           ProjectActualId.NewId(),
            projectId:    pid,
            category:     BudgetCategory.Labor,
            glAccountId:  glAcct,
            postedAmount: 125.75m,
            currency:     "usd",
            postedDate:   new DateOnly(2026, 5, 16),
            sourceKind:   ActualSourceKind.TimeEntry,
            sourceRefId:  srcRef,
            createdAt:    created,
            createdBy:    JournalEntryPostedHandler_DefaultPrincipal,
            notes:        "approved hours");

        Assert.Equal(Tenant.Value, actual.TenantId.Value);
        Assert.Equal(pid.Value, actual.ProjectId.Value);
        Assert.Equal(BudgetCategory.Labor, actual.Category);
        Assert.Equal(glAcct, actual.GlAccountId);
        Assert.Equal(125.75m, actual.PostedAmount);
        Assert.Equal("USD", actual.Currency);
        Assert.Equal(new DateOnly(2026, 5, 16), actual.PostedDate);
        Assert.Equal(ActualSourceKind.TimeEntry, actual.SourceKind);
        Assert.Equal(srcRef, actual.SourceRefId);
        Assert.Equal("approved hours", actual.Notes);
        Assert.Equal(created.Value, actual.CreatedAt.Value);
        Assert.Null(actual.DeletedAt);
    }

    [Fact]
    public void Create_InvalidCurrency_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProjectActual.Create(
            Tenant, ProjectActualId.NewId(), ProjectId.NewId(), BudgetCategory.Other,
            glAccountId: null, postedAmount: 1m, currency: "DOLLARS",
            postedDate: new DateOnly(2026, 5, 16), sourceKind: ActualSourceKind.JournalEntry,
            sourceRefId: null, createdAt: Instant.Now, createdBy: Guid.NewGuid()));
    }

    [Fact]
    public void Create_CurrencyWithSurroundingWhitespace_IsTrimmed()
    {
        var actual = ProjectActual.Create(
            Tenant, ProjectActualId.NewId(), ProjectId.NewId(), BudgetCategory.Other,
            glAccountId: null, postedAmount: 1m, currency: "  usd  ",
            postedDate: new DateOnly(2026, 5, 16), sourceKind: ActualSourceKind.JournalEntry,
            sourceRefId: null, createdAt: Instant.Now, createdBy: Guid.NewGuid());
        Assert.Equal("USD", actual.Currency);
    }

    [Fact]
    public void Create_NotesExceedsMaxLength_Throws()
    {
        var huge = new string('x', ProjectActual.MaxNotesLength + 1);
        Assert.Throws<ArgumentException>(() => ProjectActual.Create(
            Tenant, ProjectActualId.NewId(), ProjectId.NewId(), BudgetCategory.Other,
            glAccountId: null, postedAmount: 1m, currency: "USD",
            postedDate: new DateOnly(2026, 5, 16), sourceKind: ActualSourceKind.JournalEntry,
            sourceRefId: null, createdAt: Instant.Now, createdBy: Guid.NewGuid(),
            notes: huge));
    }

    private static readonly Guid JournalEntryPostedHandler_DefaultPrincipal =
        Sunfish.Blocks.WorkProjects.Events.JournalEntryPostedHandler.ProjectorPrincipalId;
}
