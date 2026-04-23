import type { ICssProvider } from '../contracts/ICssProvider';
import { ButtonVariant } from '../contracts/ButtonVariant';
import { ButtonSize } from '../contracts/ButtonSize';
import { FillMode } from '../contracts/FillMode';
import { RoundedMode } from '../contracts/RoundedMode';

/**
 * Maps Sunfish component state to Bootstrap 5.3 CSS classes.
 *
 * Port of `packages/ui-adapters-blazor/Providers/Bootstrap/BootstrapCssProvider.cs`
 * (Button / DataGrid / Dialog slices only — see ADR 0030 for scope).
 */
function cx(...parts: Array<string | false | null | undefined>): string {
  return parts.filter(Boolean).join(' ');
}

function bootstrapVariant(variant: ButtonVariant): string {
  switch (variant) {
    case ButtonVariant.Primary:
      return 'primary';
    case ButtonVariant.Secondary:
      return 'secondary';
    case ButtonVariant.Danger:
      return 'danger';
    case ButtonVariant.Warning:
      return 'warning';
    case ButtonVariant.Info:
      return 'info';
    case ButtonVariant.Success:
      return 'success';
    case ButtonVariant.Light:
      return 'light';
    case ButtonVariant.Dark:
      return 'dark';
    // ADR 0024: Subtle → documented BS5 mapping.
    case ButtonVariant.Subtle:
      return 'secondary';
    // ADR 0024: Transparent → btn-link; suffix used only as fallback.
    case ButtonVariant.Transparent:
      return 'secondary';
    default:
      return 'primary';
  }
}

function bootstrapSize(size: ButtonSize): string {
  switch (size) {
    case ButtonSize.Small:
      return 'btn-sm';
    case ButtonSize.Large:
      return 'btn-lg';
    default:
      return '';
  }
}

function bootstrapButtonSolidClass(variant: ButtonVariant): string {
  if (variant === ButtonVariant.Subtle) return 'btn-outline-secondary';
  if (variant === ButtonVariant.Transparent) return 'btn-link';
  return `btn-${bootstrapVariant(variant)}`;
}

export class BootstrapCssProvider implements ICssProvider {
  readonly name = 'bootstrap';

  buttonClass(
    variant: ButtonVariant,
    size: ButtonSize,
    fillMode: FillMode,
    rounded: RoundedMode,
    isDisabled: boolean,
  ): string {
    const solidSuffix = bootstrapVariant(variant);
    return cx(
      'btn',
      fillMode === FillMode.Filled && bootstrapButtonSolidClass(variant),
      fillMode === FillMode.Outline && `btn-outline-${solidSuffix}`,
      fillMode === FillMode.Flat && `btn-light border-0 text-${solidSuffix}`,
      fillMode === FillMode.Clear && 'btn-link text-decoration-none',
      size !== ButtonSize.Medium && bootstrapSize(size),
      rounded === RoundedMode.Small && 'rounded-1',
      rounded === RoundedMode.Large && 'rounded-3',
      isDisabled && 'disabled',
    );
  }

  // ── DataGrid ──────────────────────────────────────────────────────
  dataGridClass(): string {
    return 'table-responsive sf-bs-datagrid';
  }
  dataGridTableClass(): string {
    return 'table';
  }
  dataGridHeaderClass(): string {
    return 'sf-bs-datagrid-header';
  }
  dataGridHeaderCellClass(isSortable: boolean, isSorted: boolean): string {
    return cx(
      'sf-bs-datagrid-header-cell',
      isSortable && 'sf-bs-datagrid-header-cell--sortable',
      isSorted && 'sf-bs-datagrid-header-cell--sorted',
    );
  }
  dataGridRowClass(isSelected: boolean, isStriped: boolean): string {
    return cx(
      'sf-bs-datagrid-row',
      isSelected && 'table-active',
      isStriped && 'sf-bs-datagrid-row--striped',
    );
  }
  dataGridCellClass(): string {
    return 'sf-bs-datagrid-cell';
  }

  // ── Dialog ─────────────────────────────────────────────────────────
  dialogClass(): string {
    return 'modal';
  }
  dialogDialogClass(): string {
    return 'modal-dialog';
  }
  dialogContentClass(): string {
    return 'modal-content';
  }
  dialogHeaderClass(): string {
    return 'modal-header';
  }
  dialogTitleClass(): string {
    return 'modal-title';
  }
  dialogBodyClass(): string {
    return 'modal-body';
  }
  dialogFooterClass(): string {
    return 'modal-footer';
  }
  dialogOverlayClass(): string {
    return 'modal-backdrop fade show';
  }
  dialogCloseButtonClass(): string {
    return 'btn-close';
  }
  dialogCloseMarkup(): string {
    // BS5 `.btn-close` renders its own SVG via CSS background.
    return '';
  }
}
