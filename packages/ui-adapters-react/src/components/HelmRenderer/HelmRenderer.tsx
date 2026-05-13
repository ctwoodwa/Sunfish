import { useEffect, useState, type FC } from 'react';
import {
  HelmSlot,
  type HelmRenderContext,
  type HelmWidgetAction,
  type HelmWidgetRegistry,
  type HelmWidgetViewState,
} from '../../contracts/HelmTypes';

export interface HelmRendererProps {
  /** The Helm widget registry to render. Required for output. */
  registry: HelmWidgetRegistry | undefined;
  /**
   * The ambient render context passed to each widget's `compute`. Required for output.
   *
   * @remarks Memoize the `context` object (or avoid passing a freshly constructed literal
   * each render) to prevent recomputing all widgets on every parent re-render, since the
   * `useEffect` dependency tracks object identity. Parity behaviour with Blazor
   * `OnParametersSetAsync` which re-runs on any parameter change.
   */
  context: HelmRenderContext | undefined;
  /**
   * Accessible label on the outer `<nav>` landmark per WCAG 2.4.6 + 4.1.2.
   * Defaults to `"Helm"`.
   * TODO(SUNFISH_I18N_002): replace default with i18n lookup when the cascade ships.
   */
  helmAriaLabel?: string;
  /** Accessible label for the GlanceBand slot group. Defaults to `"Status"`. */
  glanceBandSlotLabel?: string;
  /** Accessible label for the ActionStack slot group. Defaults to `"Actions"`. */
  actionStackSlotLabel?: string;
  /** Accessible label for the ActivityFeed slot group. Defaults to `"Activity"`. */
  activityFeedSlotLabel?: string;
}

/**
 * React parity port of `HelmRenderer.razor` (W#53 Phase 2 PR 2c).
 *
 * Renders the registered Helm widgets across three slots: GlanceBand →
 * ActionStack → ActivityFeed. Each widget renders in a `<section>` with
 * `aria-label` = `widget.metadata.accessibleName` (WCAG 4.1.2). The
 * `sync-state` widget region carries `aria-live="polite"` (WCAG 4.1.3)
 * so SyncState transitions are announced to screen readers.
 *
 * The outer `<nav>` landmark is rendered unconditionally (parity with Blazor
 * where the `<nav>` is always in the DOM; only inner slot markup is gated on
 * `Registry is not null && Context is not null`). Slot content is omitted
 * while `registry` or `context` are undefined, or while the initial
 * view-state computation is in flight.
 *
 * View-states are pre-computed via `useEffect` when `registry` or `context`
 * changes. The effect fires async `widget.compute` for all widgets in
 * parallel, then batches the results into a single state update. An
 * `AbortController` + `active` flag cancel in-flight computations on
 * registry/context change or unmount.
 *
 * Compute errors (excluding `AbortError` cancellation) are stored in state
 * and thrown during the next render to surface to the nearest React error
 * boundary — parity with Blazor `OnParametersSetAsync` where a throwing
 * widget aborts the batch and surfaces to the component error boundary.
 *
 * Action-button wire contract (parity with `HelmRenderer.razor`):
 * - `data-action-id`     → `HelmWidgetAction.actionId`
 * - `data-action-kind`   → `HelmWidgetAction.kind` (`"Navigate"` |
 *                          `"IssueStandingOrder"` | `"RunLocalCommand"`)
 * - `data-action-target` → `HelmWidgetAction.target`. For
 *                          `IssueStandingOrder`, format is `"{Path}|{Scope}"`.
 */
export const HelmRenderer: FC<HelmRendererProps> = ({
  registry,
  context,
  helmAriaLabel = 'Helm',
  glanceBandSlotLabel = 'Status',
  actionStackSlotLabel = 'Actions',
  activityFeedSlotLabel = 'Activity',
}) => {
  const [viewStates, setViewStates] = useState<Map<string, HelmWidgetViewState> | null>(null);
  const [computeError, setComputeError] = useState<unknown>(null);

  // Propagate compute errors to the nearest React error boundary — parity
  // with Blazor OnParametersSetAsync where a throwing widget surfaces to the
  // component error boundary rather than being silently swallowed.
  if (computeError !== null) throw computeError;

  useEffect(() => {
    if (!registry || !context) {
      setViewStates(null);
      return;
    }

    const controller = new AbortController();
    let active = true;

    Promise.all(
      registry.widgets.map((widget) =>
        widget
          .compute(context, controller.signal)
          .then((state) => [widget.metadata.widgetId, state] as const),
      ),
    )
      .then((results) => {
        if (!active) return;
        setViewStates(new Map(results));
      })
      .catch((err) => {
        if (!active) return;
        // AbortError = expected cancellation from cleanup — ignore silently.
        if (err instanceof DOMException && err.name === 'AbortError') return;
        setComputeError(err);
      });

    return () => {
      active = false;
      controller.abort();
    };
  }, [registry, context]);

  // Always render the <nav> landmark unconditionally (parity with Blazor
  // where <nav class="sunfish-helm"> is always in the DOM). Slot content is
  // gated on registry + context + resolved view-states being available.
  const ready = registry !== undefined && context !== undefined && viewStates !== null;

  return (
    <nav className="sunfish-helm" aria-label={helmAriaLabel}>
      {ready && (
        <>
          <SlotGroup
            slot={HelmSlot.GlanceBand}
            slotClass="sunfish-helm-glance"
            slotAriaLabel={glanceBandSlotLabel}
            registry={registry}
            viewStates={viewStates}
          />
          <SlotGroup
            slot={HelmSlot.ActionStack}
            slotClass="sunfish-helm-actionstack"
            slotAriaLabel={actionStackSlotLabel}
            registry={registry}
            viewStates={viewStates}
          />
          <SlotGroup
            slot={HelmSlot.ActivityFeed}
            slotClass="sunfish-helm-activityfeed"
            slotAriaLabel={activityFeedSlotLabel}
            registry={registry}
            viewStates={viewStates}
          />
        </>
      )}
    </nav>
  );
};

interface SlotGroupProps {
  slot: HelmSlot;
  slotClass: string;
  slotAriaLabel: string;
  registry: HelmWidgetRegistry;
  viewStates: Map<string, HelmWidgetViewState>;
}

const SlotGroup: FC<SlotGroupProps> = ({ slot, slotClass, slotAriaLabel, registry, viewStates }) => {
  const widgets = registry.getSlot(slot);

  return (
    <div className={slotClass} role="group" aria-label={slotAriaLabel}>
      {widgets.map((widget) => {
        const view = viewStates.get(widget.metadata.widgetId);
        if (!view) return null;
        const isSyncState = widget.metadata.widgetId === 'sync-state';

        return (
          <section
            key={widget.metadata.widgetId}
            className="sunfish-helm-widget"
            data-widget-id={widget.metadata.widgetId}
            aria-label={widget.metadata.accessibleName}
            aria-live={isSyncState ? 'polite' : undefined}
          >
            <div className="sunfish-helm-widget-primary">{view.primaryLabel}</div>
            {view.secondaryLabel && (
              <div className="sunfish-helm-widget-secondary">{view.secondaryLabel}</div>
            )}
            {view.actions.length > 0 && (
              <div
                className="sunfish-helm-widget-actions"
                role="group"
                aria-label={`${widget.metadata.accessibleName} actions`}
              >
                {view.actions.map((action) => (
                  <ActionButton key={action.actionId} action={action} />
                ))}
              </div>
            )}
          </section>
        );
      })}
    </div>
  );
};

interface ActionButtonProps {
  action: HelmWidgetAction;
}

const ActionButton: FC<ActionButtonProps> = ({ action }) => (
  // aria-label deliberately omitted: visible button text IS the accessible name
  // per WCAG 2.5.3 (Label in Name). Parity with HelmRenderer.razor comment.
  <button
    type="button"
    className="sunfish-helm-action"
    data-action-id={action.actionId}
    data-action-kind={action.kind}
    data-action-target={action.target}
  >
    {action.accessibleLabel}
  </button>
);
