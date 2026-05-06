using System;

namespace Sunfish.UICore.Conformance;

/// <summary>
/// Documented exception to a WCAG success criterion or EN 301 549
/// chapter per ADR 0077 §7. Surfaces with at least one
/// <see cref="ConformanceException"/> still pass the W#46 a11y CI
/// gate — but only if the exception is declared, justified, and
/// (optionally) carries an expiry for the planned remediation.
/// </summary>
/// <param name="CriterionId">SC / chapter identifier the exception applies to (e.g., <c>"2.5.8"</c>).</param>
/// <param name="Justification">Localized human-readable rationale; rendered in the conformance audit report.</param>
/// <param name="ExpiresAt">Optional planned-remediation deadline; null when the exception is permanent.</param>
public sealed record ConformanceException(
    string CriterionId,
    string Justification,
    DateTimeOffset? ExpiresAt);
