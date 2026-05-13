using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.IdentityAtlas;

/// <summary>
/// An enrolled recovery trustee per ADR 0066 §Phase 3.
/// </summary>
/// <param name="TrusteeActorId">Stable actor identifier of the trustee.</param>
/// <param name="DisplayName">Trustee display name at enrollment time.</param>
/// <param name="VerificationState">Current verification state of the trustee relationship.</param>
/// <param name="EnrolledAt">Wall-clock time the trustee was enrolled.</param>
public sealed record Trustee(
    ActorId TrusteeActorId,
    string DisplayName,
    TrusteeVerificationState VerificationState,
    DateTimeOffset EnrolledAt);

/// <summary>Lifecycle state of a trustee relationship.</summary>
public enum TrusteeVerificationState
{
    /// <summary>Enrollment Standing Order issued; awaiting trustee acceptance.</summary>
    Pending,
    /// <summary>Trustee has accepted and verified the relationship.</summary>
    Verified,
    /// <summary>Trustee has been revoked or has declined.</summary>
    Revoked,
}
