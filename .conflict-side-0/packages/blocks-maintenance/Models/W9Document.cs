using System;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Signatures;
using Sunfish.Foundation.Recovery;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// W-9 document captured from a vendor during onboarding (W#18 Phase 4
/// per ADR 0058). The TIN (SSN or EIN) is stored as an
/// <see cref="EncryptedField"/> per ADR 0046-A2/A4/A5; decryption is
/// capability-gated through <see cref="Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor"/>
/// and audited at every read.
/// </summary>
/// <remarks>
/// <para>
/// <b>SignatureRef nullable in Phase 4.</b> The vendor-acknowledgment
/// signature flow lands in W#18 Phase 5, which depends on W#21
/// kernel-signatures. Phase 4 introduces this field as nullable so
/// W-9 records can be stored before signature capture is wired;
/// Phase 5 will tighten the invariant once the substrate is available.
/// </para>
/// </remarks>
public sealed record W9Document
{
    /// <summary>Unique identifier for this W-9 record.</summary>
    public required W9DocumentId Id { get; init; }

    /// <summary>The vendor this W-9 belongs to.</summary>
    public required VendorId Vendor { get; init; }

    /// <summary>The legal name on the W-9 form.</summary>
    public required string LegalName { get; init; }

    /// <summary>"Doing business as" trade name when distinct from the legal name.</summary>
    public string? DbaName { get; init; }

    /// <summary>IRS federal-tax classification check-box.</summary>
    public required W9TaxClassification TaxClassification { get; init; }

    /// <summary>
    /// SSN or EIN encrypted under the per-tenant DEK.
    /// Plaintext is never stored; reads go through
    /// <see cref="Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor"/>.
    /// </summary>
    public required EncryptedField TinEncrypted { get; init; }

    /// <summary>The address printed on the W-9 form.</summary>
    public required W9MailingAddress Address { get; init; }

    /// <summary>
    /// Reference to the vendor's electronic-signature acknowledgment
    /// (W#21 kernel-signatures). Nullable in Phase 4 — populated by
    /// Phase 5 once the signature-capture substrate is wired.
    /// </summary>
    public SignatureEventRef? SignatureRef { get; init; }

    /// <summary>When the W-9 was received from the vendor.</summary>
    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>When the W-9 was operator-verified (null until verified).</summary>
    public DateTimeOffset? VerifiedAt { get; init; }

    /// <summary>Operator who verified the W-9 (null until verified).</summary>
    public ActorId? VerifiedBy { get; init; }
}
