using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>
/// Per ADR 0064-A1.6 — evaluates a feature-key + jurisdiction probe against the
/// configured set of <see cref="JurisdictionalPolicyRule"/>s and returns a
/// <see cref="PolicyVerdict"/>.
/// </summary>
public interface IPolicyEvaluator
{
    /// <summary>
    /// Evaluates rules whose <see cref="JurisdictionalPolicyRule.RelevantFeatures"/>
    /// is null or contains <paramref name="featureKey"/>. Phase 1 substrate
    /// returns <see cref="PolicyVerdictState.Pass"/> when the rule set is
    /// empty per A1.8 (substrate is not regulatory-compliant by virtue of
    /// running with no rules).
    /// </summary>
    ValueTask<PolicyVerdict> EvaluateAsync(
        string featureKey,
        JurisdictionProbe probe,
        CancellationToken ct = default);

    /// <summary>Per A1.7 — invalidate the per-feature cache (host signals on probe-status transition).</summary>
    void InvalidateCache();
}

/// <summary>Source of <see cref="JurisdictionalPolicyRule"/>s. Phase 3 wires concrete sources.</summary>
public interface IPolicyRuleSource
{
    /// <summary>Returns the active rule set. Phase 1 substrate ships an empty-rule-set source.</summary>
    IReadOnlyList<JurisdictionalPolicyRule> GetRules();
}
