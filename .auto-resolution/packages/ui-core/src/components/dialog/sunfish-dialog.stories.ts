import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import './sunfish-dialog.js';
import '../button/sunfish-button.js';

const meta: Meta = {
  title: 'Core/Dialog',
  component: 'sunfish-dialog',
  parameters: {
    a11y: {
      sunfish: {
        wcag22Conformant: ['1.3.1', '1.4.3', '2.1.1', '2.1.2', '2.4.3', '2.4.7', '2.4.11', '2.5.8', '4.1.2'],
        ariaPattern: 'https://www.w3.org/WAI/ARIA/apg/patterns/dialog-modal/',
        keyboardMap: [
          { keys: ['Escape'], action: 'close' },
          { keys: ['Tab'], action: 'cycle-forward-in-trap' },
          { keys: ['Shift+Tab'], action: 'cycle-backward-in-trap' },
        ],
        focus: { initial: 'first-focusable-child', trap: true, restore: 'trigger' },
        screenReaderAudit: {
          'nvda-2026.1/firefox-126': { verified: '2026-04-28', auditor: '@a11y-lead', pass: true },
          'voiceover-macos15/safari-17': { verified: '2026-04-28', auditor: '@a11y-lead', pass: true },
        },
        contrast: { bodyTextMinRatio: 4.5, borderMinRatio: 3.0, apcaLcNonTextMin: 45 },
        targetSize: { desktop: 24, tablet: 32, mobile: 44 },
        shadowDom: { mode: 'open', crossRootAriaStrategy: 'reflective-aria' },
        composedOf: ['sunfish-button'],
        directionalIcons: [],
      },
    },
  },
};

export default meta;
type Story = StoryObj;

export const Default: Story = {
  args: { heading: 'Confirm deletion', open: true },
  render: (args) => html`
    <sunfish-dialog heading=${args.heading} ?open=${args.open}>
      <p>This action cannot be undone.</p>
      <sunfish-button label="Cancel"></sunfish-button>
      <sunfish-button label="Delete"></sunfish-button>
    </sunfish-dialog>
  `,
};
