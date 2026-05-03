import { forwardRef, type ButtonHTMLAttributes, type ReactNode } from 'react';
import { useCssProvider } from '../../CssProviderContext';
import { ButtonVariant } from '../../contracts/ButtonVariant';
import { ButtonSize } from '../../contracts/ButtonSize';
import { FillMode } from '../../contracts/FillMode';
import { RoundedMode } from '../../contracts/RoundedMode';

export interface SunfishButtonProps
  extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'disabled' | 'className'> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  fillMode?: FillMode;
  rounded?: RoundedMode;
  disabled?: boolean;
  className?: string;
  children?: ReactNode;
}

/**
 * Sunfish button component — React parity port of
 * `packages/ui-adapters-blazor/Components/Buttons/SunfishButton.razor`.
 *
 * Classes are emitted by the active `ICssProvider` from
 * `CssProviderContext`, so the same component re-skins across Bootstrap /
 * Fluent / Material based on the provider at the root of the tree.
 */
export const SunfishButton = forwardRef<HTMLButtonElement, SunfishButtonProps>(
  function SunfishButton(
    {
      variant = ButtonVariant.Primary,
      size = ButtonSize.Medium,
      fillMode = FillMode.Filled,
      rounded = RoundedMode.Medium,
      disabled = false,
      type = 'button',
      className,
      children,
      ...rest
    },
    ref,
  ) {
    const provider = useCssProvider();
    const providerClass = provider.buttonClass(variant, size, fillMode, rounded, disabled);
    const combined = [providerClass, className].filter(Boolean).join(' ');

    return (
      <button
        ref={ref}
        type={type}
        className={combined}
        disabled={disabled}
        {...rest}
      >
        {children}
      </button>
    );
  },
);
