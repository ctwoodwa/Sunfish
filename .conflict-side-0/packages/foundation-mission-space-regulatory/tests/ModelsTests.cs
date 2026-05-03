using System;
using System.Collections.Generic;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.MissionSpace.Regulatory.Tests;

public sealed class JurisdictionProbeTests
{
    [Fact]
    public void RoundTrip_ViaCanonicalJson_PreservesAllFields()
    {
        var original = new JurisdictionProbe
        {
            JurisdictionCode = "US-UT",
            Confidence = Confidence.High,
            SignalSources = new[] { "user-declaration", "tenant-config", "ip-geo" },
            ProbedAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
        };

        var bytes = CanonicalJson.Serialize(original);
        var json = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"jurisdictionCode\"", json);
        Assert.Contains("\"confidence\"", json);
        Assert.Contains("\"High\"", json);
        Assert.Contains("\"signalSources\"", json);
        Assert.Contains("\"probedAt\"", json);
    }

    [Fact]
    public void EnumSerializesAsStringName()
    {
        var probe = new JurisdictionProbe
        {
            JurisdictionCode = "EU-DE",
            Confidence = Confidence.Low,
            ProbedAt = DateTimeOffset.UtcNow,
        };
        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(probe));
        Assert.Contains("\"Low\"", json);
        Assert.DoesNotContain("\"confidence\":2", json);
    }
}

public sealed class JurisdictionalPolicyRuleTests
{
    [Fact]
    public void RoundTrip_ViaCanonicalJson_PreservesEnums()
    {
        var rule = new JurisdictionalPolicyRule
        {
            RuleId = "gdpr-art25",
            Regime = RegulatoryRegime.GDPR,
            EvaluationKind = PolicyEvaluationKind.UserConsentRequirement,
            EnforcementAction = PolicyEnforcementAction.PromptUserConsent,
            RelevantFeatures = new HashSet<string> { "feature.x" },
            Description = "Article 25 — privacy by design",
            RuleVersion = "1.0.0",
        };

        var json = System.Text.Encoding.UTF8.GetString(CanonicalJson.Serialize(rule));
        Assert.Contains("\"GDPR\"", json);
        Assert.Contains("\"UserConsentRequirement\"", json);
        Assert.Contains("\"PromptUserConsent\"", json);
        Assert.Contains("\"ruleVersion\"", json);
    }

    [Fact]
    public void RelevantFeatures_NullByDefault_AppliesToAllFeatures()
    {
        var rule = new JurisdictionalPolicyRule
        {
            RuleId = "fha-residency",
            Regime = RegulatoryRegime.FHA,
            EvaluationKind = PolicyEvaluationKind.DataResidencyConstraint,
            EnforcementAction = PolicyEnforcementAction.Block,
            RuleVersion = "1.0.0",
        };
        Assert.Null(rule.RelevantFeatures);
    }
}

public sealed class DefaultRegimeStancesTests
{
    [Fact]
    public void Stances_Has7Entries()
    {
        Assert.Equal(7, DefaultRegimeStances.Stances.Count);
    }

    [Fact]
    public void Stances_HipaaIsCommercialProductOnly()
    {
        var hipaa = Find(RegulatoryRegime.HIPAA);
        Assert.Equal(RegulatoryRegimeStance.CommercialProductOnly, hipaa.Stance);
    }

    [Fact]
    public void Stances_PciDssIsExplicitlyDisclaimedOpenSource_PerA1_13Reframe()
    {
        var pci = Find(RegulatoryRegime.PCI_DSS_v4);
        Assert.Equal(RegulatoryRegimeStance.ExplicitlyDisclaimedOpenSource, pci.Stance);
    }

    [Theory]
    [InlineData(RegulatoryRegime.CCPA)]
    [InlineData(RegulatoryRegime.EU_AI_Act)]
    [InlineData(RegulatoryRegime.FHA)]
    [InlineData(RegulatoryRegime.GDPR)]
    [InlineData(RegulatoryRegime.SOC2)]
    public void Stances_InScopeRegimes(RegulatoryRegime regime)
    {
        var entry = Find(regime);
        Assert.Equal(RegulatoryRegimeStance.InScope, entry.Stance);
    }

    [Fact]
    public void Stances_AllHaveLocalizationKey()
    {
        foreach (var s in DefaultRegimeStances.Stances)
        {
            Assert.False(string.IsNullOrWhiteSpace(s.RationaleKey));
            Assert.StartsWith("regulatory.stance.", s.RationaleKey);
        }
    }

    private static RegimeAcknowledgment Find(RegulatoryRegime r)
    {
        foreach (var s in DefaultRegimeStances.Stances)
        {
            if (s.Regime == r) return s;
        }
        throw new InvalidOperationException($"Regime {r} missing from default stances.");
    }
}

public sealed class AuditEventTypeRegulatoryConstantsTests
{
    [Theory]
    [InlineData("PolicyEvaluated")]
    [InlineData("PolicyEnforcementBlocked")]
    [InlineData("JurisdictionProbedWithLowConfidence")]
    [InlineData("DataResidencyViolation")]
    [InlineData("SanctionsScreeningHit")]
    [InlineData("RegimeAcknowledgmentSurfaced")]
    [InlineData("EuAiActTierClassified")]
    [InlineData("SanctionsAdvisoryOnlyConfigured")]
    [InlineData("RegulatoryRuleContentReloaded")]
    [InlineData("RegulatoryPolicyCacheInvalidated")]
    public void RegulatoryAuditEventTypes_AllExist(string expectedValue)
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
