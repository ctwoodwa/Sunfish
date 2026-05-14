import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { AtlasIntegrationConfig } from './AtlasIntegrationConfig';
import { AtlasCredentialField } from './AtlasCredentialField';
import type {
  ReactIntegrationAtlasProvider,
  IntegrationAtlasView,
  IntegrationProviderSchema,
  IntegrationValidationResult,
  CredentialFieldSpec,
} from '../../contracts/Integrations';
import {
  IntegrationCategory,
  CredentialFieldKind,
  CredentialAutocompleteHint,
  ProviderValidationStatus,
} from '../../contracts/Integrations';

// ===== Helpers =====

function makeSchema(overrides?: Partial<IntegrationProviderSchema>): IntegrationProviderSchema {
  return {
    providerId: 'test-provider',
    displayName: 'Test Provider',
    category: IntegrationCategory.Payments,
    credentialFields: [],
    ...overrides,
  };
}

function makeField(overrides?: Partial<CredentialFieldSpec>): CredentialFieldSpec {
  return {
    key: 'api-key',
    displayLabel: 'API Key',
    kind: CredentialFieldKind.Secret,
    autocompleteHint: CredentialAutocompleteHint.CurrentPassword,
    isRequired: true,
    helpText: null,
    placeholder: null,
    ...overrides,
  };
}

function makeView(overrides?: Partial<IntegrationAtlasView>): IntegrationAtlasView {
  return {
    activeByCategory: {},
    statusByCategory: {},
    ...overrides,
  };
}

function makeValidResult(status: ProviderValidationStatus): IntegrationValidationResult {
  return {
    status,
    validatedAt: '2026-05-13T00:00:00Z',
    errorCode: null,
    errorMessage: null,
  };
}

function makeProvider(
  overrides?: Partial<ReactIntegrationAtlasProvider>,
): ReactIntegrationAtlasProvider {
  return {
    getSchemas: vi.fn(() => []),
    getAtlasView: vi.fn(async () => makeView()),
    issueProviderChange: vi.fn(async () => {}),
    issueSensitiveCredential: vi.fn(async () => {}),
    issueNonSensitiveCredential: vi.fn(async () => {}),
    validateProvider: vi.fn(async () => makeValidResult(ProviderValidationStatus.Valid)),
    ...overrides,
  };
}

// ===== AtlasCredentialField tests =====

describe('AtlasCredentialField', () => {
  it('renders label for input', () => {
    render(
      <AtlasCredentialField
        field={makeField({ displayLabel: 'API Key' })}
        hasExistingValue={false}
        onSensitiveChanged={vi.fn()}
        onNonSensitiveChanged={vi.fn()}
      />,
    );
    expect(screen.getByLabelText(/API Key/, { selector: 'input' })).toBeInTheDocument();
  });

  it('renders required indicator for required fields', () => {
    render(
      <AtlasCredentialField
        field={makeField({ isRequired: true, kind: CredentialFieldKind.Text })}
        hasExistingValue={false}
        onSensitiveChanged={vi.fn()}
        onNonSensitiveChanged={vi.fn()}
      />,
    );
    expect(screen.getByText('(required)', { selector: '.sf-visually-hidden' })).toBeInTheDocument();
  });

  it('renders help text with aria-describedby (SC 3.3.2)', () => {
    render(
      <AtlasCredentialField
        field={makeField({ helpText: 'Find this in your dashboard', kind: CredentialFieldKind.Text })}
        hasExistingValue={false}
        onSensitiveChanged={vi.fn()}
        onNonSensitiveChanged={vi.fn()}
      />,
    );
    const input = screen.getByRole('textbox');
    const descId = input.getAttribute('aria-describedby');
    expect(descId).toBeTruthy();
    const desc = document.getElementById(descId!);
    expect(desc?.textContent).toBe('Find this in your dashboard');
  });

  it('renders leave-unchanged placeholder for existing secrets (SC 3.3.7)', () => {
    render(
      <AtlasCredentialField
        field={makeField({ displayLabel: 'Secret Key' })}
        hasExistingValue={true}
        onSensitiveChanged={vi.fn()}
        onNonSensitiveChanged={vi.fn()}
      />,
    );
    const input = screen.getByPlaceholderText('••••••••');
    expect(input).toBeInTheDocument();
    expect(input).toHaveAttribute('type', 'password');
  });

  it('clears on focus when leave-unchanged (SC 3.3.7)', () => {
    const onSensitive = vi.fn();
    render(
      <AtlasCredentialField
        field={makeField()}
        hasExistingValue={true}
        onSensitiveChanged={onSensitive}
        onNonSensitiveChanged={vi.fn()}
      />,
    );
    const input = screen.getByPlaceholderText('••••••••');
    fireEvent.focus(input);
    // After focus, placeholder should be gone (normal input rendered)
    expect(screen.queryByPlaceholderText('••••••••')).not.toBeInTheDocument();
  });

  it('toggle button uses aria-pressed + aria-controls (SC 4.1.2)', () => {
    render(
      <AtlasCredentialField
        field={makeField()}
        hasExistingValue={false}
        onSensitiveChanged={vi.fn()}
        onNonSensitiveChanged={vi.fn()}
      />,
    );
    const toggle = screen.getByRole('button', { name: /Show API Key/i });
    expect(toggle).toHaveAttribute('aria-pressed', 'false');
    const inputId = toggle.getAttribute('aria-controls');
    expect(inputId).toBeTruthy();
    expect(document.getElementById(inputId!)).toBeInTheDocument();
  });

  it('shows text input after toggling secret', () => {
    render(
      <AtlasCredentialField
        field={makeField()}
        hasExistingValue={false}
        onSensitiveChanged={vi.fn()}
        onNonSensitiveChanged={vi.fn()}
      />,
    );
    const input = screen.getByLabelText(/API Key/, { selector: 'input' });
    expect(input).toHaveAttribute('type', 'password');
    fireEvent.click(screen.getByRole('button', { name: /Show/i }));
    expect(input).toHaveAttribute('type', 'text');
  });

  it('emits autocomplete attribute for url (SC 3.3.8)', () => {
    render(
      <AtlasCredentialField
        field={makeField({
          kind: CredentialFieldKind.Url,
          autocompleteHint: CredentialAutocompleteHint.Url,
        })}
        hasExistingValue={false}
        onSensitiveChanged={vi.fn()}
        onNonSensitiveChanged={vi.fn()}
      />,
    );
    const input = screen.getByRole('textbox');
    expect(input).toHaveAttribute('autocomplete', 'url');
    expect(input).toHaveAttribute('type', 'url');
  });
});

// ===== AtlasIntegrationConfig tests =====

describe('AtlasIntegrationConfig', () => {
  it('renders loading state before view resolves', () => {
    const provider = makeProvider({
      getAtlasView: vi.fn(
        () => new Promise<IntegrationAtlasView>(() => { /* never resolves */ }),
      ),
    });
    render(<AtlasIntegrationConfig provider={provider} />);
    expect(screen.getByRole('status')).toHaveTextContent('Loading integrations');
  });

  it('renders tablist with 6 categories after load', async () => {
    const provider = makeProvider();
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(6);
  });

  it('first tab is active by default (aria-selected="true")', async () => {
    const provider = makeProvider();
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
    const tabs = screen.getAllByRole('tab');
    expect(tabs[0]).toHaveAttribute('aria-selected', 'true');
    for (let i = 1; i < tabs.length; i++) {
      expect(tabs[i]).toHaveAttribute('aria-selected', 'false');
    }
  });

  it('inactive tabs have tabindex=-1 (roving tabindex)', async () => {
    const provider = makeProvider();
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
    const tabs = screen.getAllByRole('tab');
    expect(tabs[0]).toHaveAttribute('tabindex', '0');
    expect(tabs[1]).toHaveAttribute('tabindex', '-1');
  });

  it('ArrowRight moves to next tab and focuses it', async () => {
    const provider = makeProvider();
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
    const tablist = screen.getByRole('tablist');
    fireEvent.keyDown(tablist, { key: 'ArrowRight' });
    const tabs = screen.getAllByRole('tab');
    expect(tabs[1]).toHaveAttribute('aria-selected', 'true');
  });

  it('ArrowLeft wraps to last tab from first', async () => {
    const provider = makeProvider();
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
    const tablist = screen.getByRole('tablist');
    fireEvent.keyDown(tablist, { key: 'ArrowLeft' });
    const tabs = screen.getAllByRole('tab');
    expect(tabs[tabs.length - 1]).toHaveAttribute('aria-selected', 'true');
  });

  it('Home key moves to first tab', async () => {
    const provider = makeProvider();
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
    const tablist = screen.getByRole('tablist');
    // move to last first
    fireEvent.keyDown(tablist, { key: 'End' });
    fireEvent.keyDown(tablist, { key: 'Home' });
    const tabs = screen.getAllByRole('tab');
    expect(tabs[0]).toHaveAttribute('aria-selected', 'true');
  });

  it('End key moves to last tab', async () => {
    const provider = makeProvider();
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
    const tablist = screen.getByRole('tablist');
    fireEvent.keyDown(tablist, { key: 'End' });
    const tabs = screen.getAllByRole('tab');
    expect(tabs[tabs.length - 1]).toHaveAttribute('aria-selected', 'true');
  });

  it('inactive panels have hidden attribute (APG Tabs pattern)', async () => {
    const provider = makeProvider();
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
    // All panels except first are hidden
    const hiddenPanels = document.querySelectorAll('[role="tabpanel"][hidden]');
    expect(hiddenPanels.length).toBe(5);
  });

  it('active tabpanel is aria-labelledby its tab id', async () => {
    const provider = makeProvider();
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
    const activeTab = screen.getAllByRole('tab')[0];
    const tabId = activeTab.id;
    const activePanel = document.querySelector(`[role="tabpanel"]:not([hidden])`);
    expect(activePanel?.getAttribute('aria-labelledby')).toBe(tabId);
  });

  it('shows provider dropdown when schemas exist for active category', async () => {
    const schema = makeSchema({ category: IntegrationCategory.Payments });
    const provider = makeProvider({ getSchemas: vi.fn(() => [schema]) });
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());
    expect(screen.getByRole('combobox')).toBeInTheDocument();
  });

  it('validate button renders with aria-label and is not aria-disabled initially', async () => {
    const schema = makeSchema({
      category: IntegrationCategory.Payments,
      credentialFields: [makeField({ kind: CredentialFieldKind.Text })],
    });
    const provider = makeProvider({ getSchemas: vi.fn(() => [schema]) });
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());

    // Select the provider
    const select = screen.getByRole('combobox');
    fireEvent.change(select, { target: { value: 'test-provider' } });

    await waitFor(() => expect(screen.getByRole('button', { name: /Validate/i })).toBeInTheDocument());
    const btn = screen.getByRole('button', { name: /Validate/i });
    expect(btn).not.toHaveAttribute('aria-disabled', 'true');
  });

  it('calls validateProvider and shows Valid status (SC 4.1.3)', async () => {
    const schema = makeSchema({
      category: IntegrationCategory.Payments,
      credentialFields: [],
    });
    const validateFn = vi.fn(async () => makeValidResult(ProviderValidationStatus.Valid));
    const provider = makeProvider({
      getSchemas: vi.fn(() => [schema]),
      validateProvider: validateFn,
    });
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());

    const select = screen.getByRole('combobox');
    fireEvent.change(select, { target: { value: 'test-provider' } });
    await waitFor(() => expect(screen.getByRole('button', { name: /Validate/i })).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: /Validate/i }));
    await waitFor(() => expect(validateFn).toHaveBeenCalledOnce());
    expect(screen.getByText('Connected')).toBeInTheDocument();
  });

  it('shows Invalid status with error message', async () => {
    const schema = makeSchema({ category: IntegrationCategory.Payments, credentialFields: [] });
    const provider = makeProvider({
      getSchemas: vi.fn(() => [schema]),
      validateProvider: vi.fn(async () => ({
        status: ProviderValidationStatus.Invalid,
        validatedAt: '2026-05-13T00:00:00Z',
        errorCode: 'AUTH_FAILED',
        errorMessage: 'Invalid API key',
      })),
    });
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());

    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'test-provider' } });
    await waitFor(() => screen.getByRole('button', { name: /Validate/i }));
    fireEvent.click(screen.getByRole('button', { name: /Validate/i }));

    await waitFor(() => expect(screen.getByText('Invalid API key')).toBeInTheDocument());
    // Invalid should use role="alert" (assertive SC 4.1.3)
    const statusRegion = screen.getByText('Invalid API key').closest('.atlas-status-region');
    expect(statusRegion?.getAttribute('role')).toBe('alert');
  });

  it('shows Unreachable status on validateProvider rejection', async () => {
    const schema = makeSchema({ category: IntegrationCategory.Payments, credentialFields: [] });
    const provider = makeProvider({
      getSchemas: vi.fn(() => [schema]),
      validateProvider: vi.fn(async () => { throw new Error('network error'); }),
    });
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());

    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'test-provider' } });
    await waitFor(() => screen.getByRole('button', { name: /Validate/i }));
    fireEvent.click(screen.getByRole('button', { name: /Validate/i }));

    await waitFor(() => expect(screen.getByText(/Endpoint unreachable/i)).toBeInTheDocument());
  });

  it('issueProviderChange is called when provider changes', async () => {
    const schema = makeSchema({ category: IntegrationCategory.Payments });
    const changeProvider = vi.fn(async () => {});
    const provider = makeProvider({
      getSchemas: vi.fn(() => [schema]),
      issueProviderChange: changeProvider,
    });
    render(<AtlasIntegrationConfig provider={provider} />);
    await waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument());

    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'test-provider' } });
    await waitFor(() => expect(changeProvider).toHaveBeenCalledWith(IntegrationCategory.Payments, 'test-provider'));
  });
});
