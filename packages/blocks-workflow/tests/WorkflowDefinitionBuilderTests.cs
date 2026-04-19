using Xunit;
using Sunfish.Blocks.Workflow.Tests.Fixtures;

namespace Sunfish.Blocks.Workflow.Tests;

/// <summary>
/// Tests for <see cref="WorkflowDefinitionBuilder{TState,TTrigger,TContext}"/> validation behaviour.
/// </summary>
public sealed class WorkflowDefinitionBuilderTests
{
    // -------------------------------------------------------------------------
    // Simple fixture enums local to this file for builder-only tests
    // -------------------------------------------------------------------------

    private enum S { A, B, C, D }
    private enum T { Go, Back, End }
    private sealed class Ctx { }

    // -------------------------------------------------------------------------
    // Builder rejects missing StartAt
    // -------------------------------------------------------------------------

    [Fact]
    public void Build_WithoutStartAt_Throws()
    {
        var builder = new WorkflowDefinitionBuilder<S, T, Ctx>()
            .Transition(S.A, T.Go, S.B)
            .Terminal(S.B);

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("StartAt", ex.Message);
    }

    // -------------------------------------------------------------------------
    // Builder rejects duplicate transition edges
    // -------------------------------------------------------------------------

    [Fact]
    public void Transition_DuplicateEdge_ThrowsArgumentException()
    {
        var builder = new WorkflowDefinitionBuilder<S, T, Ctx>()
            .StartAt(S.A)
            .Transition(S.A, T.Go, S.B);

        var ex = Assert.Throws<ArgumentException>(() => builder.Transition(S.A, T.Go, S.C));
        Assert.Contains("Duplicate transition edge", ex.Message);
    }

    // -------------------------------------------------------------------------
    // Builder rejects reachable dead-end (non-terminal, no outgoing edges)
    // -------------------------------------------------------------------------

    [Fact]
    public void Build_ReachableDeadEnd_Throws()
    {
        // A → B  (B has no outgoing transitions and is not terminal)
        var builder = new WorkflowDefinitionBuilder<S, T, Ctx>()
            .StartAt(S.A)
            .Transition(S.A, T.Go, S.B);

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("B", ex.Message);
        Assert.Contains("terminal", ex.Message);
    }

    // -------------------------------------------------------------------------
    // Build succeeds with minimal valid definition
    // -------------------------------------------------------------------------

    [Fact]
    public void Build_ValidDefinition_ReturnsDefinition()
    {
        var def = new WorkflowDefinitionBuilder<S, T, Ctx>()
            .StartAt(S.A)
            .Transition(S.A, T.Go, S.B)
            .Terminal(S.B)
            .Build();

        Assert.NotNull(def);
        Assert.Equal(S.A, def.InitialState);
        Assert.Contains(S.B, def.TerminalStates);
    }

    // -------------------------------------------------------------------------
    // Next returns correct target state
    // -------------------------------------------------------------------------

    [Fact]
    public void Next_ValidTrigger_ReturnsTargetState()
    {
        var def = new WorkflowDefinitionBuilder<S, T, Ctx>()
            .StartAt(S.A)
            .Transition(S.A, T.Go, S.B)
            .Terminal(S.B)
            .Build();

        Assert.Equal(S.B, def.Next(S.A, T.Go));
    }

    [Fact]
    public void Next_InvalidTrigger_ReturnsNull()
    {
        var def = new WorkflowDefinitionBuilder<S, T, Ctx>()
            .StartAt(S.A)
            .Transition(S.A, T.Go, S.B)
            .Terminal(S.B)
            .Build();

        Assert.Null(def.Next(S.A, T.Back));
    }
}
