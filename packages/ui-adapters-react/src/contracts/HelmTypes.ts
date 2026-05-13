/**
 * TypeScript projections of the `Sunfish.UICore.Wayfinder` Helm contract
 * surface per ADR 0066 §1.
 *
 * Mirror of:
 *   `packages/ui-core/Wayfinder/IHelmWidget.cs`
 *   `packages/ui-core/Wayfinder/IHelmWidgetRegistry.cs`
 *
 * Enum string literals match `JsonStringEnumConverter` output (PascalCase).
 * Field names mirror C# record property names exactly, converted to
 * camelCase per the project's `JsonNamingPolicy.CamelCase` serialiser default.
 *
 * W#53 Phase 2 PR 2c — React parity port. H9 parity gate target.
 */

// ===== Enum string-literal unions =====

export const HelmSlot = {
  GlanceBand: 'GlanceBand',
  ActionStack: 'ActionStack',
  ActivityFeed: 'ActivityFeed',
} as const;
export type HelmSlot = (typeof HelmSlot)[keyof typeof HelmSlot];

export const HelmActionInvocationKind = {
  Navigate: 'Navigate',
  IssueStandingOrder: 'IssueStandingOrder',
  RunLocalCommand: 'RunLocalCommand',
} as const;
export type HelmActionInvocationKind =
  (typeof HelmActionInvocationKind)[keyof typeof HelmActionInvocationKind];

// ===== Interfaces =====

/** Mirror of `HelmWidgetMetadata` record (C# positional record). */
export interface HelmWidgetMetadata {
  widgetId: string;
  slot: HelmSlot;
  orderHint: number;
  accessibleName: string;
}

/** Mirror of `HelmWidgetAction` record. */
export interface HelmWidgetAction {
  actionId: string;
  accessibleLabel: string;
  kind: HelmActionInvocationKind;
  /**
   * For `IssueStandingOrder`, format is `"{Path}|{Scope}"` — split on the
   * first `'|'` to recover the Standing Order path and scope string.
   * Matches Blazor `HelmRenderer.razor` wire contract.
   */
  target: string;
}

/** Mirror of `HelmWidgetViewState` record. */
export interface HelmWidgetViewState {
  primaryLabel: string;
  secondaryLabel?: string;
  actions: readonly HelmWidgetAction[];
}

/**
 * Ambient render context passed to each widget's `compute` call.
 * TypeScript projection of `HelmRenderContext` (C# record). ISO-8601
 * strings replace NodaTime + value-type IDs since they are opaque at
 * the adapter boundary.
 */
export interface HelmRenderContext {
  tenantId: string;
  actorId: string;
  activeTeamId?: string;
  /** ISO-8601 instant, e.g. `"2026-05-13T00:00:00Z"`. */
  now: string;
}

/**
 * TypeScript mirror of `IHelmWidget`. Implementations call `compute`
 * to produce a `HelmWidgetViewState`. `signal` replaces
 * `CancellationToken` — abort the signal to cancel the computation.
 */
export interface HelmWidget {
  readonly metadata: HelmWidgetMetadata;
  compute(context: HelmRenderContext, signal: AbortSignal): Promise<HelmWidgetViewState>;
}

/**
 * TypeScript mirror of `IHelmWidgetRegistry`. Widgets are pre-sorted
 * by slot then `orderHint` (lowest first) by the registry implementation.
 * `getSlot` returns widgets in the same stable order.
 */
export interface HelmWidgetRegistry {
  readonly widgets: readonly HelmWidget[];
  getSlot(slot: HelmSlot): readonly HelmWidget[];
}
