import type { Meta, StoryObj } from '@storybook/react';
import { SunfishDataGrid, type Column } from './SunfishDataGrid';

interface Row {
  id: number;
  name: string;
  status: string;
}

const columns: Array<Column<Row>> = [
  { key: 'id', header: 'ID', accessor: 'id', sortable: true },
  { key: 'name', header: 'Name', accessor: 'name', sortable: true },
  { key: 'status', header: 'Status', accessor: 'status' },
];

const data: Row[] = [
  { id: 1, name: 'Alpha', status: 'Active' },
  { id: 2, name: 'Beta', status: 'Pending' },
  { id: 3, name: 'Gamma', status: 'Archived' },
];

const meta: Meta<typeof SunfishDataGrid<Row>> = {
  title: 'Components/SunfishDataGrid',
  component: SunfishDataGrid<Row>,
  args: {
    columns,
    data,
    sortedKey: 'id',
    striped: true,
  },
};

export default meta;
type Story = StoryObj<typeof SunfishDataGrid<Row>>;

export const Bootstrap: Story = {
  parameters: { sunfishProvider: 'bootstrap' },
};

export const Fluent: Story = {
  parameters: { sunfishProvider: 'fluent' },
};

export const Material: Story = {
  parameters: { sunfishProvider: 'material' },
};
