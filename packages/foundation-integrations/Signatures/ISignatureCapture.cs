using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Integrations.Signatures;

/// <summary>
/// Capture contract for signature events. W#19 Phase 6 introduces this
/// stub mirroring the Money / ThreadId / IPaymentGateway minimal-stub
/// pattern; W#21 Stage 06 (kernel-signatures package per ADR 0054) will
/// replace with the full capture surface (PenStrokeBlobRef +
/// DeviceAttestation + ContentHash + ConsentRecord + UETA/E-SIGN
/// compliance).
/// </summary>
public interface ISignatureCapture
{
    /// <summary>Capture a signature event for the given tenant + actor + content. Returns an opaque <see cref="SignatureEventRef"/>.</summary>
    Task<SignatureEventRef> CaptureAsync(SignatureCaptureRequest request, CancellationToken ct);
}

/// <summary>Capture request.</summary>
/// <param name="Tenant">Owning tenant.</param>
/// <param name="Signer">Actor signing.</param>
/// <param name="Purpose">Free-text purpose label (e.g., <c>WorkOrderCompletionAttestation</c>); ADR 0054 will replace with a typed scope reference.</param>
/// <param name="ContentDigest">Hex-encoded SHA-256 digest of the signed content (ADR 0054 Stage 06 will replace with <c>ContentHash</c>).</param>
public sealed record SignatureCaptureRequest(
    TenantId Tenant,
    ActorId Signer,
    string Purpose,
    string ContentDigest);

/// <summary>
/// Stub <see cref="ISignatureCapture"/>. Mints a fresh
/// <see cref="SignatureEventRef"/> per call and journals it; **not
/// secure** — no actual signing or attestation.
/// </summary>
public sealed class InMemorySignatureCapture : ISignatureCapture
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SignatureCaptureRequest> _journal = new();

    /// <summary>Snapshot for test assertions.</summary>
    public IReadOnlyDictionary<Guid, SignatureCaptureRequest> Journal => _journal;

    /// <inheritdoc />
    public Task<SignatureEventRef> CaptureAsync(SignatureCaptureRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = Guid.NewGuid();
        _journal[id] = request;
        return Task.FromResult(new SignatureEventRef(id));
    }
}
