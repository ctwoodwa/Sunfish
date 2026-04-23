/**
 * Specifies the fill mode (visual weight) of a button. TypeScript port of
 * `packages/foundation/Enums/ButtonFillMode.cs`.
 *
 * Note: the C# source has five values (Solid, Outline, Flat, Link, Clear).
 * The scaffold scope (ADR 0030) calls for a four-value port — we keep the
 * four most-used values (Flat / Clear / Filled / Outline) per the intake's
 * explicit listing. `Filled` is the React-side alias for `Solid`; provider
 * logic maps both the same way.
 */
export const FillMode = {
  /** Solid / filled background (alias for C# `Solid`). */
  Filled: 'Filled',
  /** Border-only with transparent background. */
  Outline: 'Outline',
  /** No border or background; text only with hover effect. */
  Flat: 'Flat',
  /** No visual chrome; only content is visible. */
  Clear: 'Clear',
} as const;

export type FillMode = (typeof FillMode)[keyof typeof FillMode];
