/**
 * TypeScript projection of the Identity Atlas JSON wire format produced by
 * the Bridge `/api/v1/identity/*` route family (ADR 0066 §Phase 3, W#58 Phase 3).
 *
 * Mirrors `packages/ui-core/Wayfinder/Identity/ViewModels.cs` response DTOs
 * from `accelerators/bridge/Sunfish.Bridge/Features/Identity/IdentityEndpoints.cs`.
 *
 * - `ActorId` and `KeyFingerprint` serialize as opaque strings.
 * - `SyncState` serializes as lowercase string (canonical form per ADR 0036 A1.2).
 * - `DateTimeOffset` serializes as ISO-8601 (`"O"` format — e.g. `"2026-05-13T00:00:00+00:00"`).
 * - `Guid` (TeamId, ActiveTeamId) serializes as lowercase hyphenated string.
 * - Property names are camelCase (ASP.NET Core `JsonSerializerOptions.Web` default).
 */

// ===== Enum string-literal unions =====

/** Per ADR 0036 A1.2 — canonical lowercase wire identifiers for SyncState. */
export type SyncState = 'healthy' | 'stale' | 'offline' | 'conflict' | 'quarantine';

// ===== Interfaces =====

/** GET /api/v1/identity/profile — mirrors IdentityProfileEditViewModel */
export interface IdentityProfileResponse {
  actor: string;
  displayName: string;
  contactEmail: string;
  phoneNumber: string | null;
}

/** GET /api/v1/identity/keys — mirrors KeyRotationViewModel */
export interface KeyRotationResponse {
  actor: string;
  /** Canonical 95-char hex-with-colons fingerprint; null when no key is registered. */
  currentFingerprint: string | null;
  historicalKeyCount: number;
  rotationInProgress: boolean;
  /** ISO-8601 string; null when no rotation is in progress. */
  rotationWindowExpiry: string | null;
}

/** One enrolled recovery contact — mirrors RecoveryContact */
export interface RecoveryContactResponse {
  contactActorId: string;
  displayName: string;
  verificationStatus: SyncState;
  /** ISO-8601 string. */
  enrolledAt: string;
}

/** GET /api/v1/identity/recovery — mirrors RecoveryContactsViewModel */
export interface RecoveryContactsResponse {
  actor: string;
  contacts: RecoveryContactResponse[];
  maxContacts: number;
}

/** One retired-key entry — mirrors HistoricalKeyEntry */
export interface HistoricalKeyEntryResponse {
  /** Canonical 95-char hex-with-colons fingerprint. */
  fingerprint: string;
  /** ISO-8601 string. */
  activatedAt: string;
  /** ISO-8601 string; null for the active key. */
  retiredAt: string | null;
  rotationReason: string;
  signatureSurvivalCount: number;
}

/** GET /api/v1/identity/keys/history — mirrors HistoricalKeysBrowseViewModel */
export interface HistoricalKeysResponse {
  actor: string;
  /** Retired keys in reverse-chronological order (newest first). */
  keys: HistoricalKeyEntryResponse[];
}

/** One team-membership entry — mirrors TeamMembershipEntry */
export interface TeamMembershipResponse {
  /** Lowercase hyphenated UUID string. */
  teamId: string;
  displayName: string;
  roleDisplayName: string;
  /** Canonical 95-char hex-with-colons fingerprint of the actor's per-team subkey. */
  subkeyFingerprint: string;
}

/** GET /api/v1/identity/teams — mirrors ActiveTeamOverviewViewModel */
export interface ActiveTeamOverviewResponse {
  actor: string;
  teams: TeamMembershipResponse[];
  /** Lowercase hyphenated UUID string; null when no team is active (Bridge tenant case). */
  activeTeamId: string | null;
}

// ===== Diff-preview types (ADR 0077 §4, IDiffPreview projection — W#58 Phase 4) =====

/**
 * Single field-level change entry — TypeScript projection of
 * `Sunfish.UICore.Primitives.DiffEntry`.
 * OldValue / NewValue are nullable strings (values are pre-formatted server-side
 * or rendered as-is on the client).
 */
export interface DiffEntry {
  field: string;
  oldValue: string | null;
  newValue: string | null;
}

/**
 * Pending Standing Order diff payload — TypeScript projection of IDiffPreview.
 * Cascaded from the Helm widget to identity pages when a mutation is pending confirmation.
 */
export interface PendingDiffPreview {
  summary: string;
  entries: DiffEntry[];
}
