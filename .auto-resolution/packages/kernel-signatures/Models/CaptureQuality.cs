namespace Sunfish.Kernel.Signatures.Models;

/// <summary>
/// Quality + assurance metadata captured at signing time — drives
/// downstream evidence weight + biometric-replay eligibility (per
/// ADR 0054 §"Capture quality").
/// </summary>
public sealed record CaptureQuality
{
    /// <summary>Pen-stroke fidelity recorded at the device.</summary>
    public required PenStrokeFidelity StrokeFidelity { get; init; }

    /// <summary>Resolved clock source for <see cref="SignatureEvent.SignedAt"/>.</summary>
    public required ClockSource ClockSource { get; init; }

    /// <summary>True when the device reported a touchscreen / stylus path; false for keyboard-typed signatures.</summary>
    public required bool DeviceTouchAvailable { get; init; }

    /// <summary>True when the signature widget rendered the full document scrollback before signing (UETA "opportunity to review").</summary>
    public required bool DocumentReviewedBeforeSign { get; init; }

    /// <summary>Optional confidence score from a biometric-quality estimator; null when no estimator ran.</summary>
    public double? BiometricConfidence { get; init; }
}

/// <summary>Optional geolocation captured with the signature event (when device permission granted).</summary>
public sealed record Geolocation
{
    /// <summary>WGS-84 latitude in decimal degrees.</summary>
    public required double Latitude { get; init; }

    /// <summary>WGS-84 longitude in decimal degrees.</summary>
    public required double Longitude { get; init; }

    /// <summary>Reported accuracy radius in meters at 95% confidence; null when not reported.</summary>
    public double? AccuracyMeters { get; init; }

    /// <summary>Source — typically <c>"gps"</c>, <c>"ip-geo"</c>, or <c>"manual-entry"</c>.</summary>
    public required string Source { get; init; }
}

/// <summary>
/// Platform-specific device-attestation payload (e.g., Apple App Attest,
/// Google Play Integrity). Phase 1 stores it as opaque bytes + a kind
/// tag; verification semantics live in W#23 (iOS) and downstream
/// platform-specific hand-offs.
/// </summary>
public sealed record DeviceAttestation
{
    /// <summary>Platform identifier — e.g. <c>"apple-app-attest"</c>, <c>"google-play-integrity"</c>, <c>"none"</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Opaque attestation bytes; format defined per <see cref="Kind"/>.</summary>
    public required byte[] Payload { get; init; }

    /// <summary>UTC timestamp the attestation was minted.</summary>
    public required DateTimeOffset AttestedAt { get; init; }
}
