using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// Append-only log of <see cref="SignatureRevocation"/> events. Per
/// ADR 0054 amendments A4 + A5, concurrent revocations under the
/// AP/CRDT model merge by last-revocation-wins (latest
/// <see cref="SignatureRevocation.RevokedAt"/> in partial order; ties
/// broken by total-order on <see cref="SignatureRevocation.Id"/>).
/// </summary>
public interface ISignatureRevocationLog
{
    /// <summary>Appends a revocation event. Idempotent on repeated submission of the same <see cref="SignatureRevocation.Id"/>.</summary>
    Task AppendAsync(SignatureRevocation revocation, CancellationToken ct);

    /// <summary>Returns the current validity status of the given signature event (Phase 3 implements the merge rule; Phase 1 ships the contract).</summary>
    Task<SignatureValidityStatus> GetCurrentValidityAsync(SignatureEventId signatureId, CancellationToken ct);

    /// <summary>Streams every revocation entry for the given signature in append order.</summary>
    IAsyncEnumerable<SignatureRevocation> ListRevocationsAsync(SignatureEventId signatureId, CancellationToken ct);
}

/// <summary>The current validity status of a signature event.</summary>
public sealed record SignatureValidityStatus
{
    /// <summary>True when no revocation has been recorded; false when at least one revocation is in force.</summary>
    public required bool IsValid { get; init; }

    /// <summary>The winning revocation that determined the verdict, when <see cref="IsValid"/> is false.</summary>
    public SignatureRevocation? RevokedBy { get; init; }
}
