/**
 * TypeScript projection of the `SystemRequirementsResult` wire format produced
 * by `Sunfish.Foundation.MissionSpace.IMinimumSpecResolver.EvaluateAsync`.
 *
 * Vendored copy of `packages/ui-adapters-react/src/contracts/SystemRequirements.ts`.
 * Source of truth is ADR 0063-A1.1. Keep in sync with ui-adapters-react surface.
 *
 * Mirror of `packages/foundation-mission-space/Models/Requirements.cs` +
 * `RequirementsEnums.cs`. Field names match the `[JsonPropertyName]` attributes
 * verbatim. Enum string literals match `JsonStringEnumConverter` output
 * (PascalCase — NOT camelCase).
 */

// ===== Enum string-literal unions =====

export const SpecPolicy = {
  Required: 'Required',
  Recommended: 'Recommended',
  Informational: 'Informational',
} as const;

export type SpecPolicy = (typeof SpecPolicy)[keyof typeof SpecPolicy];

export const OverallVerdict = {
  Pass: 'Pass',
  WarnOnly: 'WarnOnly',
  Block: 'Block',
} as const;

export type OverallVerdict = (typeof OverallVerdict)[keyof typeof OverallVerdict];

export const DimensionPolicyKind = {
  Required: 'Required',
  Recommended: 'Recommended',
  Informational: 'Informational',
  Unevaluated: 'Unevaluated',
} as const;

export type DimensionPolicyKind = (typeof DimensionPolicyKind)[keyof typeof DimensionPolicyKind];

export const DimensionPassFail = {
  Pass: 'Pass',
  Fail: 'Fail',
  Unevaluated: 'Unevaluated',
} as const;

export type DimensionPassFail = (typeof DimensionPassFail)[keyof typeof DimensionPassFail];

export const SystemRequirementsRenderMode = {
  PreInstallFullPage: 'PreInstallFullPage',
  PostInstallInlineExplanation: 'PostInstallInlineExplanation',
  PostInstallRegressionBanner: 'PostInstallRegressionBanner',
} as const;

export type SystemRequirementsRenderMode =
  (typeof SystemRequirementsRenderMode)[keyof typeof SystemRequirementsRenderMode];

/**
 * The 10 mission-envelope dimensions per ADR 0062-A1.2.
 * Verified against `packages/foundation-mission-space/Models/Enums.cs`
 * `DimensionChangeKind` enum on origin/main 2026-05-12.
 */
export const DimensionChangeKind = {
  Hardware: 'Hardware',
  User: 'User',
  Regulatory: 'Regulatory',
  Runtime: 'Runtime',
  FormFactor: 'FormFactor',
  Edition: 'Edition',
  Network: 'Network',
  TrustAnchor: 'TrustAnchor',
  SyncState: 'SyncState',
  VersionVector: 'VersionVector',
} as const;

export type DimensionChangeKind = (typeof DimensionChangeKind)[keyof typeof DimensionChangeKind];

// ===== Interfaces =====

/**
 * Per ADR 0063-A1.4 — operator recovery action surfaced when an evaluation fails.
 * Mirror of `packages/foundation-mission-space/Models/Requirements.cs:OperatorRecoveryAction`.
 */
export interface OperatorRecoveryAction {
  actionKey: string;
  argumentMap?: Record<string, string>;
}

/**
 * Per ADR 0063-A1.4 — per-dimension evaluation outcome.
 * Mirror of `packages/foundation-mission-space/Models/Requirements.cs:DimensionEvaluation`.
 */
export interface DimensionEvaluation {
  dimension: DimensionChangeKind;
  policy: DimensionPolicyKind;
  outcome: DimensionPassFail;
  operatorRecoveryAction?: OperatorRecoveryAction;
  detail?: string;
}

/**
 * Per ADR 0063-A1.1 — overall result from `IMinimumSpecResolver.EvaluateAsync`.
 * Mirror of `packages/foundation-mission-space/Models/Requirements.cs:SystemRequirementsResult`.
 *
 * `evaluatedAt` is an ISO-8601 string from `DateTimeOffset` (C# serializes as
 * `"2026-05-12T00:00:00+00:00"`).
 */
export interface SystemRequirementsResult {
  overall: OverallVerdict;
  dimensions: DimensionEvaluation[];
  operatorRecoveryAction?: OperatorRecoveryAction;
  evaluatedAt: string;
}

/**
 * Per ADR 0063-A1.11 — operator install force-enable request body for
 * `POST /api/system-requirements/{bundleId}/force-install`.
 * Mirror of `packages/foundation-mission-space/Services/IInstallForceEnableSurface.cs:InstallForceRequest`.
 */
export interface InstallForceRequest {
  operatorPrincipalId: string;
  /** Justification text — MUST NOT be empty (per A1.11). */
  reason: string;
  overrideTargets: DimensionChangeKind[];
  envelopeHash: string;
  platform?: string;
}

/**
 * Per ADR 0063-A1.11 — recorded force-enable returned from the force-install endpoint.
 * Mirror of `packages/foundation-mission-space/Services/IInstallForceEnableSurface.cs:InstallForceRecord`.
 */
export interface InstallForceRecord {
  operatorPrincipalId: string;
  reason: string;
  overrideTargets: DimensionChangeKind[];
  envelopeHash: string;
  platform?: string;
  recordedAt: string;
}

// ===== Hand-rolled parse validator =====

/**
 * Validates `json` has the required shape of `SystemRequirementsResult`.
 * Zero-dep runtime check (no Zod) — types are erased at runtime.
 * Throws `TypeError` if required fields are absent.
 */
export function parseSystemRequirementsResult(json: unknown): SystemRequirementsResult {
  if (typeof json !== 'object' || json === null) {
    throw new TypeError('SystemRequirementsResult: expected object');
  }
  const obj = json as Record<string, unknown>;
  if (typeof obj['overall'] !== 'string') {
    throw new TypeError('SystemRequirementsResult: missing required field "overall"');
  }
  if (!Array.isArray(obj['dimensions'])) {
    throw new TypeError('SystemRequirementsResult: missing required field "dimensions"');
  }
  if (typeof obj['evaluatedAt'] !== 'string') {
    throw new TypeError('SystemRequirementsResult: missing required field "evaluatedAt"');
  }
  return json as SystemRequirementsResult;
}
