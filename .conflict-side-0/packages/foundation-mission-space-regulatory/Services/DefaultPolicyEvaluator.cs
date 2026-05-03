using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.MissionSpace.Regulatory.Audit;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>
/// Reference <see cref="IPolicyEvaluator"/> per ADR 0064-A1.6 + A1.8. Filters
/// rules by <see cref="JurisdictionalPolicyRule.RelevantFeatures"/> per A1.6
/// pre-filter; empty rule set ⇒ silent <see cref="PolicyVerdictState.Pass"/>.
/// </summary>
/// <remarks>
/// <para>
/// Phase 1 substrate ships the evaluator wired to a host-supplied
/// <see cref="IPolicyRuleSource"/>; the substrate does NOT ship rule
/// content per A1.8. Production hosts wire their counsel-reviewed rule
/// source separately (Phase 3).
/// </para>
/// <para>
/// Per-feature cache is in-process and refreshed on
/// <see cref="InvalidateCache"/>; the host calls
/// <c>InvalidateCache()</c> when the upstream <see cref="JurisdictionProbe"/>
/// transitions probe-status (Healthy → Stale / Failed) per A1.7.
/// </para>
/// </remarks>
public sealed class DefaultPolicyEvaluator : IPolicyEvaluator
{
    private readonly IPolicyRuleSource _ruleSource;
    private readonly TimeProvider _time;
    private readonly RegulatoryAuditEmitter? _emitter;
    private readonly ConcurrentDictionary<string, IReadOnlyList<JurisdictionalPolicyRule>> _filterCache = new();

    /// <summary>Audit-disabled overload (test / bootstrap).</summary>
    public DefaultPolicyEvaluator(IPolicyRuleSource ruleSource, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(ruleSource);
        _ruleSource = ruleSource;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Audit-enabled overload — W#32 both-or-neither contract.</summary>
    public DefaultPolicyEvaluator(
        IPolicyRuleSource ruleSource,
        RegulatoryAuditEmitter emitter,
        TimeProvider? time = null)
        : this(ruleSource, time)
    {
        ArgumentNullException.ThrowIfNull(emitter);
        _emitter = emitter;
    }

    /// <inheritdoc />
    public async ValueTask<PolicyVerdict> EvaluateAsync(string featureKey, JurisdictionProbe probe, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(featureKey);
        ArgumentNullException.ThrowIfNull(probe);
        ct.ThrowIfCancellationRequested();

        // Per A1.6 — RelevantFeatures pre-filter. Null = applies to all.
        var rules = _filterCache.GetOrAdd(featureKey, static (key, ctx) =>
        {
            var allRules = ctx._ruleSource.GetRules();
            var matched = new List<JurisdictionalPolicyRule>(allRules.Count);
            foreach (var rule in allRules)
            {
                if (rule.RelevantFeatures is null || rule.RelevantFeatures.Contains(key))
                {
                    matched.Add(rule);
                }
            }
            return matched;
        }, this);

        PolicyVerdict verdict;
        if (rules.Count == 0)
        {
            // Per A1.8 — empty rule set ⇒ silent Pass; the disclaimer carries
            // the user-facing message that this is NOT compliance.
            verdict = new PolicyVerdict
            {
                State = PolicyVerdictState.Pass,
                Evaluations = Array.Empty<PolicyRuleEvaluation>(),
                EvaluatedAt = _time.GetUtcNow(),
            };
        }
        else
        {
            // Phase 1 substrate evaluates each rule as Pass; Phase 3 ships the
            // counsel-reviewed evaluation strategy.
            var evals = new List<PolicyRuleEvaluation>(rules.Count);
            foreach (var rule in rules)
            {
                evals.Add(new PolicyRuleEvaluation
                {
                    RuleId = rule.RuleId,
                    State = PolicyVerdictState.Pass,
                    EnforcementAction = null,
                    Detail = "Phase 1 substrate evaluation — counsel-reviewed strategy lands in Phase 3.",
                });
            }
            verdict = new PolicyVerdict
            {
                State = PolicyVerdictState.Pass,
                Evaluations = evals,
                EvaluatedAt = _time.GetUtcNow(),
            };
        }

        if (_emitter is not null)
        {
            // PolicyEvaluated — always-on telemetry per A1.7; no dedup.
            await _emitter.EmitAsync(
                AuditEventType.PolicyEvaluated,
                RegulatoryAuditPayloads.PolicyEvaluated(
                    featureKey,
                    probe.JurisdictionCode,
                    verdict.Evaluations.Count,
                    verdict.State.ToString()),
                ct).ConfigureAwait(false);

            // JurisdictionProbedWithLowConfidence — surface low-confidence probes
            // (1-hour dedup keyed on jurisdiction).
            if (probe.Confidence == Confidence.Low)
            {
                await _emitter.EmitWithRuleDedupAsync(
                    $"low-confidence:{probe.JurisdictionCode}",
                    AuditEventType.JurisdictionProbedWithLowConfidence,
                    RegulatoryAuditPayloads.JurisdictionProbedWithLowConfidence(
                        probe.JurisdictionCode,
                        probe.SignalSources.Count),
                    ct).ConfigureAwait(false);
            }
        }

        return verdict;
    }

    /// <inheritdoc />
    public void InvalidateCache()
    {
        _filterCache.Clear();
        _ = _emitter?.EmitAsync(
            AuditEventType.RegulatoryPolicyCacheInvalidated,
            RegulatoryAuditPayloads.RegulatoryPolicyCacheInvalidated("InvalidateCache"),
            CancellationToken.None);
    }
}

/// <summary>Per A1.8 — Phase 1 substrate's empty-rule-set source. Silent-pass behavior is intentional.</summary>
public sealed class EmptyPolicyRuleSource : IPolicyRuleSource
{
    /// <inheritdoc />
    public IReadOnlyList<JurisdictionalPolicyRule> GetRules() => Array.Empty<JurisdictionalPolicyRule>();
}
