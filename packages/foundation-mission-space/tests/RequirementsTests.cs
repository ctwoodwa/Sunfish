using System;
using System.Collections.Generic;
using System.Text.Json;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Migration;
using Sunfish.Foundation.Transport;
using Sunfish.Foundation.UI;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Tests;

public sealed class MinimumSpecRoundTripTests
{
    [Fact]
    public void RoundTrip_BaselineOnly_PreservesPolicyAndDimensions()
    {
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec
            {
                MinMemoryBytes = 16L * 1024 * 1024 * 1024,
                MinCpuLogicalCores = 8,
            },
            Runtime = new RuntimeSpec
            {
                RequiredOsFamilies = new HashSet<string> { "Windows", "MacOS" },
                MinDotnetVersion = "9.0",
            },
        };

        var bytes = CanonicalJson.Serialize(spec);
        var json = System.Text.Encoding.UTF8.GetString(bytes);

        // Scalar SpecPolicy property uses JsonStringEnumConverter — verify literal string form.
        Assert.Contains("\"policy\":\"Required\"", json);
        Assert.Contains("\"minMemoryBytes\"", json);
        Assert.Contains("\"minCpuLogicalCores\":8", json);
        Assert.Contains("\"minDotnetVersion\":\"9.0\"", json);
    }

    [Fact]
    public void RoundTrip_AllTenDimensions_PreservesEachOne()
    {
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Recommended,
            Hardware = new HardwareSpec { MinMemoryBytes = 8L * 1024 * 1024 * 1024 },
            User = new UserSpec { RequiresSignIn = true },
            Regulatory = new RegulatorySpec { AllowedJurisdictions = new HashSet<string> { "US-UT" } },
            Runtime = new RuntimeSpec { MinDotnetVersion = "9.0" },
            FormFactor = new FormFactorSpec { AcceptableFormFactors = new HashSet<FormFactorKind> { FormFactorKind.Desktop } },
            Edition = new EditionSpec { TrialIsAcceptable = false },
            Network = new NetworkSpec { RequiresOnline = false, RequiredTransports = new HashSet<TransportTier> { TransportTier.LocalNetwork } },
            Trust = new TrustSpec { RequiresIdentityKey = true },
            SyncState = new SyncStateSpec { AcceptableStates = new HashSet<SyncState> { SyncState.Healthy, SyncState.Stale } },
            VersionVector = new VersionVectorSpec { MinKernelVersion = "1.0.0", MinSchemaEpoch = 3 },
        };

        var bytes = CanonicalJson.Serialize(spec);
        var json = System.Text.Encoding.UTF8.GetString(bytes);

        // Each per-dimension property surfaces in JSON as its camelCase key.
        Assert.Contains("\"hardware\"", json);
        Assert.Contains("\"user\"", json);
        Assert.Contains("\"regulatory\"", json);
        Assert.Contains("\"runtime\"", json);
        Assert.Contains("\"formFactor\"", json);
        Assert.Contains("\"edition\"", json);
        Assert.Contains("\"network\"", json);
        Assert.Contains("\"trust\"", json);
        Assert.Contains("\"syncState\"", json);
        Assert.Contains("\"versionVector\"", json);
    }

    [Fact]
    public void EnumScalarProperty_SerializesAsStringName()
    {
        var spec = new MinimumSpec { Policy = SpecPolicy.Informational };
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(spec));
        Assert.Contains("\"policy\":\"Informational\"", json);
        Assert.DoesNotContain("\"policy\":2", json);
    }

    [Fact]
    public void NetworkSpec_TransportTierFromW30_StructuralRoundTrip()
    {
        // CanonicalJson does not globally register JsonStringEnumConverter,
        // so collection-element enums (TransportTier here) serialize as
        // numeric ordinals. The wire-format-as-string choice is a host-
        // level option (Phase 5 DI registration). For substrate Phase 1,
        // verify structural round-trip via System.Text.Json deserialization.
        var spec = new NetworkSpec
        {
            RequiredTransports = new HashSet<TransportTier> { TransportTier.LocalNetwork, TransportTier.MeshVpn },
        };
        var bytes = CanonicalJson.Serialize(spec);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        var roundTripped = JsonSerializer.Deserialize<NetworkSpec>(json);
        Assert.NotNull(roundTripped);
        Assert.NotNull(roundTripped!.RequiredTransports);
        Assert.Contains(TransportTier.LocalNetwork, roundTripped.RequiredTransports!);
        Assert.Contains(TransportTier.MeshVpn, roundTripped.RequiredTransports);
    }

    [Fact]
    public void SyncStateSpec_AcceptableStatesFromW37_StructuralRoundTrip()
    {
        var spec = new SyncStateSpec
        {
            AcceptableStates = new HashSet<SyncState> { SyncState.Healthy, SyncState.Stale },
        };
        var bytes = CanonicalJson.Serialize(spec);
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        var roundTripped = JsonSerializer.Deserialize<SyncStateSpec>(json);
        Assert.NotNull(roundTripped);
        Assert.NotNull(roundTripped!.AcceptableStates);
        Assert.Contains(SyncState.Healthy, roundTripped.AcceptableStates!);
        Assert.Contains(SyncState.Stale, roundTripped.AcceptableStates);
    }

    [Fact]
    public void FormFactorSpec_FormFactorKindFromW35_StructuralRoundTrip()
    {
        var spec = new FormFactorSpec
        {
            AcceptableFormFactors = new HashSet<FormFactorKind> { FormFactorKind.Desktop, FormFactorKind.Phone },
        };
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(spec));
        var roundTripped = JsonSerializer.Deserialize<FormFactorSpec>(json);
        Assert.NotNull(roundTripped);
        Assert.Contains(FormFactorKind.Desktop, roundTripped!.AcceptableFormFactors!);
        Assert.Contains(FormFactorKind.Phone, roundTripped.AcceptableFormFactors!);
    }
}

public sealed class PerPlatformSpecTests
{
    [Fact]
    public void RoundTrip_PerPlatformOverrides_PreservesEntries()
    {
        var spec = new MinimumSpec
        {
            Policy = SpecPolicy.Required,
            Hardware = new HardwareSpec { MinMemoryBytes = 16L * 1024 * 1024 * 1024, MinCpuLogicalCores = 8 },
            PerPlatform = new[]
            {
                new PerPlatformSpec
                {
                    Platform = "ios",
                    Trust = new TrustSpec { RequiresIdentityKey = true },
                },
                new PerPlatformSpec
                {
                    Platform = "android",
                    Hardware = new HardwareSpec { MinMemoryBytes = 4L * 1024 * 1024 * 1024 },
                },
            },
        };

        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(spec));
        Assert.Contains("\"perPlatform\"", json);
        Assert.Contains("\"platform\":\"ios\"", json);
        Assert.Contains("\"platform\":\"android\"", json);
    }
}

public sealed class ForwardCompatTests
{
    [Fact]
    public void UnknownFields_RoundTrip_PreservedViaExtensionData()
    {
        // Build JSON that includes a hypothetical future field "experimental".
        const string forwardJson = """
            {
              "policy": "Recommended",
              "hardware": { "minMemoryBytes": 8589934592 },
              "experimental": { "flagX": true }
            }
            """;
        var deserialized = JsonSerializer.Deserialize<MinimumSpec>(forwardJson);
        Assert.NotNull(deserialized);
        Assert.True(deserialized!.UnknownFields.ContainsKey("experimental"));

        // Re-serialize and confirm "experimental" survives.
        var reserialized = JsonSerializer.Serialize(deserialized);
        Assert.Contains("experimental", reserialized);
        Assert.Contains("flagX", reserialized);
    }
}

public sealed class SystemRequirementsResultTests
{
    [Fact]
    public void SystemRequirementsResult_RoundTrip_PreservesVerdictAndDimensions()
    {
        var result = new SystemRequirementsResult
        {
            Overall = OverallVerdict.WarnOnly,
            EvaluatedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            Dimensions = new[]
            {
                new DimensionEvaluation
                {
                    Dimension = DimensionChangeKind.Hardware,
                    Policy = DimensionPolicyKind.Required,
                    Outcome = DimensionPassFail.Pass,
                },
                new DimensionEvaluation
                {
                    Dimension = DimensionChangeKind.Network,
                    Policy = DimensionPolicyKind.Recommended,
                    Outcome = DimensionPassFail.Fail,
                    Detail = "Offline; recommended online.",
                },
            },
        };

        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(result));
        // All three enums on DimensionEvaluation use scalar JsonStringEnumConverter — string form.
        Assert.Contains("\"overall\":\"WarnOnly\"", json);
        Assert.Contains("\"Hardware\"", json);
        Assert.Contains("\"Required\"", json);
        Assert.Contains("\"Pass\"", json);
        Assert.Contains("\"Network\"", json);
        Assert.Contains("\"Recommended\"", json);
        Assert.Contains("\"Fail\"", json);
    }
}

public sealed class AuditEventTypeRequirementsConstantsTests
{
    [Theory]
    [InlineData("MinimumSpecEvaluated")]
    [InlineData("InstallBlocked")]
    [InlineData("InstallWarned")]
    [InlineData("PostInstallSpecRegression")]
    [InlineData("InstallForceEnabled")]
    public void RequirementsAuditEventTypes_AllExist(string expectedValue)
    {
        var fields = typeof(AuditEventType).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var match = false;
        foreach (var f in fields)
        {
            if (f.FieldType == typeof(AuditEventType))
            {
                var v = (AuditEventType)f.GetValue(null)!;
                if (v.Value == expectedValue)
                {
                    match = true;
                    break;
                }
            }
        }
        Assert.True(match, $"AuditEventType.{expectedValue} not found.");
    }
}

public sealed class RequirementsEnumsTests
{
    [Theory]
    [InlineData(SpecPolicy.Required)]
    [InlineData(SpecPolicy.Recommended)]
    [InlineData(SpecPolicy.Informational)]
    public void SpecPolicy_HasThreeValues(SpecPolicy p) => Assert.True(Enum.IsDefined(typeof(SpecPolicy), p));

    [Theory]
    [InlineData(OverallVerdict.Pass)]
    [InlineData(OverallVerdict.WarnOnly)]
    [InlineData(OverallVerdict.Block)]
    public void OverallVerdict_HasThreeValues(OverallVerdict v) => Assert.True(Enum.IsDefined(typeof(OverallVerdict), v));

    [Theory]
    [InlineData(SystemRequirementsRenderMode.PreInstallFullPage)]
    [InlineData(SystemRequirementsRenderMode.PostInstallInlineExplanation)]
    [InlineData(SystemRequirementsRenderMode.PostInstallRegressionBanner)]
    public void SystemRequirementsRenderMode_HasThreeValues(SystemRequirementsRenderMode m) =>
        Assert.True(Enum.IsDefined(typeof(SystemRequirementsRenderMode), m));
}
