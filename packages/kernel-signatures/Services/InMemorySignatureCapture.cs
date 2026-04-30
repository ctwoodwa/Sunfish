using System.Collections.Concurrent;
using Sunfish.Kernel.Signatures.Models;

namespace Sunfish.Kernel.Signatures.Services;

/// <summary>
/// In-memory <see cref="ISignatureCapture"/> for tests + non-production
/// hosts. Validates the consent gate (UETA/E-SIGN) + persists captured
/// events to a thread-safe dictionary. Native PencilKit + CryptoKit
/// integration lands in W#23 (iOS Field-Capture App).
/// </summary>
public sealed class InMemorySignatureCapture : ISignatureCapture
{
    private readonly IConsentRegistry _consents;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<SignatureEventId, SignatureEvent> _events = new();

    /// <summary>Creates the in-memory capturer with the supplied consent registry + clock.</summary>
    public InMemorySignatureCapture(IConsentRegistry consents, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(consents);
        _consents = consents;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<SignatureEvent> CaptureAsync(SignatureCaptureRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Scope is null || request.Scope.Count == 0)
        {
            throw new ArgumentException("SignatureCaptureRequest.Scope must contain at least one taxonomy classification.", nameof(request));
        }

        var now = _time.GetUtcNow();
        var consent = await _consents.GetCurrentAsync(request.Tenant, request.Signer, now, ct).ConfigureAwait(false);
        if (consent is null)
        {
            throw new InvalidOperationException(
                $"No current UETA/E-SIGN consent for principal '{request.Signer.Value}' in tenant '{request.Tenant.Value}'. Capture refused.");
        }
        if (consent.Id != request.Consent)
        {
            throw new InvalidOperationException(
                $"SignatureCaptureRequest.Consent '{request.Consent.Value}' does not match the current consent '{consent.Id.Value}' for principal '{request.Signer.Value}'.");
        }

        var captured = new SignatureEvent
        {
            Id = new SignatureEventId(Guid.NewGuid()),
            Tenant = request.Tenant,
            Signer = request.Signer,
            Consent = request.Consent,
            DocumentHash = request.DocumentHash,
            Scope = request.Scope,
            Envelope = request.Envelope,
            SignedAt = now,
            Quality = request.Quality,
            PenStroke = request.PenStroke,
            Location = request.Location,
            Attestation = request.Attestation,
        };
        _events[captured.Id] = captured;
        return captured;
    }

    /// <inheritdoc />
    public Task<SignatureEvent?> GetAsync(SignatureEventId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _events.TryGetValue(id, out var ev);
        return Task.FromResult<SignatureEvent?>(ev);
    }
}
