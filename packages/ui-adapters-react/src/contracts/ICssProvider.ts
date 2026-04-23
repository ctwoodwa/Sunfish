import type { ButtonVariant } from './ButtonVariant';
import type { ButtonSize } from './ButtonSize';
import type { FillMode } from './FillMode';
import type { RoundedMode } from './RoundedMode';

/**
 * Bootstrap subset of the Sunfish CSS-provider contract.
 *
 * The canonical C# contract (`packages/ui-core/Contracts/ISunfishCssProvider.cs`)
 * has ~150 methods spanning every component slot in the system. This
 * TypeScript port covers ONLY the slices required by the Wave 3.5
 * proof-of-concept components — Button, DataGrid, and Dialog.
 *
 * Full parity with the C# surface is tracked in the adapter-parity matrix
 * (`_shared/engineering/adapter-parity.md`). When the React adapter grows
 * past the PoC scope, every new component adds the methods it needs to
 * this interface, following the Blazor-side naming verbatim.
 *
 * See:
 * - ADR 0014 — Adapter Parity Policy
 * - ADR 0030 — React Adapter Scaffolding (this wave)
 */
export interface ICssProvider {
  // ── Button ─────────────────────────────────────────────────────────
  /**
   * Returns the CSS class(es) for a button composed of variant + size +
   * fillMode + rounded + disabled state. Mirrors the 5-arg overload of
   * `ISunfishCssProvider.ButtonClass` in the C# contract.
   */
  buttonClass(
    variant: ButtonVariant,
    size: ButtonSize,
    fillMode: FillMode,
    rounded: RoundedMode,
    isDisabled: boolean,
  ): string;

  // ── DataGrid ──────────────────────────────────────────────────────
  dataGridClass(): string;
  dataGridTableClass(): string;
  dataGridHeaderClass(): string;
  dataGridHeaderCellClass(isSortable: boolean, isSorted: boolean): string;
  dataGridRowClass(isSelected: boolean, isStriped: boolean): string;
  dataGridCellClass(): string;

  // ── Dialog (ADR 0023 slot-class split) ─────────────────────────────
  dialogClass(): string;
  dialogDialogClass(): string;
  dialogContentClass(): string;
  dialogHeaderClass(): string;
  dialogTitleClass(): string;
  dialogBodyClass(): string;
  dialogFooterClass(): string;
  dialogOverlayClass(): string;
  dialogCloseButtonClass(): string;
  /**
   * Returns the inner HTML markup for the close button glyph.
   * Bootstrap returns the empty string because `.btn-close` renders its
   * own SVG via CSS background; Fluent returns an inline dismiss SVG;
   * Material returns a `<span class="material-symbols-outlined">close</span>`.
   */
  dialogCloseMarkup(): string;
}
