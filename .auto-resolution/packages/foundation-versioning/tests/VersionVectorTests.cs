using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Versioning.Tests;

public sealed class VersionVectorTests
{
    private static VersionVector Sample() => new(
        Kernel: "1.3.0",
        Plugins: new Dictionary<PluginId, PluginVersionVectorEntry>
        {
            [new PluginId("Sunfish.Blocks.PublicListings")] = new("1.0.0", Required: true),
            [new PluginId("Sunfish.Blocks.PropertyEquipment")] = new("1.1.0", Required: false),
        },
        Adapters: new Dictionary<AdapterId, string>
        {
            [new AdapterId("blazor")] = "0.9.0",
        },
        SchemaEpoch: 7u,
        Channel: ChannelKind.Stable,
        InstanceClass: InstanceClassKind.SelfHost);

    [Fact]
    public void CanonicalJson_RoundTrip_PreservesAllFields()
    {
        var original = Sample();
        var bytes = CanonicalJson.Serialize(original);
        var rehydrated = JsonSerializer.Deserialize<VersionVector>(bytes)!;

        Assert.Equal(original.Kernel, rehydrated.Kernel);
        Assert.Equal(original.SchemaEpoch, rehydrated.SchemaEpoch);
        Assert.Equal(original.Channel, rehydrated.Channel);
        Assert.Equal(original.InstanceClass, rehydrated.InstanceClass);
        Assert.Equal(original.Plugins.Count, rehydrated.Plugins.Count);
        Assert.Equal(original.Adapters.Count, rehydrated.Adapters.Count);
    }

    [Fact]
    public void CanonicalJson_Shape_UsesCamelCaseKeys()
    {
        var json = Encoding.UTF8.GetString(CanonicalJson.Serialize(Sample()));

        // Per A7.8: camelCase keys.
        Assert.Contains("\"kernel\":", json);
        Assert.Contains("\"plugins\":", json);
        Assert.Contains("\"adapters\":", json);
        Assert.Contains("\"schemaEpoch\":", json);
        Assert.Contains("\"channel\":", json);
        Assert.Contains("\"instanceClass\":", json);

        // Negative: PascalCase forms must NOT appear.
        Assert.DoesNotContain("\"Kernel\":", json);
        Assert.DoesNotContain("\"SchemaEpoch\":", json);
    }

    [Fact]
    public void CanonicalJson_PluginEntry_SerializesAsObjectWithVersionAndRequired()
    {
        var json = Encoding.UTF8.GetString(CanonicalJson.Serialize(Sample()));

        // Per A7.3 augmentation: each plugin entry is an object carrying
        // version + required (not just a SemVer string).
        Assert.Contains("\"version\":\"1.0.0\"", json);
        Assert.Contains("\"required\":true", json);
    }

    [Fact]
    public void Enums_Serialize_AsLiteralName()
    {
        var json = Encoding.UTF8.GetString(CanonicalJson.Serialize(Sample()));

        // Per A7.6 + A7.8: enum values serialize as their literal name string,
        // not the underlying numeric value.
        Assert.Contains("\"Stable\"", json);
        Assert.Contains("\"SelfHost\"", json);
        Assert.DoesNotContain("\"channel\":0", json);
        Assert.DoesNotContain("\"instanceClass\":0", json);
    }

    [Fact]
    public void InstanceClassKind_HasExactlyTwoValues_PostA7_6()
    {
        // Per A7.6: enum reduced from { SelfHost, ManagedBridge, Embedded }
        // to { SelfHost, ManagedBridge }. Pin the value count.
        var values = System.Enum.GetValues<InstanceClassKind>();

        Assert.Equal(2, values.Length);
        Assert.Contains(InstanceClassKind.SelfHost, values);
        Assert.Contains(InstanceClassKind.ManagedBridge, values);
    }

    [Fact]
    public void FailedRule_HasExactlySixValues_PostA6_2()
    {
        // Pin the FailedRule taxonomy: 6 values per A6.2 rules 1-6.
        var values = System.Enum.GetValues<FailedRule>();

        Assert.Equal(6, values.Length);
        Assert.Contains(FailedRule.KernelSemverWindow, values);
        Assert.Contains(FailedRule.SchemaEpochMismatch, values);
        Assert.Contains(FailedRule.RequiredPluginIntersection, values);
        Assert.Contains(FailedRule.AdapterSetIncompatible, values);
        Assert.Contains(FailedRule.ChannelOrdering, values);
        Assert.Contains(FailedRule.InstanceClassIncompatible, values);
    }

    [Fact]
    public void PluginId_AdapterId_AreOpaqueRecordStructs()
    {
        // Reflection-based pin: the ID types stay as readonly record structs
        // (drift detection — issuer/verifier-style).
        var pluginIdType = typeof(PluginId);
        var adapterIdType = typeof(AdapterId);

        Assert.True(pluginIdType.IsValueType);
        Assert.True(adapterIdType.IsValueType);

        Assert.Equal("plugin-1", new PluginId("plugin-1").ToString());
        Assert.Equal("blazor", new AdapterId("blazor").ToString());
    }

    [Fact]
    public void VersionVectorVerdict_FailedRule_IsNullForCompatibleVerdict()
    {
        var compat = new VersionVectorVerdict(VerdictKind.Compatible, FailedRule: null, FailedRuleDetail: null);
        var incompat = new VersionVectorVerdict(VerdictKind.Incompatible, FailedRule: FailedRule.SchemaEpochMismatch, FailedRuleDetail: "epoch 5 vs 7");

        Assert.Null(compat.FailedRule);
        Assert.Null(compat.FailedRuleDetail);
        Assert.Equal(FailedRule.SchemaEpochMismatch, incompat.FailedRule);
        Assert.NotNull(incompat.FailedRuleDetail);
    }
}
