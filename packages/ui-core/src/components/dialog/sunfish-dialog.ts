import { LitElement, html, css } from 'lit';
import { customElement, property } from 'lit/decorators.js';

@customElement('sunfish-dialog')
export class SunfishDialog extends LitElement {
  static styles = css`
    :host { display: contents; }
    .backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.5);
      display: none;
      align-items: center;
      justify-content: center;
      z-index: 100;
    }
    :host([open]) .backdrop { display: flex; }
    .dialog {
      background: var(--sf-color-surface, #ffffff);
      color: var(--sf-color-on-surface, #0f172a);
      padding: var(--sf-space-6, 24px);
      border-radius: var(--sf-radius-lg, 8px);
      max-inline-size: min(90vw, 32rem);
      box-shadow: 0 10px 25px rgba(0, 0, 0, 0.15);
    }
    h2 { margin-block-start: 0; }
  `;

  @property({ type: String }) heading = '';
  @property({ type: Boolean, reflect: true }) open = false;

  render() {
    return html`
      <div class="backdrop" @click=${this._onBackdropClick}>
        <div class="dialog" role="dialog" aria-modal="true" aria-labelledby="dialog-title" @click=${this._stopPropagation}>
          <h2 id="dialog-title">${this.heading}</h2>
          <slot></slot>
        </div>
      </div>
    `;
  }

  private _onBackdropClick() {
    this.open = false;
    this.dispatchEvent(new CustomEvent('close'));
  }

  private _stopPropagation(e: Event) {
    e.stopPropagation();
  }

  connectedCallback() {
    super.connectedCallback();
    document.addEventListener('keydown', this._onKeydown);
  }
  disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener('keydown', this._onKeydown);
  }
  private _onKeydown = (e: KeyboardEvent) => {
    if (e.key === 'Escape' && this.open) {
      this.open = false;
      this.dispatchEvent(new CustomEvent('close'));
    }
  };
}
