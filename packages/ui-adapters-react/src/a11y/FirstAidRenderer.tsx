import React, { useId } from 'react';
import type { LiveRegionPoliteness } from './LiveAnnouncer';

/** Mirror of IFirstAidContract for the React adapter layer. */
export interface FirstAidContract {
  helpKey: string;
  nextActionHintKey?: string | null;
  liveAnnouncementPolicy?: LiveRegionPoliteness;
}

export interface FirstAidRendererProps {
  /** The First-Aid contract describing this surface's a11y requirements. */
  contract: FirstAidContract;
  /**
   * Pre-localized help text. Falls back to `contract.helpKey` when omitted.
   * Consumers SHOULD localize before passing; the component renders the string as-is.
   */
  helpText?: string;
  /**
   * Pre-localized next-action hint. Falls back to `contract.nextActionHintKey`.
   * Omitted from output when both are null/empty.
   */
  nextActionHintText?: string;
  /** Content wrapped by this First-Aid region. */
  children?: React.ReactNode;
}

/**
 * React adapter for the First-Aid baseline per ADR 0077 §4.
 * Wraps children in an aria-described region with WCAG-required help text.
 */
export function FirstAidRenderer({
  contract,
  helpText,
  nextActionHintText,
  children,
}: FirstAidRendererProps): React.ReactElement {
  const helpId = useId();

  const resolvedHelp = helpText?.trim() || contract.helpKey;
  const resolvedHint = nextActionHintText?.trim() || contract.nextActionHintKey;

  return (
    <div className="sf-first-aid" aria-describedby={helpId}>
      {/* Visually hidden help text — WCAG SC 3.3.2 */}
      <span id={helpId} className="sf-sr-only">
        {resolvedHelp}
      </span>
      {resolvedHint && (
        <span className="sf-sr-only sf-first-aid__hint">{resolvedHint}</span>
      )}
      {children}
    </div>
  );
}
