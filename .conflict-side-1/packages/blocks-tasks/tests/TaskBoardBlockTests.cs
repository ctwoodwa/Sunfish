using TaskStatus = Sunfish.Blocks.Tasks.Models.TaskStatus;
using Sunfish.Blocks.Tasks;
using Sunfish.Blocks.Tasks.Models;
using Sunfish.Blocks.Tasks.State;
using Xunit;

namespace Sunfish.Blocks.Tasks.Tests;

public class TaskBoardBlockTests
{
    [Fact]
    public void TaskBoardBlock_TypeIsPublicAndInBlocksTasksNamespace()
    {
        var type = typeof(TaskBoardBlock);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Tasks", type.Namespace);
    }

    [Fact]
    public void TaskBoardState_AllowsValidForwardTransition()
    {
        var state = new TaskBoardState();
        var item = new TaskItem { Id = "1", Title = "x", Status = TaskStatus.Todo };

        Assert.True(state.TryTransition(item, TaskStatus.InProgress, out var updated));
        Assert.Equal(TaskStatus.InProgress, updated.Status);
    }

    [Fact]
    public void TaskBoardState_RejectsInvalidSkipTransition()
    {
        var state = new TaskBoardState();
        var item = new TaskItem { Id = "1", Title = "x", Status = TaskStatus.Backlog };

        Assert.False(state.TryTransition(item, TaskStatus.Done, out var updated));
        Assert.Equal(TaskStatus.Backlog, updated.Status);
    }
}
