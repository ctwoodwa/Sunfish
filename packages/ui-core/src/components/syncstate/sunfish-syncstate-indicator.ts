import { LitElement, html, css, svg, type TemplateResult } from 'lit';
import { customElement, property } from 'lit/decorators.js';

export type SyncState = 'healthy' | 'stale' | 'offline' | 'conflict' | 'quarantine';

// Icons rendered via Lit's safe svg template; no innerHTML.
// Geometries per spec Section 5 P0.1 closure (Material icon names in comments).
function iconFor(state: SyncState): TemplateResult {
  switch (state) {
    case 'healthy': // check_circle
      return svg`<svg viewBox="0 0 24 24" aria-hidden="true">
        <path fill="currentColor" d="M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z"/>
      </svg>`;
    case 'stale': // schedule
      return svg`<svg viewBox="0 0 24 24" aria-hidden="true">
        <path fill="currentColor" d="M12 2a10 10 0 100 20 10 10 0 000-20zm.5 5H11v6l5.2 3.2.8-1.3-4.5-2.7z"/>
      </svg>`;
    case 'offline': // cloud_off
      return svg`<svg viewBox="0 0 24 24" aria-hidden="true">
        <path fill="currentColor" d="M19.35 10.04A7.49 7.49 0 0012 4c-1.48 0-2.85.43-4.01 1.17l1.46 1.46A5.5 5.5 0 0117.5 12a5.5 5.5 0 01-5.5 5.5c-.3 0-.58-.03-.86-.08L2.8 9.1A9.01 9.01 0 002 12c0 3.87 3.13 7 7 7h10c2.76 0 5-2.24 5-5s-2.24-5-5-5zM1 4.27l2.28 2.28A8.95 8.95 0 001 12c0 4.97 4.03 9 9 9h8.73l2 2L22.73 22 2.27 3z"/>
      </svg>`;
    case 'conflict': // call_split
      return svg`<svg viewBox="0 0 24 24" aria-hidden="true">
        <path fill="currentColor" d="M14 4l5 5-5 5v-3H9.5l-3 3-1.4-1.4L7.6 10 5 7.4 6.4 6l3 3H14V6z"/>
      </svg>`;
    case 'quarantine': // do_not_disturb_on
      return svg`<svg viewBox="0 0 24 24" aria-hidden="true">
        <circle cx="12" cy="12" r="10" fill="none" stroke="currentColor" stroke-width="2"/>
        <path fill="currentColor" d="M5 11h14v2H5z"/>
      </svg>`;
  }
}

const LABELS_SHORT: Record<SyncState, string> = {
  healthy: 'Synced',
  stale: 'Stale',
  offline: 'Offline',
  conflict: 'Conflict',
  quarantine: 'Held',
};

const LABELS_LONG: Record<SyncState, string> = {
  healthy: 'Synced with all peers',
  stale: 'Last synced earlier',
  offline: 'Offline — saved locally',
  conflict: 'Review required — two versions diverged',
  quarantine: "Can't sync — open diagnostics",
};

@customElement('sunfish-syncstate-indicator')
export class SunfishSyncstateIndicator extends LitElement {
  static styles = css`
    :host { display: inline-flex; align-items: center; gap: var(--sf-space-2, 8px); font: inherit; }
    /* Palette per ADR 0036 (Paul Tol vibrant) — CVD-vetted within tracked exceptions. */
    :host([state="healthy"])    { color: var(--sf-syncstate-healthy-bg, #117733); }
    :host([state="stale"])      { color: var(--sf-syncstate-stale-bg, #0077bb); }
    :host([state="offline"])    { color: var(--sf-syncstate-offline-bg, #888888); }
    :host([state="conflict"])   { color: var(--sf-syncstate-conflict-bg, #ee7733); }
    :host([state="quarantine"]) { color: var(--sf-syncstate-quarantine-bg, #cc3311); }
    svg { inline-size: 20px; block-size: 20px; flex-shrink: 0; }
    .label {
      max-inline-size: var(--sf-syncstate-label-max, 28ch);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    :host([form="compact"]) .label { --sf-syncstate-label-max: 10ch; }
  `;

  @property({ type: String, reflect: true }) state: SyncState = 'healthy';
  @property({ type: String, reflect: true }) form: 'compact' | 'standard' = 'standard';

  render() {
    const labelText = this.form === 'compact' ? LABELS_SHORT[this.state] : LABELS_LONG[this.state];
    const role = (this.state === 'conflict' || this.state === 'quarantine') ? 'alert' : 'status';
    return html`
      <span role=${role} aria-atomic="true" aria-label=${LABELS_LONG[this.state]}>
        ${iconFor(this.state)}
        <span class="label">${labelText}</span>
      </span>
    `;
  }
}
