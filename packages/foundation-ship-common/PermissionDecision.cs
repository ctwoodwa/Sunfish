using System;
using Sunfish.Foundation.Capabilities;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Result of <c>IPermissionResolver.ResolveAsync</c> per ADR 0077 §2.
/// Sealed two-case discriminated union — <see cref="Granted"/> or
/// <see cref="Denied"/>; the resolver never returns a bare bool because
/// every resolution carries the structured reason + remediation needed by
/// the First-Aid denial UX.
/// </summary>
public abstract record PermissionDecision
{
    private PermissionDecision() { }

    /// <summary>
    /// Permission granted; subject MAY perform the action at the cell.
    /// </summary>
    /// <param name="Role">The role-grant that satisfied the resolution.</param>
    /// <param name="DecidedAt">Wall-clock time the resolver decided.</param>
    /// <param name="Proof">
    /// Optional transferable capability proof — populated only when the caller
    /// passed a non-null <see cref="Resource"/> (resource-scoped action) AND
    /// <see cref="ICapabilityGraph.ExportProofAsync"/> returned a proof. Per
    /// ADR 0077 §2.2: <see cref="CapabilityProof"/> validity is bounded by
    /// the proof's <c>ProvedAt</c> field plus the per-capability validity
    /// window configured in the graph layer (council SC-1: there is no
    /// <c>ExpiresAt</c> on <see cref="CapabilityProof"/>).
    /// </param>
    public sealed record Granted(
        ShipRole Role,
        DateTimeOffset DecidedAt,
        CapabilityProof? Proof) : PermissionDecision;

    /// <summary>
    /// Permission denied; UI MUST surface reason + remediation through the
    /// First-Aid contract per ADR 0077 §2.3.
    /// </summary>
    /// <param name="Reason">Discriminator for the denial cause.</param>
    /// <param name="ReasonDisplay">
    /// Human-readable cause; rendered as the visible message + the
    /// <c>aria-live</c> announcement on initial denial. Localized at adapter
    /// boundary. MUST be non-null + non-empty.
    /// </param>
    /// <param name="Remediation">Suggested next action; never null.</param>
    /// <param name="DecidedAt">Wall-clock time the resolver decided.</param>
    public sealed record Denied(
        DenialReason Reason,
        string ReasonDisplay,
        Remediation Remediation,
        DateTimeOffset DecidedAt) : PermissionDecision;
}
