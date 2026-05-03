import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { SunfishDataGrid, type Column } from './SunfishDataGrid';
import { CssProviderProvider } from '../../CssProviderContext';
import { FluentUICssProvider } from '../../providers/FluentUICssProvider';

interface Row {
  id: number;
  name: string;
}

const columns: Array<Column<Row>> = [
  { key: 'id', header: 'ID', accessor: 'id', sortable: true },
  { key: 'name', header: 'Name', accessor: 'name' },
];

const data: Row[] = [
  { id: 1, name: 'Alpha' },
  { id: 2, name: 'Beta' },
];

describe('SunfishDataGrid', () => {
  it('renders headers and rows', () => {
    render(<SunfishDataGrid<Row> columns={columns} data={data} />);
    expect(screen.getByText('ID')).toBeInTheDocument();
    expect(screen.getByText('Name')).toBeInTheDocument();
    expect(screen.getByText('Alpha')).toBeInTheDocument();
    expect(screen.getByText('Beta')).toBeInTheDocument();
  });

  it('fires onRowClick with the row and index', () => {
    const onRowClick = vi.fn();
    render(<SunfishDataGrid<Row> columns={columns} data={data} onRowClick={onRowClick} />);
    fireEvent.click(screen.getByText('Alpha').closest('tr')!);
    expect(onRowClick).toHaveBeenCalledWith(data[0], 0);
  });

  it('applies the Fluent wrapper class under the Fluent provider', () => {
    const { container } = render(
      <CssProviderProvider provider={new FluentUICssProvider()}>
        <SunfishDataGrid<Row> columns={columns} data={data} />
      </CssProviderProvider>,
    );
    const wrapper = container.querySelector('.sf-datagrid');
    expect(wrapper).not.toBeNull();
  });

  it('marks sorted header cells via aria-sort', () => {
    render(<SunfishDataGrid<Row> columns={columns} data={data} sortedKey="id" />);
    const header = screen.getByText('ID').closest('th');
    expect(header).toHaveAttribute('aria-sort', 'ascending');
  });
});
