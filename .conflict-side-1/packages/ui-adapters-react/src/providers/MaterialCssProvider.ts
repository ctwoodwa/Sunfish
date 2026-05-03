import type { ICssProvider } from '../contracts/ICssProvider';
import { ButtonVariant } from '../contracts/ButtonVariant';
import { ButtonSize } from '../contracts/ButtonSize';
import { FillMode } from '../contracts/FillMode';
import { RoundedMode } from '../contracts/RoundedMode';

/**
 * Maps Sunfish component state to Material 3 skin classes.
 *
 * Port of `packages/ui-adapters-blazor/Providers/Material/MaterialCssProvider.cs`
 * (Button / DataGrid / Dialog slices only — see ADR 0030 for scope).
 */
function cx(...parts: Array<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(' ');
}

function materialVariantAdditionalClass(variant: ButtonVariant): string {
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

export class MaterialCssProvider implements ICssProvider {
  readonly name = 'material';

  buttonClass(
    variant: ButtonVariant,
    size: ButtonSize,
    fillMode: FillMode,
    rounded: RoundedMode,
    isDisabled: boolean,
  ): string {
    const additional = materialVariantAdditionalClass(variant);
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
    return 'sf-m3-dialog-close';
  }
  dialogCloseMarkup(): string {
    return '<span class="material-symbols-outlined" aria-hidden="true">close</span>';
  }
}
