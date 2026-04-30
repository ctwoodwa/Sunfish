namespace Sunfish.Kernel.Signatures.Models;

/// <summary>Pen-stroke fidelity captured by the device — drives storage-cost decisions + biometric replay capability.</summary>
public enum PenStrokeFidelity
{
    /// <summary>No pen-stroke captured (typed signature, click-to-sign).</summary>
    None,

    /// <summary>Low-resolution path (≤30 fps; no pressure data).</summary>
    LowResolution,

    /// <summary>High-resolution path with pressure + tilt (e.g. Apple Pencil; ≥60 fps).</summary>
    HighResolution,
}

/// <summary>Source of the timestamp recorded with the signature event.</summary>
public enum ClockSource
{
    /// <summary>Device's local clock — lowest assurance.</summary>
    DeviceClock,

    /// <summary>Local clock with NTP-synchronized offset within the past hour.</summary>
    NtpVerified,

    /// <summary>Trusted timestamp from an RFC 3161 timestamping authority.</summary>
    TrustedTimestamp,

    /// <summary>Server-side timestamp from the signing-relay endpoint (most common production path).</summary>
    ServerSide,
}

/// <summary>Operator-supplied reason for revoking a signature event.</summary>
public enum RevocationReason
{
    /// <summary>Signature was captured under duress or via impersonation.</summary>
    Coerced,

    /// <summary>Document content was found to differ from what the signer agreed to.</summary>
    DocumentTampered,

    /// <summary>Revoked at the signer's request (cooling-off period; pre-finalization).</summary>
    SignerRequest,

    /// <summary>Operator-side error (wrong document, wrong scope).</summary>
    OperatorError,

    /// <summary>Other reason; free-text note required on the revocation entry.</summary>
    Other,
}
