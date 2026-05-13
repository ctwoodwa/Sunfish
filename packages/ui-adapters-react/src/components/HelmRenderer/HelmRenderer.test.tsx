import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { HelmRenderer } from './HelmRenderer';
import {
  HelmSlot,
  HelmActionInvocationKind,
  type HelmWidget,
  type HelmWidgetRegistry,
  type HelmWidgetViewState,
  type HelmRenderContext,
} from '../../contracts/HelmTypes';

// ===== Fixtures =====

const makeContext = (): HelmRenderContext => ({
  tenantId: 'tenant-1',
  actorId: 'actor-1',
  now: '2026-05-13T00:00:00Z',
});

const makeViewState = (
  primary: string,
  secondary?: string,
  actions: HelmWidgetViewState['actions'] = [],
): HelmWidgetViewState => ({ primaryLabel: primary, secondaryLabel: secondary, actions });

function makeWidget(
  id: string,
  slot: HelmSlot,
  orderHint: number,
  viewState: HelmWidgetViewState,
): HelmWidget {
  return {
    metadata: { widgetId: id, slot, orderHint, accessibleName: `${id} widget` },
    compute: vi.fn().mockResolvedValue(viewState),
  };
}

function makeRegistry(widgets: HelmWidget[]): HelmWidgetRegistry {
  return {
    widgets,
    getSlot: (slot) => widgets.filter((w) => w.metadata.slot === slot),
  };
}

// A full set of fixtures mirroring the 6 canonical widgets
function makeCanonicalRegistry(): HelmWidgetRegistry {
  const identityGlance = makeWidget(
    'identity-glance',
    HelmSlot.GlanceBand,
    100,
    makeViewState('Identity', undefined, [
      {
        actionId: 'rotate-key',
        accessibleLabel: 'Rotate key',
        kind: HelmActionInvocationKind.Navigate,
        target: 'wayfinder/identity/key-rotation',
      },
    ]),
  );
  const syncState = makeWidget('sync-state', HelmSlot.GlanceBand, 200, makeViewState('healthy'));
  const activeTeam = makeWidget('active-team', HelmSlot.GlanceBand, 300, makeViewState('Team A'));
  const missionSummary = makeWidget(
    'mission-summary',
    HelmSlot.GlanceBand,
    400,
    makeViewState('10 dimensions active'),
  );
  const quickToggles = makeWidget(
    'quick-toggles',
    HelmSlot.ActionStack,
    100,
    makeViewState('Toggles', undefined, [
      {
        actionId: 'offline-mode',
        accessibleLabel: 'Offline mode',
        kind: HelmActionInvocationKind.IssueStandingOrder,
        target: 'system.network.offline|Platform',
      },
    ]),
  );
  const recentOrders = makeWidget(
    'recent-orders',
    HelmSlot.ActivityFeed,
    100,
    makeViewState('No recent orders'),
  );

  return makeRegistry([identityGlance, syncState, activeTeam, missionSummary, quickToggles, recentOrders]);
}

// ===== Tests =====

describe('HelmRenderer', () => {
  describe('null guard (parity with Blazor OnParametersSetAsync null-guard)', () => {
    it('renders nothing when registry is undefined', () => {
      const { container } = render(
        <HelmRenderer registry={undefined} context={makeContext()} />,
      );
      expect(container.firstChild).toBeNull();
    });

    it('renders nothing when context is undefined', async () => {
      const widget = makeWidget('x', HelmSlot.GlanceBand, 1, makeViewState('X'));
      const registry = makeRegistry([widget]);
      const { container } = render(
        <HelmRenderer registry={registry} context={undefined} />,
      );
      expect(container.firstChild).toBeNull();
    });
  });

  describe('WCAG 2.4.6 — nav landmark has descriptive label', () => {
    it('renders <nav> with default aria-label "Helm"', async () => {
      const registry = makeCanonicalRegistry();
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() => expect(screen.getByRole('navigation', { name: 'Helm' })).toBeInTheDocument());
    });

    it('renders <nav> with custom helmAriaLabel', async () => {
      const registry = makeCanonicalRegistry();
      render(
        <HelmRenderer registry={registry} context={makeContext()} helmAriaLabel="Main Helm" />,
      );
      await waitFor(() =>
        expect(screen.getByRole('navigation', { name: 'Main Helm' })).toBeInTheDocument(),
      );
    });
  });

  describe('WCAG 4.1.2 — all widgets have accessible name (aria-label = accessibleName)', () => {
    it('every rendered widget section has aria-label matching its accessibleName', async () => {
      const registry = makeCanonicalRegistry();
      render(<HelmRenderer registry={registry} context={makeContext()} />);

      // 6 canonical widgets — wait for identity-glance to confirm render
      await waitFor(() =>
        expect(screen.getByRole('region', { name: 'identity-glance widget' })).toBeInTheDocument(),
      );

      const expectedNames = [
        'identity-glance widget',
        'sync-state widget',
        'active-team widget',
        'mission-summary widget',
        'quick-toggles widget',
        'recent-orders widget',
      ];

      for (const name of expectedNames) {
        expect(screen.getByRole('region', { name })).toBeInTheDocument();
      }
    });
  });

  describe('WCAG 4.1.3 — SyncState widget has aria-live="polite"', () => {
    it('sync-state widget section has aria-live="polite"', async () => {
      const registry = makeCanonicalRegistry();
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() =>
        expect(screen.getByRole('region', { name: 'sync-state widget' })).toBeInTheDocument(),
      );
      const syncSection = screen.getByRole('region', { name: 'sync-state widget' });
      expect(syncSection).toHaveAttribute('aria-live', 'polite');
    });

    it('non-sync widgets do NOT have aria-live', async () => {
      const registry = makeCanonicalRegistry();
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() =>
        expect(screen.getByRole('region', { name: 'identity-glance widget' })).toBeInTheDocument(),
      );
      const identitySection = screen.getByRole('region', { name: 'identity-glance widget' });
      expect(identitySection).not.toHaveAttribute('aria-live');
    });
  });

  describe('slot groups', () => {
    it('GlanceBand slot group has role="group" with default label "Status"', async () => {
      const registry = makeCanonicalRegistry();
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() =>
        expect(screen.getByRole('group', { name: 'Status' })).toBeInTheDocument(),
      );
    });

    it('ActionStack slot group has role="group" with default label "Actions"', async () => {
      const registry = makeCanonicalRegistry();
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() =>
        expect(screen.getByRole('group', { name: 'Actions' })).toBeInTheDocument(),
      );
    });

    it('ActivityFeed slot group has role="group" with default label "Activity"', async () => {
      const registry = makeCanonicalRegistry();
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() =>
        expect(screen.getByRole('group', { name: 'Activity' })).toBeInTheDocument(),
      );
    });

    it('custom slot labels are applied', async () => {
      const registry = makeCanonicalRegistry();
      render(
        <HelmRenderer
          registry={registry}
          context={makeContext()}
          glanceBandSlotLabel="Glance"
          actionStackSlotLabel="Quick Actions"
          activityFeedSlotLabel="Feed"
        />,
      );
      await waitFor(() =>
        expect(screen.getByRole('group', { name: 'Glance' })).toBeInTheDocument(),
      );
      expect(screen.getByRole('group', { name: 'Quick Actions' })).toBeInTheDocument();
      expect(screen.getByRole('group', { name: 'Feed' })).toBeInTheDocument();
    });
  });

  describe('action buttons — wire contract (parity with HelmRenderer.razor)', () => {
    it('renders action button with correct data-* attributes', async () => {
      const registry = makeCanonicalRegistry();
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() =>
        expect(screen.getByRole('button', { name: 'Rotate key' })).toBeInTheDocument(),
      );

      const btn = screen.getByRole('button', { name: 'Rotate key' });
      expect(btn).toHaveAttribute('data-action-id', 'rotate-key');
      expect(btn).toHaveAttribute('data-action-kind', 'Navigate');
      expect(btn).toHaveAttribute('data-action-target', 'wayfinder/identity/key-rotation');
      expect(btn).toHaveAttribute('type', 'button');
    });

    it('IssueStandingOrder action carries "{Path}|{Scope}" target', async () => {
      const registry = makeCanonicalRegistry();
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() =>
        expect(screen.getByRole('button', { name: 'Offline mode' })).toBeInTheDocument(),
      );

      const btn = screen.getByRole('button', { name: 'Offline mode' });
      expect(btn).toHaveAttribute('data-action-kind', 'IssueStandingOrder');
      expect(btn).toHaveAttribute('data-action-target', 'system.network.offline|Platform');
    });

    it('action button visible text IS the accessible name (no redundant aria-label)', async () => {
      const registry = makeCanonicalRegistry();
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() =>
        expect(screen.getByRole('button', { name: 'Rotate key' })).toBeInTheDocument(),
      );
      const btn = screen.getByRole('button', { name: 'Rotate key' });
      expect(btn).not.toHaveAttribute('aria-label');
    });

    it('action group has role="group" with label "{accessibleName} actions"', async () => {
      const registry = makeCanonicalRegistry();
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() =>
        expect(screen.getByRole('group', { name: 'identity-glance widget actions' })).toBeInTheDocument(),
      );
    });
  });

  describe('widget content rendering', () => {
    it('renders primaryLabel text', async () => {
      const widget = makeWidget('w1', HelmSlot.GlanceBand, 1, makeViewState('Primary text'));
      const registry = makeRegistry([widget]);
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() => expect(screen.getByText('Primary text')).toBeInTheDocument());
    });

    it('renders secondaryLabel when present', async () => {
      const widget = makeWidget('w1', HelmSlot.GlanceBand, 1, makeViewState('Primary', 'Secondary'));
      const registry = makeRegistry([widget]);
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() => expect(screen.getByText('Secondary')).toBeInTheDocument());
    });

    it('omits secondaryLabel element when absent', async () => {
      const widget = makeWidget('w1', HelmSlot.GlanceBand, 1, makeViewState('Primary'));
      const registry = makeRegistry([widget]);
      const { container } = render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() => expect(screen.getByText('Primary')).toBeInTheDocument());
      const secondary = container.querySelector('.sunfish-helm-widget-secondary');
      expect(secondary).toBeNull();
    });

    it('omits actions group when actions is empty', async () => {
      const widget = makeWidget('w1', HelmSlot.GlanceBand, 1, makeViewState('Primary', undefined, []));
      const registry = makeRegistry([widget]);
      const { container } = render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() => expect(screen.getByText('Primary')).toBeInTheDocument());
      const actionsGroup = container.querySelector('.sunfish-helm-widget-actions');
      expect(actionsGroup).toBeNull();
    });
  });

  describe('data-widget-id attribute (parity with Blazor data-widget-id)', () => {
    it('widget section carries data-widget-id', async () => {
      const widget = makeWidget('my-widget', HelmSlot.GlanceBand, 1, makeViewState('Label'));
      const registry = makeRegistry([widget]);
      const { container } = render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() => expect(screen.getByText('Label')).toBeInTheDocument());
      const section = container.querySelector('[data-widget-id="my-widget"]');
      expect(section).not.toBeNull();
    });
  });

  describe('CSS class parity (parity with HelmRenderer.razor class names)', () => {
    it('outer nav has className "sunfish-helm"', async () => {
      const widget = makeWidget('w', HelmSlot.GlanceBand, 1, makeViewState('L'));
      render(<HelmRenderer registry={makeRegistry([widget])} context={makeContext()} />);
      await waitFor(() => expect(screen.getByText('L')).toBeInTheDocument());
      const nav = screen.getByRole('navigation');
      expect(nav.className).toContain('sunfish-helm');
    });

    it('GlanceBand slot div has className "sunfish-helm-glance"', async () => {
      const widget = makeWidget('w', HelmSlot.GlanceBand, 1, makeViewState('L'));
      const { container } = render(
        <HelmRenderer registry={makeRegistry([widget])} context={makeContext()} />,
      );
      await waitFor(() => expect(screen.getByText('L')).toBeInTheDocument());
      expect(container.querySelector('.sunfish-helm-glance')).not.toBeNull();
    });

    it('ActionStack slot div has className "sunfish-helm-actionstack"', async () => {
      const widget = makeWidget('w', HelmSlot.ActionStack, 1, makeViewState('L'));
      const { container } = render(
        <HelmRenderer registry={makeRegistry([widget])} context={makeContext()} />,
      );
      await waitFor(() => expect(screen.getByText('L')).toBeInTheDocument());
      expect(container.querySelector('.sunfish-helm-actionstack')).not.toBeNull();
    });

    it('ActivityFeed slot div has className "sunfish-helm-activityfeed"', async () => {
      const widget = makeWidget('w', HelmSlot.ActivityFeed, 1, makeViewState('L'));
      const { container } = render(
        <HelmRenderer registry={makeRegistry([widget])} context={makeContext()} />,
      );
      await waitFor(() => expect(screen.getByText('L')).toBeInTheDocument());
      expect(container.querySelector('.sunfish-helm-activityfeed')).not.toBeNull();
    });
  });

  describe('compute concurrency', () => {
    it('calls compute on all widgets in the registry', async () => {
      const w1 = makeWidget('w1', HelmSlot.GlanceBand, 1, makeViewState('L1'));
      const w2 = makeWidget('w2', HelmSlot.ActionStack, 1, makeViewState('L2'));
      const registry = makeRegistry([w1, w2]);
      render(<HelmRenderer registry={registry} context={makeContext()} />);
      await waitFor(() => expect(screen.getByText('L1')).toBeInTheDocument());
      expect(w1.compute).toHaveBeenCalledTimes(1);
      expect(w2.compute).toHaveBeenCalledTimes(1);
    });

    it('passes the HelmRenderContext to each widget compute call', async () => {
      const ctx = makeContext();
      const widget = makeWidget('w', HelmSlot.GlanceBand, 1, makeViewState('L'));
      const registry = makeRegistry([widget]);
      render(<HelmRenderer registry={registry} context={ctx} />);
      await waitFor(() => expect(screen.getByText('L')).toBeInTheDocument());
      expect(widget.compute).toHaveBeenCalledWith(ctx, expect.any(AbortSignal));
    });
  });

  describe('HelmTypes contract — enum string values match C# JsonStringEnumConverter', () => {
    it('HelmSlot values are PascalCase strings', () => {
      expect(HelmSlot.GlanceBand).toBe('GlanceBand');
      expect(HelmSlot.ActionStack).toBe('ActionStack');
      expect(HelmSlot.ActivityFeed).toBe('ActivityFeed');
    });

    it('HelmActionInvocationKind values are PascalCase strings', () => {
      expect(HelmActionInvocationKind.Navigate).toBe('Navigate');
      expect(HelmActionInvocationKind.IssueStandingOrder).toBe('IssueStandingOrder');
      expect(HelmActionInvocationKind.RunLocalCommand).toBe('RunLocalCommand');
    });
  });
});
