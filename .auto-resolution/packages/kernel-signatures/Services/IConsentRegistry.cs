using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// UETA/E-SIGN consent registry per ADR 0054 §"Consent prerequisite".
/// Every signature capture MUST verify that the signing principal has
/// a current consent record; capture is refused otherwise.
/// </summary>
public interface IConsentRegistry
{
    /// <summary>Records a new <see cref="ConsentRecord"/> for the given principal.</summary>
    Task<ConsentRecord> RecordAsync(ConsentRecord consent, CancellationToken ct);

    /// <summary>Returns the most-recent current consent for the principal, or null when none is in force.</summary>
    Task<ConsentRecord?> GetCurrentAsync(TenantId tenant, ActorId principal, DateTimeOffset asOf, CancellationToken ct);

    /// <summary>Marks an existing consent as revoked at <paramref name="revokedAt"/>.</summary>
    Task RevokeAsync(ConsentRecordId id, DateTimeOffset revokedAt, CancellationToken ct);
}
