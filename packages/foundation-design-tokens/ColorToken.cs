namespace Sunfish.Foundation.DesignTokens;

/// <summary>
/// A paired light/dark color token per ADR 0077 §5.3 OS-preference
/// tokens. The values are 7-character lowercase hex strings
/// (<c>"#rrggbb"</c>) — wire-format-stable so the tokens can round-trip
/// to <c>tokens.json</c> + the generated <c>tokens.css</c> custom
/// properties without any per-renderer escaping.
/// </summary>
/// <param name="Light">Light-theme hex value.</param>
/// <param name="Dark">Dark-theme hex value.</param>
public readonly record struct ColorToken(string Light, string Dark);
