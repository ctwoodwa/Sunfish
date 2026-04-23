import { useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react';
import { SunfishDialog } from './SunfishDialog';
import { SunfishButton } from '../SunfishButton/SunfishButton';

const meta: Meta<typeof SunfishDialog> = {
  title: 'Components/SunfishDialog',
  component: SunfishDialog,
};

export default meta;
type Story = StoryObj<typeof SunfishDialog>;

// Wrapper story renderer — a dialog needs a trigger in a preview.
function DialogDemo() {
  const [open, setOpen] = useState(true);
  return (
    <>
      <SunfishButton onClick={() => setOpen(true)}>Open dialog</SunfishButton>
      <SunfishDialog
        open={open}
        onClose={() => setOpen(false)}
        title="Sunfish Dialog"
        footer={
          <>
            <SunfishButton onClick={() => setOpen(false)}>Cancel</SunfishButton>
            <SunfishButton onClick={() => setOpen(false)}>Confirm</SunfishButton>
          </>
        }
      >
        <p>The dialog renders through a portal on document.body.</p>
        <p>ADR 0023 slot classes are emitted for header / body / footer.</p>
      </SunfishDialog>
    </>
  );
}

export const Bootstrap: Story = {
  render: () => <DialogDemo />,
  parameters: { sunfishProvider: 'bootstrap' },
};

export const Fluent: Story = {
  render: () => <DialogDemo />,
  parameters: { sunfishProvider: 'fluent' },
};

export const Material: Story = {
  render: () => <DialogDemo />,
  parameters: { sunfishProvider: 'material' },
};
