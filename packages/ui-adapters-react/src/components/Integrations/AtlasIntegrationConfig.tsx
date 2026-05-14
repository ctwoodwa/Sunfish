import { useEffect, useId, useRef, useState } from 'react';
import type {
  IntegrationAtlasView,
  IntegrationProviderSchema,
  ReactIntegrationAtlasProvider,
} from '../../contracts/Integrations';
import { IntegrationCategory } from '../../contracts/Integrations';
import { AtlasIntegrationCategoryPanel } from './AtlasIntegrationCategoryPanel';

export interface AtlasIntegrationConfigProps {
  provider: ReactIntegrationAtlasProvider;
  /** Optional live-region announcer — host provides if needed. */
  onAnnounce?: (message: string, politeness: 'polite' | 'assertive') => void;
}

const CATEGORIES = Object.values(IntegrationCategory) as (keyof typeof IntegrationCategory)[];

const CATEGORY_LABELS: Record<string, string> = {
  Payments: 'Payments',
  TransactionalEmail: 'Transactional Email',
  MarketingEmail: 'Marketing Email',
  Messaging: 'Messaging',
  MeshVpn: 'Mesh VPN',
  Captcha: 'CAPTCHA',
};

/**
 * Top-level Atlas Integration-Config UI surface.
 * Mirrors `AtlasIntegrationConfig.razor` (Anchor/Bridge Blazor).
 * WCAG: APG Tabs (WAI-ARIA 1.2) — roving tabindex, arrow-key navigation,
 * Home/End, focus management via ref array.
 */
export function AtlasIntegrationConfig({ provider, onAnnounce }: AtlasIntegrationConfigProps) {
  const [schemas, setSchemas] = useState<IntegrationProviderSchema[]>([]);
  const [view, setView] = useState<IntegrationAtlasView | null>(null);
  const [activeIdx, setActiveIdx] = useState(0);
  const tabRefs = useRef<(HTMLButtonElement | null)[]>(new Array(CATEGORIES.length).fill(null));
  const tablistLabelId = useId();

  useEffect(() => {
    setSchemas(provider.getSchemas());
    let cancelled = false;
    provider.getAtlasView().then((v) => {
      if (!cancelled) setView(v);
    });
    return () => { cancelled = true; };
  }, [provider]);

  function handleKeyDown(e: React.KeyboardEvent<HTMLDivElement>) {
    let newIdx = -1;
    if (e.key === 'ArrowLeft') {
      newIdx = (activeIdx - 1 + CATEGORIES.length) % CATEGORIES.length;
    } else if (e.key === 'ArrowRight') {
      newIdx = (activeIdx + 1) % CATEGORIES.length;
    } else if (e.key === 'Home') {
      newIdx = 0;
    } else if (e.key === 'End') {
      newIdx = CATEGORIES.length - 1;
    }
    if (newIdx >= 0) {
      e.preventDefault();
      setActiveIdx(newIdx);
      tabRefs.current[newIdx]?.focus();
    }
  }

  if (view === null) {
    return <p role="status" aria-live="polite">Loading integrations…</p>;
  }

  const activeCategory = CATEGORIES[activeIdx];

  return (
    <div className="atlas-integration-config">
      <div
        role="tablist"
        aria-label="Integration categories"
        className="atlas-tablist"
        onKeyDown={handleKeyDown}
      >
        {CATEGORIES.map((cat, idx) => {
          const isActive = idx === activeIdx;
          return (
            <button
              key={cat}
              id={`atlas-tab-${cat}`}
              role="tab"
              className={`atlas-tab${isActive ? ' atlas-tab--active' : ''}`}
              aria-selected={isActive}
              aria-controls={`atlas-panel-${cat}`}
              tabIndex={isActive ? 0 : -1}
              ref={(el) => { tabRefs.current[idx] = el; }}
              onClick={() => setActiveIdx(idx)}
            >
              {CATEGORY_LABELS[cat] ?? cat}
            </button>
          );
        })}
      </div>

      {CATEGORIES.map((cat, idx) => {
        const isActive = idx === activeIdx;
        const schemasForCat = schemas.filter((s) => s.category === cat);
        const activeProvider = view.activeByCategory[cat] ?? null;
        const status = view.statusByCategory[cat];

        if (isActive) {
          return (
            <AtlasIntegrationCategoryPanel
              key={cat}
              category={cat}
              schemas={schemasForCat}
              activeProvider={activeProvider}
              currentStatus={status}
              provider={provider}
              onAnnounce={onAnnounce}
            />
          );
        }
        return (
          <div
            key={cat}
            id={`atlas-panel-${cat}`}
            role="tabpanel"
            aria-labelledby={`atlas-tab-${cat}`}
            hidden
          />
        );
      })}
    </div>
  );
}
