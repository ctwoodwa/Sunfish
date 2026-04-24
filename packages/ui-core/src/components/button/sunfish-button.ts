import { LitElement, html, css } from 'lit';
import { customElement, property } from 'lit/decorators.js';

@customElement('sunfish-button')
export class SunfishButton extends LitElement {
  static styles = css`
    :host { display: inline-block; }
    button {
      font: inherit;
      padding-inline: var(--sf-space-4, 16px);
      padding-block: var(--sf-space-2, 8px);
      min-block-size: 24px;
      min-inline-size: 24px;
      background: var(--sf-color-primary, #2563eb);
      color: var(--sf-color-on-primary, #ffffff);
      border: 2px solid transparent;
      border-radius: var(--sf-radius-md, 4px);
      cursor: pointer;
    }
    button:focus-visible {
      outline: 3px solid var(--sf-color-focus-ring, #2563eb);
      outline-offset: 2px;
    }
    button:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
  `;

  @property({ type: String }) label = '';
  @property({ type: Boolean }) disabled = false;

  render() {
    return html`
      <button ?disabled=${this.disabled} aria-label=${this.label}>
        <slot>${this.label}</slot>
      </button>
    `;
  }
}
