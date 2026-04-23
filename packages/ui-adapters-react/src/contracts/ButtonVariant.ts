/**
 * Specifies the visual style variant of a button.
 *
 * TypeScript port of `packages/foundation/Enums/ButtonVariant.cs`. Keep the
 * string values aligned with the C# enum names (lowercased) so provider
 * class suffixes such as `sf-button--primary` emit identically across
 * Blazor and React.
 *
 * See ADR 0024 — Button Variant Enum Expansion for the ten-value shape
 * and per-provider mapping contract.
 */
export const ButtonVariant = {
  /** Primary call-to-action style. */
  Primary: 'Primary',
  /** A secondary, less prominent style. */
  Secondary: 'Secondary',
  /** A destructive or dangerous action style. */
  Danger: 'Danger',
  /** A cautionary action style. */
  Warning: 'Warning',
  /** An informational action style. */
  Info: 'Info',
  /** A positive or confirmation action style. */
  Success: 'Success',
  /** Low-emphasis subtle treatment (BS5: outline-secondary; Fluent/M3: sf-btn-subtle). */
  Subtle: 'Subtle',
  /** Chromeless transparent / link-like treatment (BS5: btn-link; Fluent/M3: sf-btn-transparent). */
  Transparent: 'Transparent',
  /** Neutral-surface light treatment. */
  Light: 'Light',
  /** Neutral-inverse dark treatment. */
  Dark: 'Dark',
} as const;

export type ButtonVariant = (typeof ButtonVariant)[keyof typeof ButtonVariant];
