using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Foundation.Wayfinder.Tests;

/// <summary>
/// Phase 1 §2 — canonical-JSON round-trip tests through
/// <see cref="CanonicalJson.Serialize"/>. Per ADR 0028 §A7.8 / cohort precedent
/// (W#34 / W#35 / W#36 / W#39 / W#40 / W#41).
/// </summary>
public sealed class StandingOrderCanonicalJsonTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly ActorId ActorA = new("u1");
    private static readonly DateTimeOffset Issued = new(2026, 5, 4, 12, 0, 0, TimeSpan.Zero);

    private static StandingOrder NewOrder() => new(
        new StandingOrderId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
        TenantA,
        ActorA,
        Issued,
        StandingOrderScope.Tenant,
        new[] { new StandingOrderTriple("anchor.maui.theme", JsonNode.Parse("\"light\""), JsonNode.Parse("\"dark\"")) },
        "switch to dark mode",
        new ApprovalChain(new[] { new ApprovalStep(ActorA, Issued, "ok") }),
        new AuditRecordId(Guid.Parse("00000000-0000-0000-0000-000000000002")),
        StandingOrderState.Issued);

    [Fact]
    public void StandingOrder_RoundTripsThroughCanonicalJson_ByteStable()
    {
        var order = NewOrder();
        var bytes = CanonicalJson.Serialize(order);
        var roundtripped = JsonSerializer.Deserialize<StandingOrder>(Encoding.UTF8.GetString(bytes));

        Assert.NotNull(roundtripped);
        Assert.Equal(bytes, CanonicalJson.Serialize(roundtripped));
    }

    [Fact]
    public void StandingOrder_UsesCamelCasePropertyNames()
    {
        var order = NewOrder();
        var json = Encoding.UTF8.GetString(CanonicalJson.Serialize(order));

        Assert.Contains("\"id\"", json);
        Assert.Contains("\"tenantId\"", json);
        Assert.Contains("\"issuedBy\"", json);
        Assert.Contains("\"issuedAt\"", json);
        Assert.Contains("\"scope\"", json);
        Assert.Contains("\"triples\"", json);
        Assert.Contains("\"rationale\"", json);
        Assert.Contains("\"approvalChain\"", json);
        Assert.Contains("\"auditRecordId\"", json);
        Assert.Contains("\"state\"", json);
    }

    [Fact]
    public void StandingOrderValidationIssue_RoundTripsThroughCanonicalJson()
    {
        var issue = new StandingOrderValidationIssue(
            StandingOrderValidationSeverity.Block,
            "anchor.maui.theme",
            "value not allowed",
            "try light or dark");
        var bytes = CanonicalJson.Serialize(issue);
        var roundtripped = JsonSerializer.Deserialize<StandingOrderValidationIssue>(Encoding.UTF8.GetString(bytes));

        Assert.NotNull(roundtripped);
        Assert.Equal(issue, roundtripped);
        Assert.Equal(bytes, CanonicalJson.Serialize(roundtripped));
    }

    [Fact]
    public void StandingOrderTriple_NullValues_SerializedAsJsonNull()
    {
        var triple = new StandingOrderTriple("anchor.maui.theme", null, JsonNode.Parse("\"dark\""));
        var json = Encoding.UTF8.GetString(CanonicalJson.Serialize(triple));

        Assert.Contains("\"oldValue\":null", json);
        Assert.Contains("\"newValue\":\"dark\"", json);
    }
}
