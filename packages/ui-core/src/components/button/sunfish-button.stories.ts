import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import './sunfish-button.js';

const meta: Meta = {
  title: 'Core/Button',
  component: 'sunfish-button',
  parameters: {
    a11y: {
      sunfish: {
        wcag22Conformant: ['1.3.1', '1.4.3', '1.4.11', '2.1.1', '2.4.7', '2.5.8', '4.1.2'],
        ariaPattern: 'https://www.w3.org/WAI/ARIA/apg/patterns/button/',
        keyboardMap: [
          { keys: ['Enter'], action: 'activate' },
          { keys: ['Space'], action: 'activate' },
        ],
        focus: { initial: 'self', trap: false, restore: null },
        screenReaderAudit: {
          'nvda-2026.1/firefox-126': { verified: '2026-04-28', auditor: '@a11y-lead', pass: true },
          'voiceover-macos15/safari-17': { verified: '2026-04-28', auditor: '@a11y-lead', pass: true },
        },
        contrast: { bodyTextMinRatio: 4.5, borderMinRatio: 3.0, apcaLcNonTextMin: 45 },
        targetSize: { desktop: 24, tablet: 32, mobile: 44 },
        shadowDom: { mode: 'open', crossRootAriaStrategy: 'reflective-aria' },
        composedOf: [],
        directionalIcons: [],
      },
    },
  },
  argTypes: {
    label: { control: 'text' },
    disabled: { control: 'boolean' },
  },
};

export default meta;
type Story = StoryObj;

export const Default: Story = {
  args: { label: 'Submit', disabled: false },
  render: (args) => html`<sunfish-button label=${args.label} ?disabled=${args.disabled}></sunfish-button>`,
};

export const Disabled: Story = {
  args: { label: 'Submit', disabled: true },
  render: (args) => html`<sunfish-button label=${args.label} ?disabled=${args.disabled}></sunfish-button>`,
};
