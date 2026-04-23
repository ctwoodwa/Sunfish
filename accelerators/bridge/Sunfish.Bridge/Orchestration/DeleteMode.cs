namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Disk-disposal policy for
/// <see cref="ITenantProcessSupervisor.StopAndEraseAsync"/>. Chosen by the
/// operator per-cancellation; the default (used when the operator has not
/// explicitly opted into <see cref="SecureWipe"/>) is
/// <see cref="RetainCiphertext"/>.
/// </summary>
/// <remarks>
/// Per <c>_shared/product/wave-5.2-decomposition.md</c> §2.4 "Delete tenant".
/// Because Bridge holds ciphertext-only at rest (paper §17.2), retaining a
/// cancelled tenant's disk in the graveyard is a zero-leakage operation — the
/// operator still cannot decrypt it. The distinction between the two modes
/// is one of DSR / GDPR compliance, not of confidentiality.
/// </remarks>
public enum DeleteMode
{
    /// <summary>
    /// Default. Move <c>{TenantDataRoot}/tenants/{TenantId:D}/</c> to
    /// <c>{TenantDataRoot}/graveyard/{TenantId:D}/{cancelledAt:yyyyMMdd-HHmmss}/</c>
    /// (see <see cref="TenantPaths.GraveyardRoot"/>). Recoverable via a
    /// human-operated restore pathway; SQLCipher DB remains encrypted by a
    /// key the operator cannot synthesize without the tenant's seed.
    /// </summary>
    RetainCiphertext,

    /// <summary>
    /// Recursive delete of <c>{TenantDataRoot}/tenants/{TenantId:D}/</c>.
    /// Irreversible. Chosen when the operator wants to honour a "delete my
    /// data" request without relying on the ciphertext-at-rest invariant.
    /// </summary>
    SecureWipe,
}
