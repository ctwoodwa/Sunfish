// Contracts
export * from './contracts/ButtonVariant';
export * from './contracts/ButtonSize';
export * from './contracts/FillMode';
export * from './contracts/RoundedMode';
export type { ICssProvider } from './contracts/ICssProvider';
export type { IIconProvider } from './contracts/IIconProvider';
export {
  HelmSlot,
  HelmActionInvocationKind,
} from './contracts/HelmTypes';
export type {
  HelmWidgetMetadata,
  HelmWidgetAction,
  HelmWidgetViewState,
  HelmRenderContext,
  HelmWidget,
  HelmWidgetRegistry,
} from './contracts/HelmTypes';

// Provider context
export {
  CssProviderContext,
  CssProviderProvider,
  useCssProvider,
  type CssProviderProviderProps,
} from './CssProviderContext';

// Providers
export { BootstrapCssProvider } from './providers/BootstrapCssProvider';
export { FluentUICssProvider } from './providers/FluentUICssProvider';
export { MaterialCssProvider } from './providers/MaterialCssProvider';

// Components
export { SunfishButton, type SunfishButtonProps } from './components/SunfishButton';
export {
  SunfishDataGrid,
  type SunfishDataGridProps,
  type Column,
} from './components/SunfishDataGrid';
export { SunfishDialog, type SunfishDialogProps } from './components/SunfishDialog';
export { HelmRenderer, type HelmRendererProps } from './components/HelmRenderer';
export {
  SystemRequirements,
  type SystemRequirementsProps,
  SystemRequirementsDimensionRow,
  type SystemRequirementsDimensionRowProps,
  SystemRequirementsInlinePanel,
  type SystemRequirementsInlinePanelProps,
  SystemRequirementsRegressionBanner,
  type SystemRequirementsRegressionBannerProps,
} from './components/SystemRequirements';
export { useSystemRequirements } from './hooks/useSystemRequirements';
export type { UseSystemRequirementsResult, UseSystemRequirementsOptions } from './hooks/useSystemRequirements';

// Identity Atlas pages (ADR 0066 §Phase 3, W#58 Phase 3)
export {
  IdentityProfilePage,
  type IdentityProfilePageProps,
  KeyRotationPage,
  type KeyRotationPageProps,
  RecoveryContactsPage,
  type RecoveryContactsPageProps,
  HistoricalKeysPage,
  type HistoricalKeysPageProps,
  ActiveTeamOverviewPage,
  type ActiveTeamOverviewPageProps,
} from './components/Identity';

// Identity Atlas contract types (ADR 0066 §Phase 3 wire format + Phase 4 diff-preview)
export type {
  SyncState,
  IdentityProfileResponse,
  KeyRotationResponse,
  RecoveryContactResponse,
  RecoveryContactsResponse,
  HistoricalKeyEntryResponse,
  HistoricalKeysResponse,
  TeamMembershipResponse,
  ActiveTeamOverviewResponse,
  DiffEntry,
  PendingDiffPreview,
} from './contracts/IdentityTypes';

// Integration Atlas components + contracts (ADR 0067, W#48 Phase 4)
export {
  AtlasIntegrationConfig,
  type AtlasIntegrationConfigProps,
  AtlasIntegrationCategoryPanel,
  type AtlasIntegrationCategoryPanelProps,
  AtlasCredentialField,
  type AtlasCredentialFieldProps,
} from './components/Integrations';
export type {
  IntegrationProviderSchema,
  CredentialFieldSpec,
  IntegrationAtlasView,
  IntegrationValidationResult,
  ActiveProviderSnapshot,
  IntegrationEmailRouting,
  ReactIntegrationAtlasProvider,
} from './contracts/Integrations';
export {
  IntegrationCategory,
  CredentialFieldKind,
  CredentialAutocompleteHint,
  ProviderValidationStatus,
} from './contracts/Integrations';

// SystemRequirements contract types and values (Phase 1 serialization contract)
export type {
  SystemRequirementsResult,
  DimensionEvaluation,
  OperatorRecoveryAction,
  InstallForceRequest,
  InstallForceRecord,
} from './contracts/SystemRequirements';
export {
  OverallVerdict,
  DimensionChangeKind,
  DimensionPolicyKind,
  DimensionPassFail,
  SystemRequirementsRenderMode,
  SpecPolicy,
  parseSystemRequirementsResult,
} from './contracts/SystemRequirements';
