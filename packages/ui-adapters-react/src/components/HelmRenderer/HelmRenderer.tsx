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
  /** The ambient render context passed to each widget's `compute`. Required for output. */
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
 * View-states are pre-computed via `useEffect` when `registry` or
 * `context` changes. The effect fires async `widget.compute` for all
 * widgets in parallel, then batches the results into a single state
 * update. An `AbortController` cancels in-flight computations on
 * registry/context change or unmount.
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
          .then((state) => [widget.metadata.widgetId, state] as const)
          .catch(() => null),
      ),
    ).then((results) => {
      if (!active) return;
      const map = new Map<string, HelmWidgetViewState>();
      for (const entry of results) {
        if (entry) map.set(entry[0], entry[1]);
      }
      setViewStates(map);
    });

    return () => {
      active = false;
      controller.abort();
    };
  }, [registry, context]);

  if (!registry || !context || viewStates === null) {
    return null;
  }

  return (
    <nav className="sunfish-helm" aria-label={helmAriaLabel}>
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
