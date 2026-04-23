/**
 * Specifies the border-radius mode of a button. TypeScript port of the
 * three-value subset of `packages/foundation/Enums/ButtonFillMode.cs`
 * (the C# enum ships five values; the scaffold scope uses three).
 */
export const RoundedMode = {
  /** Small border radius. */
  Small: 'Small',
  /** Medium border radius (default). */
  Medium: 'Medium',
  /** Large border radius. */
  Large: 'Large',
} as const;

export type RoundedMode = (typeof RoundedMode)[keyof typeof RoundedMode];
