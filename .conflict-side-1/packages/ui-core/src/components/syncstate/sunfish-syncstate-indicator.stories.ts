import type { Meta, StoryObj } from '@storybook/web-components';
import { html } from 'lit';
import './sunfish-syncstate-indicator.js';

const meta: Meta = {
  title: 'Core/SyncStateIndicator',
  component: 'sunfish-syncstate-indicator',
  parameters: {
    a11y: {
      sunfish: {
        wcag22Conformant: ['1.3.1', '1.4.3', '1.4.11', '4.1.2', '4.1.3'],
        ariaPattern: 'https://www.w3.org/WAI/ARIA/apg/practices/live-regions/',
        keyboardMap: [],
        focus: { initial: 'none', trap: false, restore: null },
        screenReaderAudit: {
          'nvda-2026.1/firefox-126': { verified: '2026-04-28', auditor: '@a11y-lead', pass: true },
          'voiceover-macos15/safari-17': { verified: '2026-04-28', auditor: '@a11y-lead', pass: true },
        },
        contrast: { bodyTextMinRatio: 4.5, borderMinRatio: 3.0, apcaLcNonTextMin: 45 },
        targetSize: { desktop: 24, tablet: 32, mobile: 44 },
        shadowDom: { mode: 'open', crossRootAriaStrategy: 'reflective-aria' },
        composedOf: [],
        directionalIcons: ['conflict'],
      },
    },
  },
};

export default meta;
type Story = StoryObj;

export const AllStates: Story = {
  render: () => html`
    <div style="display: flex; flex-direction: column; gap: 12px; padding: 16px;">
      <sunfish-syncstate-indicator state="healthy"></sunfish-syncstate-indicator>
      <sunfish-syncstate-indicator state="stale"></sunfish-syncstate-indicator>
      <sunfish-syncstate-indicator state="offline"></sunfish-syncstate-indicator>
      <sunfish-syncstate-indicator state="conflict"></sunfish-syncstate-indicator>
      <sunfish-syncstate-indicator state="quarantine"></sunfish-syncstate-indicator>
    </div>
  `,
};

export const Compact: Story = {
  render: () => html`
    <div style="display: flex; gap: 16px; padding: 16px;">
      <sunfish-syncstate-indicator state="healthy" form="compact"></sunfish-syncstate-indicator>
      <sunfish-syncstate-indicator state="stale" form="compact"></sunfish-syncstate-indicator>
      <sunfish-syncstate-indicator state="conflict" form="compact"></sunfish-syncstate-indicator>
    </div>
  `,
};
