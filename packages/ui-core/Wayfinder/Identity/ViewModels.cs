using System;
using System.Collections.Generic;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.UI;

namespace Sunfish.UICore.Wayfinder;

/// <summary>
/// View-model for the identity-profile edit pane per ADR 0066 §2.1.
/// </summary>
/// <param name="Actor">The actor whose profile is being edited.</param>
/// <param name="DisplayName">Localized display name.</param>
/// <param name="ContactEmail">Contact email; rendered in the profile pane.</param>
/// <param name="PhoneNumber">Optional contact phone number.</param>
public sealed record IdentityProfileEditViewModel(
    ActorId Actor,
    string DisplayName,
    string ContactEmail,
    string? PhoneNumber);

/// <summary>
/// View-model for the key-rotation pane per ADR 0066 §2.2.
/// </summary>
/// <remarks>
/// <see cref="DateTimeOffset"/> stands in for the hand-off's
/// <c>NodaTime.Instant</c> per W#46 / W#49 / W#50 / W#54 / W#55 / W#53 P1a
/// cohort precedent — NodaTime is not on
/// <c>Directory.Packages.props</c>; future ADR amendment will migrate
/// every cohort time-bearing record at once.
/// </remarks>
/// <param name="Actor">The actor whose key is being rotated.</param>
/// <param name="CurrentFingerprint">Canonical fingerprint of the active key.</param>
/// <param name="HistoricalKeyCount">Count of retired keys for the actor.</param>
/// <param name="RotationInProgress">True when a rotation Standing Order is mid-flight.</param>
/// <param name="RotationWindowExpiry">When the active rotation window expires; null when no rotation is in progress.</param>
public sealed record KeyRotationViewModel(
    ActorId Actor,
    KeyFingerprint CurrentFingerprint,
    int HistoricalKeyCount,
    bool RotationInProgress,
    DateTimeOffset? RotationWindowExpiry);

/// <summary>
/// View-model for the recovery-contacts pane per ADR 0066 §2.3.
/// </summary>
/// <param name="Actor">The actor whose recovery contacts are being managed.</param>
/// <param name="Contacts">Currently-enrolled recovery contacts.</param>
/// <param name="MaxContacts">Maximum contacts allowed by the tenant policy.</param>
public sealed record RecoveryContactsViewModel(
    ActorId Actor,
    IReadOnlyList<RecoveryContact> Contacts,
    int MaxContacts);

/// <summary>
/// One enrolled recovery contact per ADR 0066 §2.3 + OQ-1 council
/// decision (NM-1 disposition: user-facing UX vocabulary uses
/// "Recovery Contact"; audit / persistence vocabulary uses "Trustee"
/// per <c>AuditEventType.TrusteeSetChanged</c> in ADR 0046).
/// </summary>
/// <param name="ContactActorId">Actor identifier of the contact.</param>
/// <param name="DisplayName">Localized display name for the contact.</param>
/// <param name="VerificationStatus">Sync-state discriminator surfacing the verification health of the contact's enrollment.</param>
/// <param name="EnrolledAt">Wall-clock time the contact was enrolled.</param>
public sealed record RecoveryContact(
    ActorId ContactActorId,
    string DisplayName,
    SyncState VerificationStatus,
    DateTimeOffset EnrolledAt);

/// <summary>
/// View-model for the historical-keys browse pane per ADR 0066 §2.4.
/// </summary>
/// <param name="Actor">The actor whose retired keys are being browsed.</param>
/// <param name="Keys">Retired keys in reverse-chronological order (newest first).</param>
public sealed record HistoricalKeysBrowseViewModel(
    ActorId Actor,
    IReadOnlyList<HistoricalKeyEntry> Keys);

/// <summary>
/// One retired-key entry per ADR 0066 §2.4. Phase 1b types
/// <see cref="RotationReason"/> as <see cref="string"/>; ADR 0046-a1
/// (not yet on origin/main) will introduce a typed
/// <c>KeyRotationReason</c> enum that replaces this string in a Phase 2
/// follow-up amendment.
/// </summary>
/// <param name="Fingerprint">Canonical fingerprint of the retired key.</param>
/// <param name="ActivatedAt">Wall-clock time the key was activated.</param>
/// <param name="RetiredAt">Wall-clock time the key was retired; null for the active key.</param>
/// <param name="RotationReason">Free-form reason for the rotation (Phase 1b string; Phase 2 typed enum).</param>
/// <param name="SignatureSurvivalCount">Number of audit-record signatures still verifiable under this retired key.</param>
public sealed record HistoricalKeyEntry(
    KeyFingerprint Fingerprint,
    DateTimeOffset ActivatedAt,
    DateTimeOffset? RetiredAt,
    string RotationReason,
    int SignatureSurvivalCount);

/// <summary>
/// View-model for the active-team overview pane per ADR 0066 §2.5 +
/// §2.6.
/// </summary>
/// <remarks>
/// <see cref="ActiveTeamId"/> uses <see cref="Guid"/> rather than
/// <c>Sunfish.Kernel.Runtime.Teams.TeamId</c> per the W#53 P1a
/// cycle-break decision — <c>kernel-runtime</c> already references
/// <c>ui-core</c>, so a ui-core → kernel-runtime ProjectReference would
/// form a cycle. Consumers in kernel-runtime / accelerators wrap the
/// Guid back into <c>TeamId</c> at the boundary.
/// </remarks>
/// <param name="Actor">The actor whose team memberships are being viewed.</param>
/// <param name="Teams">Per-team membership entries.</param>
/// <param name="ActiveTeamId">Currently-active team identifier; null when no team is selected (Bridge tenant case).</param>
public sealed record ActiveTeamOverviewViewModel(
    ActorId Actor,
    IReadOnlyList<TeamMembershipEntry> Teams,
    Guid? ActiveTeamId);

/// <summary>
/// One team-membership entry per ADR 0066 §2.6.
/// </summary>
/// <param name="TeamId">Team identifier; <see cref="Guid"/> per the cycle-break decision (see <see cref="ActiveTeamOverviewViewModel"/>).</param>
/// <param name="DisplayName">Localized display name for the team.</param>
/// <param name="RoleDisplayName">Localized display name for the actor's role within the team.</param>
/// <param name="SubkeyFingerprint">Canonical fingerprint of the actor's per-team subkey.</param>
public sealed record TeamMembershipEntry(
    Guid TeamId,
    string DisplayName,
    string RoleDisplayName,
    KeyFingerprint SubkeyFingerprint);
