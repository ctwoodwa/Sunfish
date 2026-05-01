using Sunfish.Foundation.MissionSpace.Audit;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Tests;

public sealed class MissionSpaceAuditPayloadsTests
{
    [Fact]
    public void FeatureProbed_PopulatesAlphabetizedKeys()
    {
        var p = MissionSpaceAuditPayloads.FeatureProbed("Hardware", "Healthy", 12.5);
        Assert.Equal("Hardware", p.Body["dimension"]);
        Assert.Equal(12.5, p.Body["duration_ms"]);
        Assert.Equal("Healthy", p.Body["probe_status"]);
    }

    [Fact]
    public void FeatureProbeFailed_PopulatesAlphabetizedKeys()
    {
        var p = MissionSpaceAuditPayloads.FeatureProbeFailed("Network", "TimeoutAfter5s", 5012);
        Assert.Equal("Network", p.Body["dimension"]);
        Assert.Equal(5012.0, p.Body["duration_ms"]);
        Assert.Equal("TimeoutAfter5s", p.Body["failure_reason"]);
    }

    [Fact]
    public void FeatureForceEnabled_PopulatesAllFields()
    {
        var p = MissionSpaceAuditPayloads.FeatureForceEnabled(
            "feature.x", "Network", "operator-1", expiresAt: "2026-05-02T00:00:00Z", reason: "migration");
        Assert.Equal("Network", p.Body["dimension"]);
        Assert.Equal("2026-05-02T00:00:00Z", p.Body["expires_at"]);
        Assert.Equal("feature.x", p.Body["feature_key"]);
        Assert.Equal("operator-1", p.Body["operator_principal_id"]);
        Assert.Equal("migration", p.Body["reason"]);
    }

    [Fact]
    public void FeatureForceEnabled_AcceptsNullExpiresAtAndReason()
    {
        var p = MissionSpaceAuditPayloads.FeatureForceEnabled(
            "feature.x", "Network", "operator-1", expiresAt: null, reason: null);
        Assert.Null(p.Body["expires_at"]);
        Assert.Null(p.Body["reason"]);
    }

    [Fact]
    public void FeatureForceRevoked_PopulatesFields()
    {
        var p = MissionSpaceAuditPayloads.FeatureForceRevoked("feature.x", "Network", "operator-1");
        Assert.Equal("Network", p.Body["dimension"]);
        Assert.Equal("feature.x", p.Body["feature_key"]);
        Assert.Equal("operator-1", p.Body["operator_principal_id"]);
    }

    [Fact]
    public void FeatureForceEnableRejected_PopulatesPolicyAndReason()
    {
        var p = MissionSpaceAuditPayloads.FeatureForceEnableRejected(
            "feature.x", "Hardware", "operator-1", "NotOverridable", "tried");
        Assert.Equal("Hardware", p.Body["dimension"]);
        Assert.Equal("feature.x", p.Body["feature_key"]);
        Assert.Equal("operator-1", p.Body["operator_principal_id"]);
        Assert.Equal("NotOverridable", p.Body["policy"]);
        Assert.Equal("tried", p.Body["reason"]);
    }

    [Fact]
    public void EnvelopeChangeBroadcast_PopulatesFields()
    {
        var p = MissionSpaceAuditPayloads.EnvelopeChangeBroadcast(2, "deadbeef", "Warning");
        Assert.Equal(2, p.Body["changed_dimension_count"]);
        Assert.Equal("deadbeef", p.Body["envelope_hash"]);
        Assert.Equal("Warning", p.Body["severity"]);
    }

    [Fact]
    public void ObserverOverflow_PopulatesMaxPending()
    {
        var p = MissionSpaceAuditPayloads.ObserverOverflow(100);
        Assert.Equal(100, p.Body["max_pending"]);
    }

    [Fact]
    public void FeatureVerdictSurfaced_PopulatesAllFields()
    {
        var p = MissionSpaceAuditPayloads.FeatureVerdictSurfaced(
            "feature.x", "DegradedAvailable", "ReadOnly", "abc123");
        Assert.Equal("DegradedAvailable", p.Body["availability_state"]);
        Assert.Equal("ReadOnly", p.Body["degradation_kind"]);
        Assert.Equal("abc123", p.Body["envelope_hash"]);
        Assert.Equal("feature.x", p.Body["feature_key"]);
    }

    [Fact]
    public void FeatureVerdictSurfaced_AcceptsNullDegradation()
    {
        var p = MissionSpaceAuditPayloads.FeatureVerdictSurfaced(
            "feature.x", "Available", null, "abc123");
        Assert.Null(p.Body["degradation_kind"]);
    }
}
