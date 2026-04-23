import type { ReactNode } from 'react';
import { useCssProvider } from '../../CssProviderContext';

export interface Column<T> {
  /** Stable key — used for sort tracking and React list keys. */
  key: string;
  /** Column header label. */
  header: ReactNode;
  /** Accessor: either a property name on T or a render function. */
  accessor: keyof T | ((row: T) => ReactNode);
  /** If true, the column renders its header as sortable. */
  sortable?: boolean;
}

export interface SunfishDataGridProps<T> {
  columns: Array<Column<T>>;
  data: T[];
  /** Which column key is currently sorted (for header emphasis). */
  sortedKey?: string;
  /** Which row indices are currently selected (for row highlighting). */
  selectedIndices?: Set<number> | number[];
  /** Fired when a row is clicked. Row index is passed alongside the row. */
  onRowClick?: (row: T, index: number) => void;
  /** Emit striped rows (every other row). */
  striped?: boolean;
  className?: string;
}

function isSelected(indices: SunfishDataGridProps<unknown>['selectedIndices'], index: number) {
  if (!indices) return false;
  if (indices instanceof Set) return indices.has(index);
  return indices.includes(index);
}

/**
 * Minimal tabular shell — parity goal is the Blazor `SunfishDataGrid`
 * skeleton (wrapper / table / header / row / cell slots), without the
 * full G37 feature matrix. A later wave will restore sort / filter /
 * pager / command column per ADR 0014 parity policy.
 */
export function SunfishDataGrid<T>({
  columns,
  data,
  sortedKey,
  selectedIndices,
  onRowClick,
  striped = false,
  className,
}: SunfishDataGridProps<T>) {
  const provider = useCssProvider();
  const wrapperClass = [provider.dataGridClass(), className].filter(Boolean).join(' ');
  const tableClass = provider.dataGridTableClass();

  return (
    <div className={wrapperClass}>
      <table className={tableClass || undefined}>
        <thead className={provider.dataGridHeaderClass()}>
          <tr>
            {columns.map((col) => (
              <th
                key={col.key}
                className={provider.dataGridHeaderCellClass(
                  !!col.sortable,
                  sortedKey === col.key,
                )}
                aria-sort={sortedKey === col.key ? 'ascending' : undefined}
              >
                {col.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {data.map((row, rowIndex) => {
            const selected = isSelected(selectedIndices, rowIndex);
            return (
              <tr
                key={rowIndex}
                className={provider.dataGridRowClass(selected, striped && rowIndex % 2 === 1)}
                onClick={onRowClick ? () => onRowClick(row, rowIndex) : undefined}
                role={onRowClick ? 'button' : undefined}
                tabIndex={onRowClick ? 0 : undefined}
              >
                {columns.map((col) => (
                  <td key={col.key} className={provider.dataGridCellClass()}>
                    {typeof col.accessor === 'function'
                      ? col.accessor(row)
                      : (row[col.accessor] as unknown as ReactNode)}
                  </td>
                ))}
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
