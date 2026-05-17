using Sunfish.Blocks.WorkProjects.Models;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="ProjectStatusMachine"/> per
/// Stage 02 §2.2 8-state diagram.
/// </summary>
public sealed class ProjectStatusMachineTests
{
    [Fact]
    public void AllTerminalStates_ReturnEmptyTransitions()
    {
        Assert.Empty(ProjectStatusMachine.AllowedTransitionsFrom(ProjectStatus.Closed));
        Assert.Empty(ProjectStatusMachine.AllowedTransitionsFrom(ProjectStatus.Cancelled));
    }

    [Fact]
    public void StateMachine_AllowsReopen_CompletedToInProgress()
    {
        Assert.True(ProjectStatusMachine.CanTransition(
            ProjectStatus.Completed, ProjectStatus.InProgress));
    }

    [Fact]
    public void StateMachine_RejectsSelfTransition()
    {
        Assert.False(ProjectStatusMachine.CanTransition(
            ProjectStatus.InProgress, ProjectStatus.InProgress));
    }

    [Fact]
    public void StateMachine_AllowsHappyPath()
    {
        Assert.True(ProjectStatusMachine.CanTransition(ProjectStatus.Draft, ProjectStatus.Planned));
        Assert.True(ProjectStatusMachine.CanTransition(ProjectStatus.Planned, ProjectStatus.InProgress));
        Assert.True(ProjectStatusMachine.CanTransition(ProjectStatus.InProgress, ProjectStatus.Completed));
        Assert.True(ProjectStatusMachine.CanTransition(ProjectStatus.Completed, ProjectStatus.Closed));
    }

    [Fact]
    public void StateMachine_RejectsDraftToInProgressDirectly()
    {
        Assert.False(ProjectStatusMachine.CanTransition(
            ProjectStatus.Draft, ProjectStatus.InProgress));
    }
}
