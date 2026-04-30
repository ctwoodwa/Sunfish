using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Taxonomy.Models;

namespace Sunfish.Kernel.Signatures.Models;

/// <summary>
/// Top-level signature-capture record per ADR 0054. Binds a signing
/// principal + consent + canonicalized document hash + algorithm-agility
/// envelope + capture-quality metadata, and references an optional
/// pen-stroke blob for high-resolution biometric replay.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope (per amendment A7):</b> <see cref="Scope"/> references
/// <c>Sunfish.Signature.Scopes@1.0.0</c> taxonomy nodes (W#31), one or
/// more per signature. Multiple scopes capture overlapping legal effect
/// (e.g. a single ink stroke that simultaneously executes a lease + a
/// move-in checklist).
/// </para>
/// <para>
/// <b>Algorithm agility (per amendment A2):</b>
/// <see cref="Envelope"/> wraps the raw signature bytes + algorithm tag
/// per <see cref="SignatureEnvelope"/>. The Phase 0 envelope stub
/// accepts any algorithm string; ADR 0004 Stage 06 will tighten to a
/// registry.
/// </para>
/// <para>
/// <b>Revocation:</b> revoked signature events stay in storage for
/// audit trail; current-validity is computed by projecting the
/// append-only <c>ISignatureRevocationLog</c>.
/// </para>
/// </remarks>
public sealed record SignatureEvent
{
    /// <summary>Stable identifier.</summary>
    public required SignatureEventId Id { get; init; }

    /// <summary>Owning tenant.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>The principal who signed.</summary>
    public required ActorId Signer { get; init; }

    /// <summary>UETA/E-SIGN consent under which this signature was captured. MUST be current at <see cref="SignedAt"/>.</summary>
    public required ConsentRecordId Consent { get; init; }

    /// <summary>Hash of the canonicalized document bytes (per amendment A1).</summary>
    public required ContentHash DocumentHash { get; init; }

    /// <summary>One or more taxonomy classifications binding the signature's legal effect (per amendment A7; references <c>Sunfish.Signature.Scopes@1.0.0</c>).</summary>
    public required IReadOnlyList<TaxonomyClassification> Scope { get; init; }

    /// <summary>Algorithm-agility container holding the cryptographic signature bytes (per amendment A2).</summary>
    public required SignatureEnvelope Envelope { get; init; }

    /// <summary>UTC timestamp of signing — see <see cref="CaptureQuality.ClockSource"/> for assurance level.</summary>
    public required DateTimeOffset SignedAt { get; init; }

    /// <summary>Quality + assurance metadata captured at signing time.</summary>
    public required CaptureQuality Quality { get; init; }

    /// <summary>Optional pen-stroke blob reference (high-fidelity capture).</summary>
    public PenStrokeBlobRef? PenStroke { get; init; }

    /// <summary>Optional geolocation captured with the signature event.</summary>
    public Geolocation? Location { get; init; }

    /// <summary>Optional device-attestation payload (Apple App Attest / Google Play Integrity).</summary>
    public DeviceAttestation? Attestation { get; init; }
}
