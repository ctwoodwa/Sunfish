using System;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Signatures;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Decrypted-on-demand projection of a <see cref="W9Document"/>
/// returned by <see cref="IW9DocumentService.GetWithDecryptedTinAsync"/>.
/// Carries plaintext TIN bytes; should be short-lived in caller scope
/// and never written to durable storage or logs.
/// </summary>
public sealed record W9DocumentView
{
    public required W9DocumentId Id { get; init; }
    public required VendorId Vendor { get; init; }
    public required string LegalName { get; init; }
    public string? DbaName { get; init; }
    public required W9TaxClassification TaxClassification { get; init; }

    /// <summary>Decrypted TIN bytes (SSN or EIN). Treat as PII; do not log.</summary>
    public required ReadOnlyMemory<byte> Tin { get; init; }

    public required W9MailingAddress Address { get; init; }
    public SignatureEventRef? SignatureRef { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
    public DateTimeOffset? VerifiedAt { get; init; }
    public ActorId? VerifiedBy { get; init; }
}
