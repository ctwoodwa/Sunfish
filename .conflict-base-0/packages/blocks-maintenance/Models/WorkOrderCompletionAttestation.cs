using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Signatures;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Signature-bound completion attestation for a <see cref="WorkOrder"/>
/// (per ADR 0053). Captured when the operator signs off on vendor-completed
/// work. The bound signature event lives in the kernel-signatures substrate
/// (ADR 0054 Stage 06) and is referenced here via
/// <see cref="SignatureEventRef"/>.
/// </summary>
public sealed record WorkOrderCompletionAttestation
{
    /// <summary>Stable identifier for this attestation.</summary>
    public required WorkOrderCompletionAttestationId Id { get; init; }

    /// <summary>The work order this attestation completes.</summary>
    public required WorkOrderId WorkOrder { get; init; }

    /// <summary>Reference to the signature event that bound this attestation (per ADR 0054).</summary>
    public required SignatureEventRef Signature { get; init; }

    /// <summary>Wall-clock time the attestation was captured.</summary>
    public required DateTimeOffset AttestedAt { get; init; }

    /// <summary>Actor that captured the attestation (typically the operator).</summary>
    public required ActorId Attestor { get; init; }

    /// <summary>Optional free-text notes captured alongside the attestation (e.g., punch-list deltas, vendor caveats).</summary>
    public string? AttestationNotes { get; init; }
}
