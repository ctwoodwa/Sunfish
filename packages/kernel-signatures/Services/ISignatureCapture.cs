using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// Captures a legally-binding <see cref="SignatureEvent"/> per ADR 0054.
/// Phase 1 ships the contract + an InMemory implementation suitable for
/// tests + non-production hosts; native PencilKit + CryptoKit integration
/// (Apple-side) lands in W#23 (iOS Field-Capture App).
/// </summary>
public interface ISignatureCapture
{
    /// <summary>
    /// Records a captured signature. The implementation is responsible
    /// for verifying that <paramref name="request"/>'s consent is
    /// current (UETA/E-SIGN gate) and for persisting the resulting
    /// <see cref="SignatureEvent"/>.
    /// </summary>
    Task<SignatureEvent> CaptureAsync(SignatureCaptureRequest request, CancellationToken ct);

    /// <summary>Reads back a captured signature event by id.</summary>
    Task<SignatureEvent?> GetAsync(SignatureEventId id, CancellationToken ct);
}

/// <summary>Submission shape for <see cref="ISignatureCapture.CaptureAsync"/>.</summary>
public sealed record SignatureCaptureRequest
{
    /// <summary>The owning tenant.</summary>
    public required Sunfish.Foundation.Assets.Common.TenantId Tenant { get; init; }

    /// <summary>The signing principal.</summary>
    public required Sunfish.Foundation.Assets.Common.ActorId Signer { get; init; }

    /// <summary>The current UETA/E-SIGN consent under which this signature is captured.</summary>
    public required ConsentRecordId Consent { get; init; }

    /// <summary>Hash of the canonical document bytes.</summary>
    public required ContentHash DocumentHash { get; init; }

    /// <summary>One or more taxonomy classifications binding the signature's legal effect.</summary>
    public required IReadOnlyList<Sunfish.Foundation.Taxonomy.Models.TaxonomyClassification> Scope { get; init; }

    /// <summary>Algorithm-agility container for the cryptographic signature bytes.</summary>
    public required Sunfish.Foundation.Crypto.SignatureEnvelope Envelope { get; init; }

    /// <summary>Capture-quality metadata.</summary>
    public required CaptureQuality Quality { get; init; }

    /// <summary>Optional pen-stroke blob ref (high-fidelity capture).</summary>
    public PenStrokeBlobRef? PenStroke { get; init; }

    /// <summary>Optional geolocation.</summary>
    public Geolocation? Location { get; init; }

    /// <summary>Optional device-attestation payload.</summary>
    public DeviceAttestation? Attestation { get; init; }
}
