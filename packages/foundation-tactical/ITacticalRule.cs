namespace Sunfish.Foundation.Tactical;

/// <summary>
/// A rule that evaluates incoming
/// <see cref="TacticalSignal"/> values and emits a
/// <see cref="TacticalAlert"/> when the signal matches the rule's
/// detection logic. Per ADR 0081 §1 + §2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Synchronous + non-IO contract:</b> <see cref="Evaluate"/> MUST
/// be synchronous, MUST NOT perform I/O, and MUST NOT throw on
/// unexpected signal shapes — return <c>false</c> + <c>alert =
/// null</c> instead. Phase 2's
/// <see cref="ITacticalRuleEngine"/> implementation tracks per-rule
/// throw rates; rules that throw &gt; 100 times/minute emit
/// <see cref="Sunfish.Kernel.Audit.AuditEventType.TacticalAuthorizationDenied"/>
/// once per minute per ADR 0081 §2.1.
/// </para>
/// </remarks>
public interface ITacticalRule
{
    /// <summary>Stable rule name; used for routing + audit attribution + threat-trigger template matching.</summary>
    string RuleName { get; }

    /// <summary>Default severity emitted by this rule when no per-alert override applies.</summary>
    AlertSeverity DefaultSeverity { get; }

    /// <summary>Default routing policy for alerts emitted by this rule.</summary>
    AlertRoutingPolicy DefaultRoutingPolicy { get; }

    /// <summary>
    /// Evaluate the signal. Returns <c>true</c> + an emitted alert
    /// when the rule matches; <c>false</c> + null otherwise.
    /// MUST NOT throw — wrap unexpected payload shapes in defensive
    /// branches.
    /// </summary>
    bool Evaluate(TacticalSignal signal, out TacticalAlert? alert);
}
