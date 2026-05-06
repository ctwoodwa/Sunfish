using System.Collections.Generic;
using System.Threading;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Engine that evaluates <see cref="TacticalSignal"/> values against
/// every registered <see cref="ITacticalRule"/>. Per ADR 0081 §2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Registration contract:</b>
/// <see cref="RegisterRule"/> MUST throw
/// <see cref="System.InvalidOperationException"/> if a rule with the
/// same <see cref="ITacticalRule.RuleName"/> is already registered,
/// AND MUST throw if called after the first signal has been
/// processed (registration is open-then-closed). Phase 2's
/// implementation enforces the
/// <c>sunfish.*</c> rule-name prefix reservation per ADR 0081 §8.3
/// at registration time.
/// </para>
/// <para>
/// <b>Evaluation contract:</b>
/// <see cref="Evaluate"/> invokes every registered rule in
/// registration order; per-rule exceptions are caught and surfaced
/// via per-rule error-rate audit (ADR 0081 §2.1). All rules are
/// invoked — there is no short-circuit on first match. The combined
/// emitted-alert list is returned in rule-registration order.
/// </para>
/// <para>
/// <b>Streaming contract:</b>
/// <see cref="EvaluateStreamAsync"/> wraps <see cref="Evaluate"/>
/// per signal with async enumeration. Source faults propagate to
/// the caller. Signal ordering is partitioned by
/// <see cref="TacticalSignal.TenantId"/> in the Phase 2
/// implementation per ADR 0081 §2.2.
/// </para>
/// </remarks>
public interface ITacticalRuleEngine
{
    /// <summary>Register a rule. MUST throw on duplicate rule name OR after first signal processed.</summary>
    void RegisterRule(ITacticalRule rule);

    /// <summary>Evaluate one signal against all registered rules; returns combined emitted alerts.</summary>
    IReadOnlyList<TacticalAlert> Evaluate(TacticalSignal signal);

    /// <summary>Stream-evaluate signals; emits each alert as it is produced.</summary>
    IAsyncEnumerable<TacticalAlert> EvaluateStreamAsync(
        IAsyncEnumerable<TacticalSignal> signals,
        CancellationToken ct = default);

    /// <summary>Snapshot of currently registered rules.</summary>
    IReadOnlyList<ITacticalRule> GetRegisteredRules();
}
