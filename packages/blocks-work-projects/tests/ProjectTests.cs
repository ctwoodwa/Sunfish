using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="Project"/> per Stage 02 §2.1
/// + §2.2 status machine.
/// </summary>
public sealed class ProjectTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Create_ValidInput_StatusIsDraft()
    {
        var p = NewProject();
        Assert.Equal(ProjectStatus.Draft, p.Status);
    }

    [Fact]
    public void Create_KindRemodel_DoesNotRequireSidecar()
    {
        // Sidecar (RemodelProject) lives in PR 5; entity creation is
        // independent.
        var p = NewProject(kind: ProjectKind.Remodel);
        Assert.Equal(ProjectKind.Remodel, p.Kind);
    }

    [Fact]
    public void Create_EmptyCode_Throws()
    {
        Assert.Throws<ArgumentException>(() => Project.Create(
            Tenant, ProjectId.NewId(), "  ", "Name",
            ProjectKind.Generic, Priority.Normal, Owner, Actor, Instant.Now));
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => Project.Create(
            Tenant, ProjectId.NewId(), "PRJ-2026-L00001", "  ",
            ProjectKind.Generic, Priority.Normal, Owner, Actor, Instant.Now));
    }

    [Fact]
    public void Create_OwnerPartyEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => Project.Create(
            Tenant, ProjectId.NewId(), "PRJ-2026-L00001", "Name",
            ProjectKind.Generic, Priority.Normal, Guid.Empty, Actor, Instant.Now));
    }

    [Fact]
    public void Create_PlannedEndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(() => Project.Create(
            Tenant, ProjectId.NewId(), "PRJ-2026-L00001", "Name",
            ProjectKind.Generic, Priority.Normal, Owner, Actor, Instant.Now,
            plannedStartDate: new DateOnly(2026, 6, 1),
            plannedEndDate:   new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void TransitionStatus_DraftToPlanned_Succeeds()
    {
        var p = NewProject();
        p.TransitionStatus(ProjectStatus.Planned, Actor, Instant.Now);
        Assert.Equal(ProjectStatus.Planned, p.Status);
    }

    [Fact]
    public void TransitionStatus_DraftToCompleted_Throws()
    {
        var p = NewProject();
        Assert.Throws<InvalidProjectStatusTransitionException>(
            () => p.TransitionStatus(ProjectStatus.Completed, Actor, Instant.Now));
    }

    [Fact]
    public void TransitionStatus_ClosedToInProgress_Throws_TerminalState()
    {
        var p = NewProject();
        p.TransitionStatus(ProjectStatus.Planned, Actor, Instant.Now);
        p.TransitionStatus(ProjectStatus.InProgress, Actor, Instant.Now);
        p.TransitionStatus(ProjectStatus.Completed, Actor, Instant.Now);
        p.TransitionStatus(ProjectStatus.Closed, Actor, Instant.Now);

        Assert.Throws<InvalidProjectStatusTransitionException>(
            () => p.TransitionStatus(ProjectStatus.InProgress, Actor, Instant.Now));
    }

    [Fact]
    public void TransitionStatus_CompletedToInProgress_ReopenPath_Succeeds()
    {
        var p = NewProject();
        p.TransitionStatus(ProjectStatus.Planned, Actor, Instant.Now);
        p.TransitionStatus(ProjectStatus.InProgress, Actor, Instant.Now);
        p.TransitionStatus(ProjectStatus.Completed, Actor, Instant.Now);

        p.TransitionStatus(ProjectStatus.InProgress, Actor, Instant.Now);

        Assert.Equal(ProjectStatus.InProgress, p.Status);
    }

    [Fact]
    public void Archive_SetsArchivedAt()
    {
        var p = NewProject();
        p.Archive(Actor, Instant.Now);
        Assert.NotNull(p.ArchivedAt);
        // Archive is distinct from soft-delete.
        Assert.Null(p.DeletedAt);
    }

    [Fact]
    public void SoftDelete_SetsDeletedAt_CannotTransition()
    {
        var p = NewProject();
        p.SoftDelete(Actor, Instant.Now);
        Assert.NotNull(p.DeletedAt);
        Assert.Throws<InvalidOperationException>(
            () => p.TransitionStatus(ProjectStatus.Planned, Actor, Instant.Now));
    }

    [Fact]
    public void UpdatePlannedDates_EndBeforeStart_Throws()
    {
        var p = NewProject();
        Assert.Throws<ArgumentException>(() => p.UpdatePlannedDates(
            new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1), Actor, Instant.Now));
    }

    private static Project NewProject(ProjectKind kind = ProjectKind.Generic)
        => Project.Create(
            Tenant, ProjectId.NewId(), "PRJ-2026-L00001", "Test Project",
            kind, Priority.Normal, Owner, Actor, Instant.Now);
}
