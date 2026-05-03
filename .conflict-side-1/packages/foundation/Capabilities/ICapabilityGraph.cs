using Sunfish.Foundation.Crypto;

namespace Sunfish.Foundation.Capabilities;

/// <summary>
/// The capability graph: the authoritative record of who holds which capability on which
/// resource. All mutations flow in as signed operations; queries return boolean decisions
/// or exportable proofs.
/// </summary>
/// <remarks>
/// Implementations own the op log, the principal set, and replay-protection state. This
/// interface intentionally does not expose internal storage — consumers only see the log
/// via <see cref="ListOpsAsync"/>.
/// </remarks>
public interface ICapabilityGraph
{
    /// <summary>
    /// Returns <c>true</c> iff <paramref name="subject"/> holds <paramref name="action"/>
    /// on <paramref name="resource"/> at <paramref name="asOf"/>.
    /// </summary>
    /// <param name="subject">The principal to check.</param>
    /// <param name="resource">The resource to check against.</param>
    /// <param name="action">The action being authorized.</param>
    /// <param name="asOf">Point-in-time for the decision (used to honour expirations and revocations).</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<bool> QueryAsync(
        PrincipalId subject,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf,
        CancellationToken ct = default);

    /// <summary>
    /// Applies a signed mutation to the graph. The envelope's signature is verified,
    /// its nonce is checked against the replay-protection set, then the op is applied
    /// under a lock. Returns <see cref="MutationResult.Accepted"/> or
    /// <see cref="MutationResult.Rejected(string)"/>.
    /// </summary>
    ValueTask<MutationResult> MutateAsync(
        SignedOperation<CapabilityOp> op,
        CancellationToken ct = default);

    /// <summary>
    /// Produces a transferable <see cref="CapabilityProof"/> if the subject holds the
    /// capability at <paramref name="asOf"/>; otherwise returns <c>null</c>.
    /// </summary>
    ValueTask<CapabilityProof?> ExportProofAsync(
        PrincipalId subject,
        Resource resource,
        CapabilityAction action,
        DateTimeOffset asOf,
        CancellationToken ct = default);

    /// <summary>
    /// Streams all applied signed operations in insertion order. Callers may use this
    /// for replication, auditing, or rebuilding a secondary view.
    /// </summary>
    IAsyncEnumerable<SignedOperation<CapabilityOp>> ListOpsAsync(CancellationToken ct = default);
}
