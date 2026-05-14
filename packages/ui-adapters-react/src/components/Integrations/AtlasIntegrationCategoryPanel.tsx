import { useId, useState } from 'react';
import type {
  IntegrationCategory,
  IntegrationProviderSchema,
  ActiveProviderSnapshot,
  IntegrationValidationResult,
  ReactIntegrationAtlasProvider,
} from '../../contracts/Integrations';
import { ProviderValidationStatus } from '../../contracts/Integrations';
import { AtlasCredentialField } from './AtlasCredentialField';

export interface AtlasIntegrationCategoryPanelProps {
  category: IntegrationCategory;
  schemas: IntegrationProviderSchema[];
  activeProvider?: ActiveProviderSnapshot | null;
  currentStatus?: ProviderValidationStatus;
  provider: ReactIntegrationAtlasProvider;
  onAnnounce?: (message: string, politeness: 'polite' | 'assertive') => void;
}

const CATEGORY_LABELS: Record<string, string> = {
  Payments: 'Payments',
  TransactionalEmail: 'Transactional Email',
  MarketingEmail: 'Marketing Email',
  Messaging: 'Messaging',
  MeshVpn: 'Mesh VPN',
  Captcha: 'CAPTCHA',
};

function categoryLabel(cat: IntegrationCategory): string {
  return CATEGORY_LABELS[cat] ?? String(cat);
}

function StatusBadge({ status, result }: { status: ProviderValidationStatus | null; result: IntegrationValidationResult | null }) {
  if (status === null) return null;
  if (status === ProviderValidationStatus.Valid) {
    return (
      <span className="atlas-status atlas-status--valid" aria-label="Connected">
        <span aria-hidden="true">✓</span> Connected
      </span>
    );
  }
  if (status === ProviderValidationStatus.Invalid) {
    const msg = result?.errorMessage ?? result?.errorCode ?? 'check credentials';
    return (
      <span className="atlas-status atlas-status--invalid" aria-label={`Invalid: ${msg}`}>
        <span aria-hidden="true">✕</span> {result?.errorMessage ?? result?.errorCode ?? 'Invalid credentials'}
      </span>
    );
  }
  if (status === ProviderValidationStatus.Unreachable) {
    const msg = result?.errorMessage ?? 'endpoint not reachable';
    return (
      <span className="atlas-status atlas-status--unreachable" aria-label={`Unreachable: ${msg}`}>
        <span aria-hidden="true">⚠</span> {result?.errorMessage ?? 'Endpoint unreachable'}
      </span>
    );
  }
  return (
    <span className="atlas-status atlas-status--unknown" aria-label="Not validated">
      <span aria-hidden="true">○</span> Not validated
    </span>
  );
}

/**
 * Per-category tab-panel for the Atlas Integration-Config surface.
 * Mirrors `AtlasIntegrationCategoryPanel.razor` (Anchor/Bridge Blazor).
 * WCAG: SC 4.1.2 (aria-disabled validate button), SC 4.1.3 (aria-live/alert status region),
 * SC 1.4.1 (shape-distinct status icons), SC 3.3.7 (leave-unchanged placeholder via AtlasCredentialField).
 */
export function AtlasIntegrationCategoryPanel({
  category,
  schemas,
  activeProvider,
  currentStatus,
  provider,
  onAnnounce,
}: AtlasIntegrationCategoryPanelProps) {
  const selectId = useId();
  const panelId = `atlas-panel-${category}`;
  const tabId = `atlas-tab-${category}`;

  const [selectedProviderId, setSelectedProviderId] = useState<string>(
    activeProvider?.providerId ?? '',
  );
  const [isValidating, setIsValidating] = useState(false);
  const [lastStatus, setLastStatus] = useState<ProviderValidationStatus | null>(currentStatus ?? null);
  const [lastResult, setLastResult] = useState<IntegrationValidationResult | null>(null);
  const [pendingSensitive, setPendingSensitive] = useState<Map<string, string>>(new Map());
  const [pendingNonSensitive, setPendingNonSensitive] = useState<Map<string, unknown>>(new Map());

  const selectedSchema = schemas.find((s) => s.providerId === selectedProviderId) ?? null;
  const validateLabel = isValidating
    ? `Validating ${selectedSchema?.displayName ?? categoryLabel(category)}…`
    : `Validate ${selectedSchema?.displayName ?? categoryLabel(category)}`;

  async function handleProviderChange(e: React.ChangeEvent<HTMLSelectElement>) {
    const pid = e.target.value;
    setSelectedProviderId(pid);
    setPendingSensitive(new Map());
    setPendingNonSensitive(new Map());
    if (pid) {
      await provider.issueProviderChange(category, pid);
      setLastStatus(ProviderValidationStatus.Unknown);
      setLastResult(null);
      const schema = schemas.find((s) => s.providerId === pid);
      onAnnounce?.(
        `Provider changed to ${schema?.displayName ?? pid}. Validation status reset.`,
        'polite',
      );
    }
  }

  function handleSensitiveChanged(key: string, value: string) {
    setPendingSensitive((prev) => new Map(prev).set(key, value));
  }

  function handleNonSensitiveChanged(key: string, value: unknown) {
    setPendingNonSensitive((prev) => new Map(prev).set(key, value));
  }

  async function handleValidate() {
    if (isValidating || !selectedProviderId) return;

    for (const [key, val] of pendingSensitive) {
      await provider.issueSensitiveCredential(category, selectedProviderId, key, val);
    }
    for (const [key, val] of pendingNonSensitive) {
      await provider.issueNonSensitiveCredential(category, selectedProviderId, key, val);
    }
    setPendingSensitive(new Map());
    setPendingNonSensitive(new Map());

    setIsValidating(true);
    setLastStatus(null);
    setLastResult(null);
    try {
      const result = await provider.validateProvider(category);
      setLastStatus(result.status);
      setLastResult(result);
      const politeness: 'polite' | 'assertive' =
        result.status === ProviderValidationStatus.Valid ? 'polite' : 'assertive';
      onAnnounce?.(statusAnnouncement(result), politeness);
    } catch {
      setLastStatus(ProviderValidationStatus.Unreachable);
      onAnnounce?.('Validation failed due to an unexpected error.', 'assertive');
    } finally {
      setIsValidating(false);
    }
  }

  const isAlertStatus =
    lastStatus === ProviderValidationStatus.Invalid ||
    lastStatus === ProviderValidationStatus.Unreachable;

  return (
    <div
      id={panelId}
      role="tabpanel"
      aria-labelledby={tabId}
      className="atlas-category-panel"
      aria-busy={isValidating ? true : undefined}
    >
      {schemas.length === 0 ? (
        <p className="atlas-panel-empty">No providers registered for {categoryLabel(category)}.</p>
      ) : (
        <>
          <div className="atlas-provider-row">
            <label htmlFor={selectId} className="atlas-cred-label">Active provider</label>
            <select
              id={selectId}
              className="atlas-select"
              aria-label={`Active provider for ${categoryLabel(category)}`}
              value={selectedProviderId}
              onChange={handleProviderChange}
            >
              <option value="">— None —</option>
              {schemas.map((schema) => (
                <option key={schema.providerId} value={schema.providerId}>
                  {schema.displayName}
                </option>
              ))}
            </select>
          </div>

          {selectedSchema && (
            <>
              <fieldset
                className="atlas-credential-fieldset"
                aria-label={`Credentials for ${selectedSchema.displayName}`}
              >
                <legend className="atlas-fieldset-legend">{selectedSchema.displayName} credentials</legend>
                {selectedSchema.credentialFields.map((field) => (
                  <AtlasCredentialField
                    key={field.key}
                    field={field}
                    hasExistingValue={activeProvider != null}
                    onSensitiveChanged={handleSensitiveChanged}
                    onNonSensitiveChanged={handleNonSensitiveChanged}
                  />
                ))}
              </fieldset>

              <div className="atlas-panel-actions">
                <button
                  type="button"
                  className={`sf-btn sf-btn--primary${isValidating ? ' sf-btn--busy' : ''}`}
                  aria-label={validateLabel}
                  aria-disabled={isValidating ? true : undefined}
                  onClick={handleValidate}
                >
                  {isValidating ? 'Validating…' : 'Validate'}
                </button>
              </div>

              {/* SC 4.1.3: status live region */}
              <div
                className="atlas-status-region"
                aria-atomic="true"
                aria-live={isAlertStatus ? undefined : 'polite'}
                role={isAlertStatus ? 'alert' : undefined}
              >
                <StatusBadge status={lastStatus} result={lastResult} />
              </div>
            </>
          )}
        </>
      )}
    </div>
  );
}

function statusAnnouncement(result: IntegrationValidationResult): string {
  switch (result.status) {
    case ProviderValidationStatus.Valid: return 'Provider connected successfully.';
    case ProviderValidationStatus.Invalid:
      return `Validation failed: ${result.errorMessage ?? result.errorCode ?? 'invalid credentials.'}`;
    case ProviderValidationStatus.Unreachable: return 'Provider endpoint unreachable.';
    default: return 'Validation complete.';
  }
}
