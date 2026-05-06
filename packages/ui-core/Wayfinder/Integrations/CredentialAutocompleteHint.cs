using System.Text.Json.Serialization;

namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Per-credential-field WHATWG-constrained autocomplete hint per ADR
/// 0067 §3.2. Renderers emit this as the HTML <c>autocomplete</c>
/// attribute on the corresponding form input — the closed-enum surface
/// guarantees the rendered attribute value is always a valid WHATWG
/// token, never a free-form string that could degrade browser/password-
/// manager behaviour.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CredentialAutocompleteHint
{
    /// <summary>No autocomplete hint; emits <c>autocomplete="off"</c>.</summary>
    None,

    /// <summary>Existing credential — emits <c>autocomplete="current-password"</c>.</summary>
    CurrentPassword,

    /// <summary>New credential — emits <c>autocomplete="new-password"</c>.</summary>
    NewPassword,

    /// <summary>One-time code (e.g., 2FA SMS / TOTP) — emits <c>autocomplete="one-time-code"</c>.</summary>
    OneTimeCode,

    /// <summary>Username — emits <c>autocomplete="username"</c>.</summary>
    Username,

    /// <summary>Email address — emits <c>autocomplete="email"</c>.</summary>
    Email,

    /// <summary>URL — emits <c>autocomplete="url"</c>.</summary>
    Url,
}
