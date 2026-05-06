namespace Sunfish.UICore.Primitives;

/// <summary>
/// Per-form-control accessibility contract per ADR 0077 §4 First-Aid
/// baseline. Every renderable form input declares this metadata so the
/// First-Aid renderer can wire labels + error messages + required
/// indicators per WCAG SC 3.3.1 (Error Identification) + SC 3.3.2
/// (Labels or Instructions) + SC 3.3.3 (Error Suggestion).
/// </summary>
public interface IFormControlContract
{
    /// <summary>Discriminator for the input kind.</summary>
    FormControlKind Kind { get; }

    /// <summary>Localization key for the visible label (WCAG SC 3.3.2).</summary>
    string LabelKey { get; }

    /// <summary>True when the field is required; renderer emits the SC 3.3.2 indicator.</summary>
    bool IsRequired { get; }

    /// <summary>Current error message; null when valid. Rendered via <c>aria-describedby</c> per SC 3.3.1.</summary>
    string? ErrorMessage { get; }

    /// <summary>Localization key for a suggested-correction hint per SC 3.3.3; null when no hint.</summary>
    string? ErrorSuggestionKey { get; }
}
