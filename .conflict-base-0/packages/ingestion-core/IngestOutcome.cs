namespace Sunfish.Ingestion.Core;

/// <summary>
/// Outcome categories for an ingestion call. <see cref="Success"/> is the only terminal success
/// state; all other values indicate a structured failure. See Sunfish Platform spec §7.7.
/// </summary>
public enum IngestOutcome
{
    /// <summary>The input was ingested successfully.</summary>
    Success,

    /// <summary>The input failed validation rules defined by the modality adapter.</summary>
    ValidationFailed,

    /// <summary>The input was recognized as a duplicate of a previously ingested payload.</summary>
    Duplicate,

    /// <summary>The input exceeded the configured size budget for the modality.</summary>
    TooLarge,

    /// <summary>The input was quarantined (e.g. virus scan hit, policy block).</summary>
    Quarantined,

    /// <summary>A required external provider was unavailable (transient).</summary>
    ProviderUnavailable,

    /// <summary>A required external provider returned a non-recoverable error.</summary>
    ProviderFailed,

    /// <summary>The input used a media type or encoding the modality does not support.</summary>
    UnsupportedFormat,

    /// <summary>An unexpected internal error occurred while processing the input.</summary>
    InternalError,

    /// <summary>
    /// The tenant's ingestion quota has been exhausted for the current period. The caller should
    /// back off and retry after the quota window refills.
    /// </summary>
    QuotaExceeded,
}
