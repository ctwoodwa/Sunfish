using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.UI;
using Xunit;

namespace Sunfish.Foundation.UI.Tests;

public sealed class SyncStateRoundTripTests
{
    private static readonly JsonSerializerOptions LowercaseEnumOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [Theory]
    [InlineData(SyncState.Healthy, "healthy")]
    [InlineData(SyncState.Stale, "stale")]
    [InlineData(SyncState.Offline, "offline")]
    [InlineData(SyncState.Conflict, "conflict")]
    [InlineData(SyncState.Quarantine, "quarantine")]
    public void ToCanonicalIdentifier_RoundTripsToCanonicalWireForm(SyncState state, string expected)
    {
        Assert.Equal(expected, state.ToCanonicalIdentifier());
    }

    [Theory]
    [InlineData("healthy", SyncState.Healthy)]
    [InlineData("stale", SyncState.Stale)]
    [InlineData("offline", SyncState.Offline)]
    [InlineData("conflict", SyncState.Conflict)]
    [InlineData("quarantine", SyncState.Quarantine)]
    public void TryFromCanonicalIdentifier_ParsesCanonicalWireForm(string identifier, SyncState expected)
    {
        Assert.True(SyncStateExtensions.TryFromCanonicalIdentifier(identifier, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData("Healthy")]   // PascalCase rejected (drift detection)
    [InlineData("HEALTHY")]   // ALLCAPS rejected
    [InlineData("hEaLtHy")]   // mixed case rejected
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-state")]
    public void TryFromCanonicalIdentifier_RejectsNonCanonical(string? identifier)
    {
        Assert.False(SyncStateExtensions.TryFromCanonicalIdentifier(identifier, out _));
    }

    [Fact]
    public void ToCanonicalIdentifier_UnknownEnumValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ((SyncState)999).ToCanonicalIdentifier());
    }

    [Fact]
    public void SyncState_RoundTripsThroughJsonSerializer_AsLowercaseStrings()
    {
        var values = new[]
        {
            SyncState.Healthy,
            SyncState.Stale,
            SyncState.Offline,
            SyncState.Conflict,
            SyncState.Quarantine,
        };
        var json = JsonSerializer.Serialize(values, LowercaseEnumOptions);
        Assert.Contains("\"healthy\"", json);
        Assert.Contains("\"stale\"", json);
        Assert.Contains("\"offline\"", json);
        Assert.Contains("\"conflict\"", json);
        Assert.Contains("\"quarantine\"", json);
        // Ordinal-regression guard.
        Assert.DoesNotContain(":0,", json);

        var roundtripped = JsonSerializer.Deserialize<SyncState[]>(json, LowercaseEnumOptions);
        Assert.NotNull(roundtripped);
        Assert.Equal(values, roundtripped);
    }

    [Fact]
    public void SyncState_RoundTripsThroughCanonicalJson_AsLowercaseStrings()
    {
        // Pin the CanonicalJson → wire form. The substrate ADRs cite
        // CanonicalJson.Serialize as the signature-stable encoder.
        var envelope = new SyncStateEnvelope { State = SyncState.Conflict };
        var bytes = CanonicalJson.Serialize(envelope);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"conflict\"", json);
    }

    [Fact]
    public void SyncState_DictionaryKeyContext_WorksAsValue()
    {
        // SyncState is value-typed (enum); keying a dictionary on it
        // is a property-value context (covered by the lowercase enum
        // converter), not the property-name context that PluginId in
        // W#34 P1 needed. This pin documents the surface choice.
        var dict = new Dictionary<SyncState, int>
        {
            [SyncState.Healthy] = 1,
            [SyncState.Stale] = 2,
        };
        Assert.Equal(2, dict.Count);
        Assert.Equal(1, dict[SyncState.Healthy]);
    }

    private sealed record SyncStateEnvelope
    {
        [JsonPropertyName("state")]
        [JsonConverter(typeof(LowercaseEnumConverter))]
        public required SyncState State { get; init; }
    }

    private sealed class LowercaseEnumConverter : JsonStringEnumConverter<SyncState>
    {
        public LowercaseEnumConverter() : base(JsonNamingPolicy.CamelCase) { }
    }
}
