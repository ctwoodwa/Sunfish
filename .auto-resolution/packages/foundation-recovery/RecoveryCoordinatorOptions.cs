namespace Sunfish.Foundation.Recovery;

/// <summary>
/// Configuration for <see cref="IRecoveryCoordinator"/> per ADR 0046.
/// Phase 1 defaults match the ADR's selected sub-patterns:
/// <list type="bullet">
///   <item><description><b>#48a</b> 3-of-5 multi-sig social recovery (<see cref="QuorumThreshold"/> = 3, <see cref="MaxTrustees"/> = 5)</description></item>
///   <item><description><b>#48e</b> 7-day grace window (<see cref="GracePeriod"/> = 7 days, range 7–30 per ADR)</description></item>
/// </list>
/// </summary>
public sealed record RecoveryCoordinatorOptions
{
    /// <summary>
    /// Number of trustee attestations required to start the grace period.
    /// ADR 0046 selects 3-of-5; the field is configurable so post-MVP can
    /// tune for higher-assurance tenants without code change.
    /// </summary>
    public int QuorumThreshold { get; init; } = 3;

    /// <summary>
    /// Maximum number of trustees the owner may designate. Beyond this,
    /// <see cref="IRecoveryCoordinator.DesignateTrusteeAsync"/> rejects
    /// the request. ADR 0046 selects 5.
    /// </summary>
    public int MaxTrustees { get; init; } = 5;

    /// <summary>
    /// Wall-clock duration that must elapse after quorum before the
    /// coordinator emits <see cref="RecoveryEventType.RecoveryCompleted"/>.
    /// During this window the original device may dispute.
    /// ADR 0046 selects 7 days; permitted range is 7–30.
    /// </summary>
    public TimeSpan GracePeriod { get; init; } = TimeSpan.FromDays(7);
}
