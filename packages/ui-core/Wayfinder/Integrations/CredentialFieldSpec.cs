namespace Sunfish.UICore.Wayfinder.Integrations;

/// <summary>
/// Per-credential-field schema per ADR 0067 §3.2. Each field-spec
/// describes ONE form input rendered into the integration-config
/// editor: its key (wire-format identifier; round-trips through the
/// signed-credential pipeline), its localized display label, its
/// rendering kind + autocomplete hint, and optional help/placeholder
/// strings.
/// </summary>
/// <param name="Key">Wire-format identifier (kebab-case; e.g., <c>"api-key"</c>). Used as the dictionary key for sensitive/non-sensitive credentials.</param>
/// <param name="DisplayLabel">Localized display label rendered as the form-input label.</param>
/// <param name="Kind">Rendering discriminator (Text / Secret / Url / ReadOnlyOutput).</param>
/// <param name="AutocompleteHint">WHATWG-constrained autocomplete attribute hint.</param>
/// <param name="IsRequired">When true, the renderer MUST mark the field required + emit the WCAG SC 3.3.2 indicator.</param>
/// <param name="HelpText">Optional localized help text surfaced via <c>aria-describedby</c>.</param>
/// <param name="Placeholder">Optional localized placeholder; renderers MUST NOT use placeholder as a label substitute (WCAG SC 1.3.1 / SC 3.3.2).</param>
public sealed record CredentialFieldSpec(
    string Key,
    string DisplayLabel,
    CredentialFieldKind Kind,
    CredentialAutocompleteHint AutocompleteHint,
    bool IsRequired,
    string? HelpText,
    string? Placeholder);
