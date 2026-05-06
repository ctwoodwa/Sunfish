using System.Text.Json.Serialization;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Per-credential-field rendering discriminator per ADR 0067 §3.2.
/// Determines the <c>&lt;input type="..."&gt;</c> attribute emitted by
/// the renderer + the masking / URL-validation behavior.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CredentialFieldKind
{
    /// <summary>Plain text input.</summary>
    Text,

    /// <summary>Masked secret input — renderer MUST NOT log, copy-to-clipboard, or persist outside the encrypted credential pipeline.</summary>
    Secret,

    /// <summary>URL input — renderer applies URL-format validation.</summary>
    Url,

    /// <summary>Read-only output (e.g., "API key (stored)" badge); not a user-editable field.</summary>
    ReadOnlyOutput,
}
