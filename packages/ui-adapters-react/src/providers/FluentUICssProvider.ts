import type { ICssProvider } from '../contracts/ICssProvider';
import { ButtonVariant } from '../contracts/ButtonVariant';
import { ButtonSize } from '../contracts/ButtonSize';
import { FillMode } from '../contracts/FillMode';
import { RoundedMode } from '../contracts/RoundedMode';

/**
 * Maps Sunfish component state to Fluent UI v9 skin classes.
 *
 * Port of `packages/ui-adapters-blazor/Providers/FluentUI/FluentUICssProvider.cs`
 * (Button / DataGrid / Dialog slices only — see ADR 0030 for scope).
 *
 * Class vocabulary mirrors the Blazor provider: generic `sf-button--{variant}`
 * for backward-compat, plus ADR-0024 `sf-btn-*` tokens for subtle/transparent/
 * light/dark variants.
 */
function cx(...parts: Array<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(' ');
}

function fluentVariantAdditionalClass(variant: ButtonVariant): string {
  switch (variant) {
    case ButtonVariant.Subtle:
      return 'sf-btn-subtle';
    case ButtonVariant.Transparent:
      return 'sf-btn-transparent';
    case ButtonVariant.Light:
      return 'sf-btn-light';
    case ButtonVariant.Dark:
      return 'sf-btn-dark';
    default:
      return '';
  }
}

export class FluentUICssProvider implements ICssProvider {
  readonly name = 'fluent';

  buttonClass(
    variant: ButtonVariant,
    size: ButtonSize,
    fillMode: FillMode,
    rounded: RoundedMode,
    isDisabled: boolean,
  ): string {
    const additional = fluentVariantAdditionalClass(variant);
    return cx(
      'sf-button',
      `sf-button--${String(variant).toLowerCase()}`,
      additional,
      `sf-button--${String(size).toLowerCase()}`,
      fillMode !== FillMode.Filled && `sf-button--fill-${String(fillMode).toLowerCase()}`,
      rounded !== RoundedMode.Medium && `sf-button--rounded-${String(rounded).toLowerCase()}`,
      isDisabled && 'sf-button--disabled',
    );
  }

  // ── DataGrid ──────────────────────────────────────────────────────
  dataGridClass(): string {
    return 'sf-datagrid';
  }
  dataGridTableClass(): string {
    // Fluent lets the skin CSS style the table; no class on the table element.
    return '';
  }
  dataGridHeaderClass(): string {
    return 'sf-datagrid-header';
  }
  dataGridHeaderCellClass(isSortable: boolean, isSorted: boolean): string {
    return cx(
      'sf-datagrid-header-cell',
      isSortable && 'sf-datagrid-header-cell--sortable',
      isSorted && 'sf-datagrid-header-cell--sorted',
    );
  }
  dataGridRowClass(isSelected: boolean, isStriped: boolean): string {
    return cx(
      'sf-datagrid-row',
      isSelected && 'sf-datagrid-row--selected',
      isStriped && 'sf-datagrid-row--striped',
    );
  }
  dataGridCellClass(): string {
    return 'sf-datagrid-cell';
  }

  // ── Dialog ─────────────────────────────────────────────────────────
  dialogClass(): string {
    return 'sf-dialog';
  }
  dialogDialogClass(): string {
    return 'sf-dialog';
  }
  dialogContentClass(): string {
    return 'sf-dialog__content';
  }
  dialogHeaderClass(): string {
    return 'sf-dialog__header';
  }
  dialogTitleClass(): string {
    return 'sf-dialog__title';
  }
  dialogBodyClass(): string {
    return 'sf-dialog__body';
  }
  dialogFooterClass(): string {
    return 'sf-dialog__footer';
  }
  dialogOverlayClass(): string {
    return 'sf-dialog-overlay';
  }
  dialogCloseButtonClass(): string {
    return 'sf-fui-dialog-close';
  }
  dialogCloseMarkup(): string {
    return (
      '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true">' +
      '<path d="M2.22 2.22a.75.75 0 0 1 1.06 0L8 6.94l4.72-4.72a.75.75 0 1 1 1.06 1.06L9.06 8l4.72 4.72a.75.75 0 1 1-1.06 1.06L8 9.06l-4.72 4.72a.75.75 0 0 1-1.06-1.06L6.94 8 2.22 3.28a.75.75 0 0 1 0-1.06Z" fill="currentColor"/>' +
      '</svg>'
    );
  }
}
