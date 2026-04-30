using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Kernel.Signatures.Models;

/// <summary>
/// UETA / E-SIGN consent record — the prerequisite for legally-binding
/// electronic signature capture per ADR 0054 §"Consent prerequisite".
/// Without a current <see cref="ConsentRecord"/> for the signing
/// principal, capture MUST be refused.
/// </summary>
public sealed record ConsentRecord
{
    /// <summary>Stable identifier; referenced by every <see cref="SignatureEvent"/> captured under this consent.</summary>
    public required ConsentRecordId Id { get; init; }

    /// <summary>The principal whose consent is recorded (typically a tenant, applicant, or operator).</summary>
    public required ActorId Principal { get; init; }

    /// <summary>Owning tenant.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>UTC timestamp consent was given.</summary>
    public required DateTimeOffset GivenAt { get; init; }

    /// <summary>Optional UTC expiry; consent must be re-affirmed past this point. Null = no expiry.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>UTC timestamp of revocation; null while consent is current.</summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>Verbatim text the principal affirmed (e.g. "I agree to electronic signatures…"); preserved for audit.</summary>
    public required string AffirmationText { get; init; }

    /// <summary>Source IP captured at consent time, when available.</summary>
    public string? GivenFromIp { get; init; }

    /// <summary>Whether this consent is currently in force (not expired + not revoked).</summary>
    public bool IsCurrentAt(DateTimeOffset now) =>
        RevokedAt is null && (ExpiresAt is null || now < ExpiresAt.Value);
}
