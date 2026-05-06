using System.Text.Json.Serialization;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Validation-status discriminator per ADR 0067 §3.9. A provider's
/// status is set by <see cref="IIntegrationProviderValidator.ValidateAsync"/>
/// and persisted in <see cref="IValidationStatusStore"/>; the surface
/// renders a status badge + history per ADR 0067 §3.13.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProviderValidationStatus
{
    /// <summary>Validation has not yet been performed (initial state).</summary>
    Unknown,

    /// <summary>Provider credentials validated successfully.</summary>
    Valid,

    /// <summary>Provider credentials invalid; UI surfaces error context.</summary>
    Invalid,

    /// <summary>Provider endpoint unreachable; transient — retry recommended.</summary>
    Unreachable,
}
