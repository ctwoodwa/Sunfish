import { useEffect, useRef, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { useCssProvider } from '../../CssProviderContext';

export interface SunfishDialogProps {
  /** Whether the dialog is visible. */
  open: boolean;
  /** Fired when the dialog requests to close (close button / overlay / Esc). */
  onClose: () => void;
  /** Title displayed in the header. */
  title?: ReactNode;
  /** Whether to show the close button in the title bar. Default true. */
  showCloseButton?: boolean;
  /** Whether clicking the backdrop closes the dialog. Default true. */
  closeOnOverlayClick?: boolean;
  /** Whether pressing Escape closes the dialog. Default true. */
  closeOnEsc?: boolean;
  /** Body content. */
  children?: ReactNode;
  /** Optional footer slot (action buttons). */
  footer?: ReactNode;
  /** Optional inline width. */
  width?: string | number;
  /** Optional inline height. */
  height?: string | number;
}

/**
 * React parity port of `packages/ui-adapters-blazor/Components/Feedback/Dialog/SunfishDialog.razor`.
 *
 * Renders into a portal on `document.body`. Emits the ADR-0023 slot
 * classes (DialogDialog / DialogContent / Header / Title / Body / Footer).
 * Server-side rendering: when `document` is undefined the portal is
 * skipped and nothing renders — matches Blazor's prerender behavior.
 */
export function SunfishDialog({
  open,
  onClose,
  title,
  showCloseButton = true,
  closeOnOverlayClick = true,
  closeOnEsc = true,
  children,
  footer,
  width,
  height,
}: SunfishDialogProps) {
  const provider = useCssProvider();
  const closeBtnRef = useRef<HTMLButtonElement | null>(null);

  useEffect(() => {
    if (!open || !closeOnEsc) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, closeOnEsc, onClose]);

  // The provider's close markup is a trusted, author-controlled HTML
  // fragment (inline SVG for Fluent, Material Symbols span for M3, empty
  // for BS5 — see each provider's `dialogCloseMarkup()` implementation).
  // We inject it via imperative `innerHTML` under a ref rather than
  // `dangerouslySetInnerHTML` because the value never comes from user
  // input or props; it is a closed enum of provider-authored strings.
  useEffect(() => {
    if (!showCloseButton) return;
    const el = closeBtnRef.current;
    if (!el) return;
    const markup = provider.dialogCloseMarkup();
    if (el.innerHTML !== markup) {
      el.innerHTML = markup;
    }
  }, [provider, showCloseButton, open]);

  if (!open) return null;
  if (typeof document === 'undefined') return null;

  const handleOverlayClick = () => {
    if (closeOnOverlayClick) onClose();
  };

  const stop = (e: React.MouseEvent) => e.stopPropagation();

  const dimensionStyle: React.CSSProperties = {};
  if (width !== undefined) dimensionStyle.width = width;
  if (height !== undefined) dimensionStyle.height = height;

  return createPortal(
    <div
      className={provider.dialogOverlayClass()}
      onClick={handleOverlayClick}
      data-testid="sf-dialog-overlay"
    >
      <div
        className={provider.dialogClass()}
        role="dialog"
        aria-modal="true"
        tabIndex={-1}
        style={dimensionStyle}
        onClick={stop}
      >
        <div className={provider.dialogDialogClass()}>
          <div className={provider.dialogContentClass()}>
            {(title || showCloseButton) && (
              <div className={provider.dialogHeaderClass()}>
                <span className={provider.dialogTitleClass()}>{title}</span>
                {showCloseButton && (
                  <button
                    ref={closeBtnRef}
                    type="button"
                    className={`sf-dialog__close ${provider.dialogCloseButtonClass()}`.trim()}
                    aria-label="Close"
                    onClick={onClose}
                  />
                )}
              </div>
            )}
            <div className={provider.dialogBodyClass()}>{children}</div>
            {footer && <div className={provider.dialogFooterClass()}>{footer}</div>}
          </div>
        </div>
      </div>
    </div>,
    document.body,
  );
}
