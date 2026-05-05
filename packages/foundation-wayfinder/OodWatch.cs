using System;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// One OOD watch — an authority assignment for a single
/// <see cref="OodRole"/> within a tenant, with a TTL and a relieve / expire
/// terminal state. Per ADR 0078 §1.
/// </summary>
/// <remarks>
/// Wall-clock fields use <see cref="DateTimeOffset"/> to align with the
/// kernel-audit <c>OccurredAt</c> cohort precedent (W#34 / W#35 / W#40 /
/// W#41 followed the same choice — see StandingOrder.IssuedAt docstring).
/// ADR 0078's spec used <c>NodaTime.Instant</c>; the cohort has not adopted
/// NodaTime in <c>Directory.Packages.props</c>, so this implementation
/// deviates intentionally to preserve binary compatibility with the rest
/// of the foundation-wayfinder substrate.
/// </remarks>
/// <param name="Id">Server-generated stable identifier; callers MUST NOT supply.</param>
/// <param name="TenantId">Tenant scope. Single Active watch per (TenantId, Role) pair.</param>
/// <param name="OnWatchActor">Actor currently holding the watch.</param>
/// <param name="Role">Which OOD role this watch covers.</param>
/// <param name="StartedAt">Wall-clock instant the watch entered <see cref="OodWatchState.Active"/>.</param>
/// <param name="RelievedAt">Wall-clock instant the watch transitioned out of Active; null while Active.</param>
/// <param name="StartedBy">Actor who initiated the watch via <c>StartWatchAsync</c>.</param>
/// <param name="RelievedBy">Actor who relieved or expired the watch; null while Active.</param>
/// <param name="MaxWatchDuration">TTL bound — the expiry sweep transitions to <see cref="OodWatchState.Expired"/> at <c>StartedAt + MaxWatchDuration</c>.</param>
/// <param name="State">Lifecycle state per <see cref="OodWatchState"/>.</param>
public sealed record OodWatch(
    OodWatchId Id,
    TenantId TenantId,
    ActorId OnWatchActor,
    OodRole Role,
    DateTimeOffset StartedAt,
    DateTimeOffset? RelievedAt,
    ActorId StartedBy,
    ActorId? RelievedBy,
    TimeSpan MaxWatchDuration,
    OodWatchState State) : IMustHaveTenant;
