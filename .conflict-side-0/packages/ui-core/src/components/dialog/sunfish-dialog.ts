import { LitElement, html, css, type PropertyValues } from 'lit';
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

  /**
   * Auto-focus the first focusable child when the dialog becomes open. Honors
   * the ADR 0034 a11y contract `focus.initial = "first-focusable-child"`. Looks
   * up the slotted children (since dialog body content is projected via <slot>)
   * and focuses the first focusable one. Falls back to focusing the dialog
   * container so screen readers announce the dialog title.
   */
  protected updated(changedProperties: PropertyValues): void {
    super.updated(changedProperties);
    if (changedProperties.has('open') && this.open) {
      this._focusFirstFocusableChild();
    }
  }

  private _focusFirstFocusableChild(): void {
    const focusableSelector =
      'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    const slot = this.shadowRoot?.querySelector('slot');
    const slottedTargets: Element[] = (slot?.assignedElements({ flatten: true }) ?? [])
      .flatMap((el) => [
        ...(el.matches(focusableSelector) ? [el] : []),
        ...Array.from(el.querySelectorAll(focusableSelector)),
      ]);

    const target = (slottedTargets[0] as HTMLElement | undefined)
      ?? (this.shadowRoot?.querySelector('.dialog') as HTMLElement | null);

    if (target) {
      // Custom elements with shadow roots may need tabindex=-1 to be focusable.
      if (!target.hasAttribute('tabindex') && !target.matches(focusableSelector)) {
        target.setAttribute('tabindex', '-1');
      }
      // Defer to next microtask so layout settles after the open transition.
      queueMicrotask(() => target.focus());
    }
  }
}
