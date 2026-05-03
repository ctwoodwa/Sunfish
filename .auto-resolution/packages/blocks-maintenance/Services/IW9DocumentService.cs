using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Recovery.Crypto;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Service for managing W-9 documents captured during vendor
/// onboarding (W#18 Phase 4 / ADR 0058). The TIN is encrypted at
/// write-time via <see cref="IFieldEncryptor"/> and decrypted only
/// on demand via <see cref="IFieldDecryptor"/> + an
/// <see cref="IDecryptCapability"/>. Every read of plaintext TIN
/// emits an audit record (per ADR 0046-A4).
/// </summary>
public interface IW9DocumentService
{
    /// <summary>
    /// Persist a new W-9 record, encrypting the TIN bytes under the
    /// caller's tenant DEK. The plaintext TIN is consumed and not
    /// retained anywhere outside the encryption call.
    /// </summary>
    Task<W9Document> CreateAsync(CreateW9DocumentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Fetch a W-9 record by id <em>without</em> decrypting the TIN.
    /// Use this for operator workflows that don't need the plaintext
    /// (e.g., verification status, document listing).
    /// </summary>
    Task<W9Document?> GetAsync(W9DocumentId id, TenantId tenant, CancellationToken ct = default);

    /// <summary>
    /// Fetch a W-9 record and decrypt the TIN. Validates
    /// <paramref name="capability"/>; on success emits a
    /// <c>FieldDecrypted</c> audit record; on rejection emits a
    /// <c>FieldDecryptionDenied</c> audit record and throws
    /// <see cref="FieldDecryptionDeniedException"/>.
    /// </summary>
    Task<W9DocumentView> GetWithDecryptedTinAsync(
        W9DocumentId id,
        IDecryptCapability capability,
        TenantId tenant,
        CancellationToken ct = default);

    /// <summary>
    /// Mark a W-9 as operator-verified. Sets <see cref="W9Document.VerifiedAt"/>
    /// and <see cref="W9Document.VerifiedBy"/>. Emits a
    /// <c>W9DocumentVerified</c> audit record (Phase 7 wiring).
    /// </summary>
    Task<W9Document> VerifyAsync(W9DocumentId id, ActorId verifiedBy, TenantId tenant, CancellationToken ct = default);
}
