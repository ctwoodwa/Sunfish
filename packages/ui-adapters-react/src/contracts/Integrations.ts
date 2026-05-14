/**
 * TypeScript projection of the Integration Atlas contract types
 * defined in `packages/ui-core/Wayfinder/Integrations/`.
 *
 * Enum string literals match `JsonStringEnumConverter` output (PascalCase).
 * See ADR 0067 for the full substrate spec.
 */

// ===== Enum string-literal unions =====

export const IntegrationCategory = {
  Payments: 'Payments',
  TransactionalEmail: 'TransactionalEmail',
  MarketingEmail: 'MarketingEmail',
  Messaging: 'Messaging',
  MeshVpn: 'MeshVpn',
  Captcha: 'Captcha',
} as const;
export type IntegrationCategory = (typeof IntegrationCategory)[keyof typeof IntegrationCategory];

export const CredentialFieldKind = {
  Text: 'Text',
  Secret: 'Secret',
  Url: 'Url',
  ReadOnlyOutput: 'ReadOnlyOutput',
} as const;
export type CredentialFieldKind = (typeof CredentialFieldKind)[keyof typeof CredentialFieldKind];

export const CredentialAutocompleteHint = {
  None: 'None',
  CurrentPassword: 'CurrentPassword',
  NewPassword: 'NewPassword',
  OneTimeCode: 'OneTimeCode',
  Username: 'Username',
  Email: 'Email',
  Url: 'Url',
} as const;
export type CredentialAutocompleteHint =
  (typeof CredentialAutocompleteHint)[keyof typeof CredentialAutocompleteHint];

export const ProviderValidationStatus = {
  Unknown: 'Unknown',
  Valid: 'Valid',
  Invalid: 'Invalid',
  Unreachable: 'Unreachable',
} as const;
export type ProviderValidationStatus =
  (typeof ProviderValidationStatus)[keyof typeof ProviderValidationStatus];

// ===== Data types =====

export interface CredentialFieldSpec {
  key: string;
  displayLabel: string;
  kind: CredentialFieldKind;
  autocompleteHint: CredentialAutocompleteHint;
  isRequired: boolean;
  helpText?: string | null;
  placeholder?: string | null;
}

export interface IntegrationProviderSchema {
  providerId: string;
  displayName: string;
  category: IntegrationCategory;
  credentialFields: CredentialFieldSpec[];
  helpText?: string | null;
  documentationUrl?: string | null;
}

export interface ActiveProviderSnapshot {
  providerId: string;
  activatedAt: string;
}

export interface IntegrationValidationResult {
  status: ProviderValidationStatus;
  validatedAt: string;
  errorCode?: string | null;
  errorMessage?: string | null;
}

export interface IntegrationEmailRouting {
  transactionalProvider?: string | null;
  marketingProvider?: string | null;
}

export interface IntegrationAtlasView {
  activeByCategory: Partial<Record<IntegrationCategory, ActiveProviderSnapshot>>;
  statusByCategory: Partial<Record<IntegrationCategory, ProviderValidationStatus>>;
  emailRouting?: IntegrationEmailRouting | null;
}

// ===== Provider interface =====

/**
 * React-side provider interface mirroring `IIntegrationAtlasProvider`.
 * The host application wires up an implementation backed by REST API calls
 * or an in-memory stub for testing.
 */
export interface ReactIntegrationAtlasProvider {
  getSchemas(): IntegrationProviderSchema[];
  getAtlasView(): Promise<IntegrationAtlasView>;
  issueProviderChange(category: IntegrationCategory, providerId: string): Promise<void>;
  issueSensitiveCredential(
    category: IntegrationCategory,
    providerId: string,
    key: string,
    value: string,
  ): Promise<void>;
  issueNonSensitiveCredential(
    category: IntegrationCategory,
    providerId: string,
    key: string,
    value: unknown,
  ): Promise<void>;
  validateProvider(category: IntegrationCategory): Promise<IntegrationValidationResult>;
}
