namespace Sunfish.Foundation.Enums;

/// <summary>
/// Specifies the HTML type attribute for a button element.
/// </summary>
public enum ButtonType
{
    /// <summary>A standard button that does not submit a form.</summary>
    Button,

    /// <summary>Submits the associated form.</summary>
    Submit,

    /// <summary>Resets the associated form.</summary>
    Reset
}

/// <summary>
/// Specifies the visual style variant of a button.
/// See <see href="../../../docs/adrs/0024-button-variant-enum-expansion.md">ADR 0024</see>
/// for the rationale behind the ten-value shape and the per-provider mapping contract.
/// </summary>
public enum ButtonVariant
{
    /// <summary>The primary call-to-action style.</summary>
    Primary,

    /// <summary>A secondary, less prominent style.</summary>
    Secondary,

    /// <summary>A destructive or dangerous action style.</summary>
    Danger,

    /// <summary>A cautionary action style.</summary>
    Warning,

    /// <summary>An informational action style.</summary>
    Info,

    /// <summary>A positive or confirmation action style.</summary>
    Success,

    /// <summary>
    /// A low-emphasis "subtle" treatment. Provider mapping:
    /// Fluent v9 → <c>sf-btn-subtle</c> (native <c>fui-Button--subtle</c> token ladder);
    /// Bootstrap 5 → <c>btn-outline-secondary</c> (no native <c>btn-subtle</c>, documented semantic compromise);
    /// Material 3 → <c>sf-btn-subtle</c> (M3 text button).
    /// </summary>
    Subtle,

    /// <summary>
    /// A chromeless "transparent" treatment — no fill, no border, link-like affordance.
    /// Provider mapping:
    /// Fluent v9 → <c>sf-btn-transparent</c> (native <c>fui-Button--transparent</c>);
    /// Bootstrap 5 → <c>btn-link</c> (native link-styled button);
    /// Material 3 → <c>sf-btn-transparent</c> (M3 text button).
    /// </summary>
    Transparent,

    /// <summary>
    /// A neutral-surface "light" treatment. Provider mapping:
    /// Bootstrap 5 → <c>btn-light</c> (native);
    /// Fluent v9 → <c>sf-btn-light</c> (subtle on neutral surface);
    /// Material 3 → <c>sf-btn-light</c> (outlined on neutral surface).
    /// </summary>
    Light,

    /// <summary>
    /// A neutral-inverse "dark" treatment. Provider mapping:
    /// Bootstrap 5 → <c>btn-dark</c> (native);
    /// Fluent v9 → <c>sf-btn-dark</c> (subtle on neutral-inverted surface);
    /// Material 3 → <c>sf-btn-dark</c> (filled on inverse-surface).
    /// </summary>
    Dark
}
