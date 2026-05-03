using Sunfish.Kernel.Audit;
using Sunfish.Kernel.Signatures.Models;
using Sunfish.Kernel.Signatures.Services;

namespace Sunfish.Kernel.Signatures.Audit;

/// <summary>
/// W#21 Phase 5 audit-payload factory per ADR 0054 + ADR 0049. Builds
/// <see cref="AuditPayload"/> bodies for the 5 signature lifecycle event
/// types. Mirrors W#19 / W#22 / W#27 factory conventions.
/// </summary>
internal static class SignatureAuditPayloadFactory
{
    /// <summary>Body for <see cref="AuditEventType.SignatureCaptured"/>.</summary>
    public static AuditPayload SignatureCaptured(SignatureEvent ev) =>
        new(new Dictionary<string, object?>
        {
            ["signature_event_id"] = ev.Id.Value,
            ["tenant"] = ev.Tenant.Value,
            ["signer"] = ev.Signer.Value,
            ["consent_id"] = ev.Consent.Value,
            ["document_hash"] = ev.DocumentHash.ToString(),
            ["scope_count"] = ev.Scope.Count,
            ["envelope_algorithm"] = ev.Envelope.Algorithm,
            ["clock_source"] = ev.Quality.ClockSource.ToString(),
            ["stroke_fidelity"] = ev.Quality.StrokeFidelity.ToString(),
            ["signed_at"] = ev.SignedAt.ToString("O"),
        });

    /// <summary>Body for <see cref="AuditEventType.SignatureRevoked"/>.</summary>
    public static AuditPayload SignatureRevoked(SignatureRevocation revocation) =>
        new(new Dictionary<string, object?>
        {
            ["revocation_event_id"] = revocation.Id.Value,
            ["signature_event_id"] = revocation.SignatureEvent.Value,
            ["revoked_at"] = revocation.RevokedAt.ToString("O"),
            ["revoked_by"] = revocation.RevokedBy.Value,
            ["reason"] = revocation.Reason.ToString(),
            ["note"] = revocation.Note,
        });

    /// <summary>Body for <see cref="AuditEventType.SignatureValidityProjected"/>.</summary>
    public static AuditPayload SignatureValidityProjected(SignatureEventId signatureId, SignatureValidityStatus status) =>
        new(new Dictionary<string, object?>
        {
            ["signature_event_id"] = signatureId.Value,
            ["is_valid"] = status.IsValid,
            ["revoked_by_event_id"] = status.RevokedBy?.Id.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.ConsentRecorded"/>.</summary>
    public static AuditPayload ConsentRecorded(ConsentRecord consent) =>
        new(new Dictionary<string, object?>
        {
            ["consent_id"] = consent.Id.Value,
            ["principal"] = consent.Principal.Value,
            ["tenant"] = consent.Tenant.Value,
            ["given_at"] = consent.GivenAt.ToString("O"),
            ["expires_at"] = consent.ExpiresAt?.ToString("O"),
            ["given_from_ip"] = consent.GivenFromIp,
        });

    /// <summary>Body for <see cref="AuditEventType.ConsentRevoked"/>.</summary>
    public static AuditPayload ConsentRevoked(ConsentRecordId consentId, DateTimeOffset revokedAt) =>
        new(new Dictionary<string, object?>
        {
            ["consent_id"] = consentId.Value,
            ["revoked_at"] = revokedAt.ToString("O"),
        });
}
