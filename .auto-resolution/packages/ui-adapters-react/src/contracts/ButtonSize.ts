/**
 * Specifies the size of a button. TypeScript port of
 * `packages/foundation/Enums/ButtonSize.cs`.
 */
export const ButtonSize = {
  /** A compact button for tight layouts. */
  Small: 'Small',
  /** The default button size. */
  Medium: 'Medium',
  /** A larger button for increased prominence. */
  Large: 'Large',
} as const;

export type ButtonSize = (typeof ButtonSize)[keyof typeof ButtonSize];
