using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Foundation.Wayfinder.Tests;

/// <summary>
/// Phase 1 §1 — record-shape round-trip tests. Every cohort substrate ships
/// these to lock the public surface against accidental mutation.
/// </summary>
public sealed class StandingOrderShapeTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly ActorId ActorA = new("u1");
    private static readonly ActorId ActorB = new("u2");
    private static readonly DateTimeOffset Issued = new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StandingOrder_ConstructorWithRecordEquality_RoundTrips()
    {
        var triples = new[]
        {
            new StandingOrderTriple("anchor.maui.theme", JsonNode.Parse("\"light\""), JsonNode.Parse("\"dark\"")),
        };
        var order = new StandingOrder(
            new StandingOrderId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            TenantA,
            ActorA,
            Issued,
            StandingOrderScope.User,
            triples,
            "switch to dark mode",
            ApprovalChain: null,
            new AuditRecordId(Guid.Parse("00000000-0000-0000-0000-000000000002")),
            StandingOrderState.Issued);

        Assert.Equal(TenantA, order.TenantId);
        Assert.Equal(ActorA, order.IssuedBy);
        Assert.Equal(Issued, order.IssuedAt);
        Assert.Equal(StandingOrderScope.User, order.Scope);
        Assert.Single(order.Triples);
        Assert.Equal("switch to dark mode", order.Rationale);
        Assert.Null(order.ApprovalChain);
        Assert.Equal(StandingOrderState.Issued, order.State);
    }

    [Fact]
    public void StandingOrderTriple_PreservesUnsetSemantics()
    {
        // OldValue null = the path was previously unset.
        var triple1 = new StandingOrderTriple("anchor.maui.theme", null, JsonNode.Parse("\"dark\""));
        Assert.Null(triple1.OldValue);
        Assert.NotNull(triple1.NewValue);

        // NewValue null = the path is being unset (delete).
        var triple2 = new StandingOrderTriple("anchor.maui.theme", JsonNode.Parse("\"dark\""), null);
        Assert.NotNull(triple2.OldValue);
        Assert.Null(triple2.NewValue);
    }

    [Fact]
    public void ApprovalChain_EmptyStepsList_IsValid()
    {
        var chain = new ApprovalChain(Array.Empty<ApprovalStep>());
        Assert.NotNull(chain);
        Assert.Empty(chain.Steps);
    }

    [Fact]
    public void ApprovalStep_OptionalCommentNullable()
    {
        var stepA = new ApprovalStep(ActorA, Issued, "approved with caveat");
        var stepB = new ApprovalStep(ActorB, Issued.AddMinutes(5), null);

        Assert.Equal("approved with caveat", stepA.Comment);
        Assert.Null(stepB.Comment);
    }

    [Fact]
    public void StandingOrderValidationResult_AcceptedFalseWithBlockIssue()
    {
        var issue = new StandingOrderValidationIssue(
            StandingOrderValidationSeverity.Block,
            "anchor.maui.theme",
            "theme value 'experimental' not allowed in production",
            "use 'light' or 'dark'");
        var result = new StandingOrderValidationResult(false, new[] { issue });

        Assert.False(result.Accepted);
        Assert.Single(result.Issues);
        Assert.Equal(StandingOrderValidationSeverity.Block, result.Issues[0].Severity);
    }
}
