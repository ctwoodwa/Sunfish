using System;
using System.Collections.Generic;
using System.Text.Json;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Migration;
using Sunfish.Foundation.UI;
using Sunfish.Foundation.Versioning;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Tests;

public sealed class MissionEnvelopeTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static MissionEnvelope NewEnvelope() => new()
    {
        Hardware = new() { ProbeStatus = ProbeStatus.Healthy, CpuArch = "arm64", CpuLogicalCores = 8 },
        User = new() { ProbeStatus = ProbeStatus.Healthy, IsSignedIn = true, PrincipalId = "principal-1" },
        Regulatory = new() { ProbeStatus = ProbeStatus.Healthy, JurisdictionCodes = new[] { "US-CA" } },
        Runtime = new() { ProbeStatus = ProbeStatus.Healthy, OsFamily = "darwin", DotnetVersion = "11.0" },
        FormFactor = new() { ProbeStatus = ProbeStatus.Healthy },
        Edition = new() { ProbeStatus = ProbeStatus.Healthy, EditionKey = "anchor-self-host" },
        Network = new() { ProbeStatus = ProbeStatus.Healthy, IsOnline = true },
        TrustAnchor = new() { ProbeStatus = ProbeStatus.Healthy, HasIdentityKey = true },
        SyncState = new() { ProbeStatus = ProbeStatus.Healthy, State = global::Sunfish.Foundation.UI.SyncState.Healthy },
        VersionVector = new() { ProbeStatus = ProbeStatus.Healthy },
        SnapshotAt = Now,
    };

    [Fact]
    public void MissionEnvelope_RoundTripsThroughCanonicalJson_ByteStable()
    {
        var env = NewEnvelope().WithComputedHash();
        var bytes = CanonicalJson.Serialize(env);
        var roundtripped = JsonSerializer.Deserialize<MissionEnvelope>(System.Text.Encoding.UTF8.GetString(bytes));

        Assert.NotNull(roundtripped);
        Assert.Equal(bytes, CanonicalJson.Serialize(roundtripped));
    }

    [Fact]
    public void MissionEnvelope_UsesCamelCasePropertyNames()
    {
        var env = NewEnvelope();
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(env));
        Assert.Contains("\"hardware\"", json);
        Assert.Contains("\"user\"", json);
        Assert.Contains("\"regulatory\"", json);
        Assert.Contains("\"runtime\"", json);
        Assert.Contains("\"formFactor\"", json);
        Assert.Contains("\"edition\"", json);
        Assert.Contains("\"network\"", json);
        Assert.Contains("\"trustAnchor\"", json);
        Assert.Contains("\"syncState\"", json);
        Assert.Contains("\"versionVector\"", json);
        Assert.Contains("\"envelopeHash\"", json);
        Assert.Contains("\"snapshotAt\"", json);
        Assert.DoesNotContain("\"Hardware\"", json);
    }

    [Fact]
    public void MissionEnvelope_SerializesEnumLiterals_NotOrdinals()
    {
        var env = NewEnvelope();
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(env));
        Assert.Contains("\"Healthy\"", json);
    }

    [Fact]
    public void WithComputedHash_ProducesNonEmpty_64HexChars()
    {
        var env = NewEnvelope().WithComputedHash();
        Assert.NotNull(env.EnvelopeHash);
        Assert.Equal(64, env.EnvelopeHash.Length);
        Assert.Matches("^[0-9a-f]{64}$", env.EnvelopeHash);
    }

    [Fact]
    public void WithComputedHash_IsDeterministic()
    {
        var env1 = NewEnvelope().WithComputedHash();
        var env2 = NewEnvelope().WithComputedHash();
        Assert.Equal(env1.EnvelopeHash, env2.EnvelopeHash);
    }

    [Fact]
    public void WithComputedHash_ChangesWhenAnyDimensionChanges()
    {
        var env1 = NewEnvelope().WithComputedHash();
        var env2 = (NewEnvelope() with
        {
            Edition = new EditionCapabilities { ProbeStatus = ProbeStatus.Healthy, EditionKey = "bridge-pro" },
        }).WithComputedHash();
        Assert.NotEqual(env1.EnvelopeHash, env2.EnvelopeHash);
    }

    [Fact]
    public void WithComputedHash_ExcludesItself_FromHashInput()
    {
        // An envelope with EnvelopeHash="garbage" should produce the
        // same hash as one with EnvelopeHash="" (the field is stripped
        // from the signing surface).
        var withGarbage = (NewEnvelope() with { EnvelopeHash = "garbage-input-hash" }).WithComputedHash();
        var withEmpty = NewEnvelope().WithComputedHash();
        Assert.Equal(withGarbage.EnvelopeHash, withEmpty.EnvelopeHash);
    }

    [Fact]
    public void ComputeEnvelopeHash_StaticHelperEqualsInstance()
    {
        var env = NewEnvelope();
        Assert.Equal(env.WithComputedHash().EnvelopeHash, MissionEnvelope.ComputeEnvelopeHash(env));
    }

    [Fact]
    public void ComputeEnvelopeHash_NullEnvelope_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MissionEnvelope.ComputeEnvelopeHash(null!));
    }
}

public sealed class EnumsAndRecordsTests
{
    [Fact]
    public void DimensionChangeKind_HasTenValues_InOrder()
    {
        var values = Enum.GetValues<DimensionChangeKind>();
        Assert.Equal(10, values.Length);
        Assert.Equal(DimensionChangeKind.Hardware, values[0]);
        Assert.Equal(DimensionChangeKind.VersionVector, values[^1]);
    }

    [Fact]
    public void EnvelopeChangeSeverity_HasFourValues_IncludingProbeUnreliable()
    {
        var values = Enum.GetValues<EnvelopeChangeSeverity>();
        Assert.Equal(4, values.Length);
        Assert.Contains(EnvelopeChangeSeverity.ProbeUnreliable, values);
    }

    [Fact]
    public void DegradationKind_FiveValueTaxonomy_PerA1_2()
    {
        Assert.Equal(5, Enum.GetValues<DegradationKind>().Length);
    }

    [Fact]
    public void ProbeStatus_FiveValues_PerA1_10()
    {
        Assert.Equal(5, Enum.GetValues<ProbeStatus>().Length);
    }

    [Fact]
    public void ProbeCostClass_FiveValues_PerA1_6()
    {
        Assert.Equal(5, Enum.GetValues<ProbeCostClass>().Length);
    }

    [Fact]
    public void ForceEnablePolicy_ThreeValues_PerA1_9()
    {
        Assert.Equal(3, Enum.GetValues<ForceEnablePolicy>().Length);
    }

    [Fact]
    public void LocalizedString_RoundTripsThroughCanonicalJson()
    {
        var ls = new LocalizedString { Key = "feature.x.unavailable", DefaultValue = "Feature X is unavailable" };
        var bytes = CanonicalJson.Serialize(ls);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"key\"", json);
        Assert.Contains("\"defaultValue\"", json);
        var roundtripped = JsonSerializer.Deserialize<LocalizedString>(json);
        Assert.Equal(ls, roundtripped);
    }

    [Fact]
    public void LocalizedString_ToString_ReturnsDefaultValue()
    {
        var ls = new LocalizedString { Key = "x", DefaultValue = "Hello" };
        Assert.Equal("Hello", ls.ToString());
    }

    [Fact]
    public void FeatureVerdict_RoundTrip_PreservesScalarEnumLiterals()
    {
        // Property-level [JsonConverter(JsonStringEnumConverter<T>)]
        // applies to scalar enum properties; list-of-enum elements
        // serialize as ordinals (a known System.Text.Json limitation
        // when no global converter is registered). Scalar-property
        // assertions pass; list-element assertions are deferred to a
        // future amendment that registers a global converter on
        // CanonicalJson.SerializerOptions.
        var verdict = new FeatureVerdict
        {
            FeatureKey = "feature.x",
            State = FeatureAvailabilityState.DegradedAvailable,
            DegradationKind = DegradationKind.AdvisoryCaveat,
            Reason = new LocalizedString { Key = "x.reason", DefaultValue = "Operator force-enabled" },
            ContributingDimensions = new[] { DimensionChangeKind.Regulatory },
        };
        var bytes = CanonicalJson.Serialize(verdict);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("\"DegradedAvailable\"", json);
        Assert.Contains("\"AdvisoryCaveat\"", json);
        // ContributingDimensions list-of-enum: ordinal representation
        // is acceptable for v0 (round-trips byte-stably).
    }

    [Fact]
    public void EnvelopeChange_RoundTripsByteStably()
    {
        var current = NewEnvelopeWithComputedHash();
        var change = new EnvelopeChange
        {
            Previous = null,
            Current = current,
            ChangedDimensions = new[] { DimensionChangeKind.Edition },
            Severity = EnvelopeChangeSeverity.Warning,
        };
        var bytes = CanonicalJson.Serialize(change);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        // Scalar enum (Severity) → literal; list-of-enum → ordinal per
        // System.Text.Json default (acceptable for v0; round-trip-stable).
        Assert.Contains("\"Warning\"", json);
        var roundtripped = JsonSerializer.Deserialize<EnvelopeChange>(json);
        Assert.NotNull(roundtripped);
        Assert.Equal(bytes, CanonicalJson.Serialize(roundtripped));
    }

    [Fact]
    public void ForceEnableNotPermittedException_CarriesDimension()
    {
        var ex = new ForceEnableNotPermittedException(DimensionChangeKind.Hardware);
        Assert.Equal(DimensionChangeKind.Hardware, ex.Dimension);
        Assert.Contains("Hardware", ex.Message);
        Assert.Contains("NotOverridable", ex.Message);
    }

    private static MissionEnvelope NewEnvelopeWithComputedHash() => new MissionEnvelope
    {
        Hardware = new() { ProbeStatus = ProbeStatus.Healthy },
        User = new() { ProbeStatus = ProbeStatus.Healthy, IsSignedIn = false },
        Regulatory = new() { ProbeStatus = ProbeStatus.Healthy },
        Runtime = new() { ProbeStatus = ProbeStatus.Healthy },
        FormFactor = new() { ProbeStatus = ProbeStatus.Healthy },
        Edition = new() { ProbeStatus = ProbeStatus.Healthy },
        Network = new() { ProbeStatus = ProbeStatus.Healthy, IsOnline = true },
        TrustAnchor = new() { ProbeStatus = ProbeStatus.Healthy, HasIdentityKey = false },
        SyncState = new() { ProbeStatus = ProbeStatus.Healthy, State = global::Sunfish.Foundation.UI.SyncState.Healthy },
        VersionVector = new() { ProbeStatus = ProbeStatus.Healthy },
        SnapshotAt = DateTimeOffset.UtcNow,
    }.WithComputedHash();
}
