using System.Text.Json;
using Xunit;

namespace Sunfish.Foundation.Wayfinder.Tests;

/// <summary>
/// Phase 1 §3 — enum canonical-identifier round-trip tests. Cohort convention
/// (W#34 / W#37 / W#40) ships <see cref="JsonStringEnumConverter"/> on every
/// enum so canonical-JSON serialization yields signature-stable strings.
/// </summary>
public sealed class StandingOrderEnumTests
{
    [Theory]
    [InlineData(StandingOrderScope.User, "\"User\"")]
    [InlineData(StandingOrderScope.Tenant, "\"Tenant\"")]
    [InlineData(StandingOrderScope.Platform, "\"Platform\"")]
    [InlineData(StandingOrderScope.Integration, "\"Integration\"")]
    [InlineData(StandingOrderScope.Security, "\"Security\"")]
    public void StandingOrderScope_RoundTripsAsNamedString(StandingOrderScope value, string expectedJson)
    {
        var json = JsonSerializer.Serialize(value);
        Assert.Equal(expectedJson, json);

        var roundtripped = JsonSerializer.Deserialize<StandingOrderScope>(json);
        Assert.Equal(value, roundtripped);
    }

    [Theory]
    [InlineData(StandingOrderState.Issued, "\"Issued\"")]
    [InlineData(StandingOrderState.Validated, "\"Validated\"")]
    [InlineData(StandingOrderState.Applied, "\"Applied\"")]
    [InlineData(StandingOrderState.Rescinded, "\"Rescinded\"")]
    [InlineData(StandingOrderState.Rejected, "\"Rejected\"")]
    [InlineData(StandingOrderState.Conflicted, "\"Conflicted\"")]
    public void StandingOrderState_RoundTripsAsNamedString(StandingOrderState value, string expectedJson)
    {
        var json = JsonSerializer.Serialize(value);
        Assert.Equal(expectedJson, json);

        var roundtripped = JsonSerializer.Deserialize<StandingOrderState>(json);
        Assert.Equal(value, roundtripped);
    }

    [Theory]
    [InlineData(StandingOrderValidatorPriority.Schema, "\"Schema\"", 100)]
    [InlineData(StandingOrderValidatorPriority.Policy, "\"Policy\"", 200)]
    [InlineData(StandingOrderValidatorPriority.Authority, "\"Authority\"", 300)]
    [InlineData(StandingOrderValidatorPriority.Conflict, "\"Conflict\"", 400)]
    public void StandingOrderValidatorPriority_RoundTripsAsNamedString_AndExposesNumericSlot(
        StandingOrderValidatorPriority value, string expectedJson, int expectedNumeric)
    {
        var json = JsonSerializer.Serialize(value);
        Assert.Equal(expectedJson, json);

        var roundtripped = JsonSerializer.Deserialize<StandingOrderValidatorPriority>(json);
        Assert.Equal(value, roundtripped);

        // Numeric value reservation is part of the contract per ADR 0065 §3.
        Assert.Equal(expectedNumeric, (int)value);
    }
}
