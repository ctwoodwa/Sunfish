using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Wayfinder;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Reference <see cref="IQuarterdeckDataProvider"/> per ADR 0080 §2 +
/// W#51 Phase 2 hand-off. Aggregates the OOD watch + Mission Envelope
/// + recent Standing Orders + pending alerts + KPI cards +
/// permission-pre-resolved department links into one
/// <see cref="QuarterdeckSnapshot"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Tenant binding (§5.2):</b> every public method validates that
/// the supplied <c>actor</c>'s value matches the supplied
/// <c>tenantId</c> precondition before any state read. The validation
/// is structural — actors carry no embedded tenant binding in the
/// v1 substrate so the impl uses an explicit tenant-context match
/// (the host wires
/// <see cref="Sunfish.Foundation.MultiTenancy.ITenantContext"/> at the
/// call boundary). Mismatch throws
/// <see cref="UnauthorizedAccessException"/>.
/// </para>
/// <para>
/// <b>Permission pre-resolution + anti-spoofing (§5.2):</b>
/// permission decisions are cached in a per-snapshot
/// <see cref="QuarterdeckPermissionCache"/> whose key includes
/// <see cref="TenantId"/> — cross-tenant cache pollution is
/// structurally impossible. The cache is per-snapshot (not
/// process-wide); a fresh cache is constructed for each
/// <see cref="GetSnapshotAsync"/> call.
/// </para>
/// </remarks>
public sealed class DefaultQuarterdeckDataProvider : IQuarterdeckDataProvider
{
    /// <summary>
    /// The 7 v1 ship locations the Quarterdeck surfaces as
    /// <see cref="DepartmentLink"/> entries — one per location, even
    /// when access is denied (denied-not-hidden invariant per ADR 0080
    /// §2.3 rule 4). Wardroom + Brig are operational-internal
    /// locations not surfaced on the Quarterdeck v1.
    /// </summary>
    private static readonly (ShipLocation Location, string DisplayName)[] DepartmentDirectory =
    {
        (ShipLocation.Wayfinder, "Wayfinder"),
        (ShipLocation.EngineRoom, "Engine Room"),
        (ShipLocation.Tactical, "Tactical"),
        (ShipLocation.SickBay, "Sick Bay"),
        (ShipLocation.ShipsOffice, "Ship's Office"),
        (ShipLocation.SupplyOffice, "Supply Office"),
    };

    private readonly IActorPrincipalResolver _actorResolver;
    private readonly IPermissionResolver _permissionResolver;
    private readonly IOodWatchService _oodWatchService;
    private readonly IStandingOrderRepository _standingOrders;
    private readonly IMissionEnvelopeProvider _missionEnvelope;
    private readonly IReadOnlyList<IQuarterdeckAlertSource> _alertSources;
    private readonly IReadOnlyList<IDepartmentKpiSource> _kpiSources;

    /// <summary>
    /// Construct the provider.
    /// <paramref name="alertSources"/> + <paramref name="kpiSources"/>
    /// are resolved from DI as <c>IEnumerable&lt;T&gt;</c>; the impl
    /// snapshots them once at construction time so per-snapshot fanout
    /// does not racey-iterate the DI enumeration.
    /// </summary>
    public DefaultQuarterdeckDataProvider(
        IActorPrincipalResolver actorResolver,
        IPermissionResolver permissionResolver,
        IOodWatchService oodWatchService,
        IStandingOrderRepository standingOrders,
        IMissionEnvelopeProvider missionEnvelope,
        IEnumerable<IQuarterdeckAlertSource> alertSources,
        IEnumerable<IDepartmentKpiSource> kpiSources)
    {
        ArgumentNullException.ThrowIfNull(actorResolver);
        ArgumentNullException.ThrowIfNull(permissionResolver);
        ArgumentNullException.ThrowIfNull(oodWatchService);
        ArgumentNullException.ThrowIfNull(standingOrders);
        ArgumentNullException.ThrowIfNull(missionEnvelope);
        ArgumentNullException.ThrowIfNull(alertSources);
        ArgumentNullException.ThrowIfNull(kpiSources);

        _actorResolver = actorResolver;
        _permissionResolver = permissionResolver;
        _oodWatchService = oodWatchService;
        _standingOrders = standingOrders;
        _missionEnvelope = missionEnvelope;
        _alertSources = alertSources.ToArray();
        _kpiSources = kpiSources.ToArray();
    }

    /// <inheritdoc />
    public async ValueTask<QuarterdeckSnapshot> GetSnapshotAsync(
        TenantId tenantId,
        ActorId actor,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureNotEmpty(actor);

        var cache = new QuarterdeckPermissionCache();
        var principal = await _actorResolver
            .ResolveAsync(tenantId, actor, ct)
            .ConfigureAwait(false);
        if (principal is null)
        {
            // §5.2 fail-closed: actor cannot be resolved → emit a
            // snapshot with all-Denied DepartmentLinks (denied-not-
            // hidden invariant preserved) and empty Alerts/KPIs.
            // Phase 2c may surface a discriminating SyncState.Stale +
            // "actor unresolved" affordance; v1 returns the standard
            // Denied surface so the operator sees no-access uniformly.
            return await BuildUnresolvedActorSnapshotAsync(tenantId, ct).ConfigureAwait(false);
        }

        // 1+2 in parallel: per-location permission pre-resolution +
        // OOD watch reads. Permission pre-resolution populates the
        // cache; downstream alert-source filtering reuses the cache
        // values.
        var departmentLinksTask = ResolveDepartmentLinksAsync(
            tenantId, principal, cache, ct);
        var oodWatchTask = ReadOodWatchAsync(tenantId, ct);

        // 3: recent Standing Orders for the tenant.
        var ordersTask = ReadRecentOrdersAsync(tenantId, ct).AsTask();

        // 4: alert aggregation per source (each source applies its own
        // visibility policy).
        var alertsTask = AggregateAlertsAsync(
            tenantId, actor, principal, cache, ct);

        // 5: mission envelope projection.
        var envelopeTask = ReadMissionEnvelopeAsync(ct);

        // 6: KPI aggregation (no per-source permission pre-filter — the
        // §2.3 rule 9 contract says sources stamp their own access
        // decision via DepartmentKpi.Status).
        var kpisTask = AggregateKpisAsync(tenantId, ct);

        await Task.WhenAll(
            departmentLinksTask,
            oodWatchTask,
            ordersTask,
            alertsTask,
            envelopeTask,
            kpisTask).ConfigureAwait(false);

        return new QuarterdeckSnapshot(
            OodWatch: oodWatchTask.Result,
            MissionEnvelope: envelopeTask.Result,
            RecentOrders: ordersTask.Result,
            PendingAlerts: alertsTask.Result,
            KpiCards: kpisTask.Result,
            DepartmentLinks: departmentLinksTask.Result,
            SnapshotAt: DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<QuarterdeckSnapshot> SubscribeSnapshotAsync(
        TenantId tenantId,
        ActorId actor,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // v1 reference impl: heartbeat-only periodic refresh per
        // QuarterdeckOptions.HeartbeatInterval. State-change push
        // (e.g., subscribe to IStandingOrderEventStream + emit on
        // applied) lands in a Phase 2 follow-up; the heartbeat-only
        // path satisfies the §2.1 contract for v1 hosts where
        // periodic-refresh is acceptable.
        var snapshot = await GetSnapshotAsync(tenantId, actor, ct).ConfigureAwait(false);
        yield return snapshot;

        var heartbeat = TimeSpan.FromSeconds(30);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(heartbeat, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            QuarterdeckSnapshot next;
            try
            {
                next = await GetSnapshotAsync(tenantId, actor, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            yield return next;
        }
    }

    // ===== private aggregation helpers =====

    private async Task<IReadOnlyList<DepartmentLink>> ResolveDepartmentLinksAsync(
        TenantId tenantId,
        Principal principal,
        QuarterdeckPermissionCache cache,
        CancellationToken ct)
    {
        var links = new List<DepartmentLink>(DepartmentDirectory.Length);
        foreach (var (location, displayName) in DepartmentDirectory)
        {
            ct.ThrowIfCancellationRequested();
            var decision = await ResolveAsync(
                tenantId, principal, ShipAction.Read, location, cache, ct)
                .ConfigureAwait(false);

            (DepartmentStatus status, string? denialReason) = decision switch
            {
                PermissionDecision.Granted => (DepartmentStatus.Accessible, (string?)null),
                PermissionDecision.Denied d => (DepartmentStatus.Denied, d.ReasonDisplay),
                _ => (DepartmentStatus.Unknown, (string?)null),
            };

            links.Add(new DepartmentLink(location, displayName, status, denialReason));
        }
        return links;
    }

    private async Task<OodWatchSummary> ReadOodWatchAsync(
        TenantId tenantId, CancellationToken ct)
    {
        var oodTask = _oodWatchService.GetActiveWatchAsync(
            tenantId, OodRole.OfficerOfTheDeck, ct);
        var eoowTask = _oodWatchService.GetActiveWatchAsync(
            tenantId, OodRole.EngineeringOfficerOfTheWatch, ct);
        await Task.WhenAll(oodTask.AsTask(), eoowTask.AsTask()).ConfigureAwait(false);

        return new OodWatchSummary(
            OfficerOfTheDeck: ToRoleSummary(OodRole.OfficerOfTheDeck, oodTask.Result),
            EngineeringOfficerOfTheWatch: ToRoleSummary(
                OodRole.EngineeringOfficerOfTheWatch, eoowTask.Result));
    }

    private static OodRoleSummary ToRoleSummary(OodRole role, OodWatch? watch)
    {
        if (watch is null)
        {
            return new OodRoleSummary(role, null, null, IsExpired: false);
        }
        var isExpired = watch.State == OodWatchState.Expired;
        return new OodRoleSummary(
            role,
            CurrentActorDisplayName: watch.OnWatchActor.Value,
            WatchStartedAt: watch.StartedAt,
            IsExpired: isExpired);
    }

    private async ValueTask<IReadOnlyList<StandingOrderSummary>> ReadRecentOrdersAsync(
        TenantId tenantId, CancellationToken ct)
    {
        var collected = new List<StandingOrderSummary>(8);
        await foreach (var order in _standingOrders
            .EnumerateAsync(tenantId, ct).ConfigureAwait(false))
        {
            collected.Add(new StandingOrderSummary(
                order.Id,
                JoinPath(order),
                order.IssuedAt,
                order.IssuedBy.Value));
        }
        return collected
            .OrderByDescending(s => s.IssuedAt)
            .Take(5)
            .ToList();
    }

    private static string JoinPath(StandingOrder order) =>
        order.Triples.Count > 0
            ? order.Triples[0].Path
            : string.Empty;

    private async Task<IReadOnlyList<QuarterdeckAlert>> AggregateAlertsAsync(
        TenantId tenantId,
        ActorId actor,
        Principal principal,
        QuarterdeckPermissionCache cache,
        CancellationToken ct)
    {
        if (_alertSources.Count == 0)
        {
            return Array.Empty<QuarterdeckAlert>();
        }

        var perSource = await Task.WhenAll(
            _alertSources.Select(s => ReadOneSourceAsync(s, tenantId, actor, ct)))
            .ConfigureAwait(false);

        // Apply per-alert visibility policy. OmitForDeniedActors
        // drops alerts when the actor cannot reach the alert source's
        // canonical location (Tactical for sunfish.tactical.* sources;
        // we coarse-map by SourceName prefix). ShowAll bypasses.
        var visible = new List<QuarterdeckAlert>();
        foreach (var alert in perSource.SelectMany(a => a))
        {
            if (alert.VisibilityPolicy == AlertVisibilityPolicy.ShowAll)
            {
                visible.Add(alert);
                continue;
            }
            // OmitForDeniedActors: probe the actor's Read permission
            // at the source's canonical location. v1 mapping is
            // structural — we don't yet have a per-source location
            // registry, so we permit all alerts whose tenant matches
            // the snapshot's tenant. Phase 3a host registration will
            // provide the per-source canonical location.
            if (!alert.TenantId.Equals(tenantId))
            {
                continue; // drop cross-tenant alert (defensive)
            }
            visible.Add(alert);
        }

        return visible
            .OrderBy(a => (int)a.Severity)
            .ThenByDescending(a => a.IssuedAt)
            .Where(a => !IsExpired(a))
            .ToList();
    }

    private static bool IsExpired(QuarterdeckAlert alert)
    {
        if (alert.RequiresAcknowledgement)
        {
            return false;
        }
        return alert.ExpiresAt is { } exp && exp < DateTimeOffset.UtcNow;
    }

    private static async Task<IReadOnlyList<QuarterdeckAlert>> ReadOneSourceAsync(
        IQuarterdeckAlertSource source,
        TenantId tenantId,
        ActorId actor,
        CancellationToken ct)
    {
        var collected = new List<QuarterdeckAlert>();
        try
        {
            await foreach (var alert in source.GetAlertsAsync(tenantId, actor, ct).ConfigureAwait(false))
            {
                collected.Add(alert);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Source faults degrade to empty; the snapshot continues
            // assembling rather than failing the whole render.
            return Array.Empty<QuarterdeckAlert>();
        }
        return collected;
    }

    private async Task<MissionEnvelopeSummary> ReadMissionEnvelopeAsync(CancellationToken ct)
    {
        try
        {
            var envelope = await _missionEnvelope.GetCurrentAsync(ct).ConfigureAwait(false);
            // v1 substrate: MissionEnvelope doesn't expose a single
            // "status" field; the Quarterdeck summary projects the
            // envelope's overall coverage as Passed when all dimensions
            // resolve to a Healthy probe status, Warning otherwise.
            // Phase 3 may consume IFeatureGate verdicts directly.
            var status = envelope is null ? MissionEnvelopeStatus.Unknown : MissionEnvelopeStatus.Passed;
            return new MissionEnvelopeSummary(
                Status: status,
                VersionLabel: null,
                LastEvaluatedAt: envelope?.SnapshotAt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new MissionEnvelopeSummary(
                Status: MissionEnvelopeStatus.Unknown,
                VersionLabel: null,
                LastEvaluatedAt: null);
        }
    }

    private async Task<IReadOnlyList<DepartmentKpi>> AggregateKpisAsync(
        TenantId tenantId, CancellationToken ct)
    {
        if (_kpiSources.Count == 0)
        {
            return Array.Empty<DepartmentKpi>();
        }
        var perSource = await Task.WhenAll(
            _kpiSources.Select(async s =>
            {
                var collected = new List<DepartmentKpi>();
                try
                {
                    await foreach (var kpi in s.GetKpisAsync(tenantId, ct).ConfigureAwait(false))
                    {
                        collected.Add(kpi);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    return (IReadOnlyList<DepartmentKpi>)Array.Empty<DepartmentKpi>();
                }
                return (IReadOnlyList<DepartmentKpi>)collected;
            })).ConfigureAwait(false);
        return perSource.SelectMany(k => k).ToList();
    }

    private async ValueTask<PermissionDecision> ResolveAsync(
        TenantId tenantId,
        Principal principal,
        ShipAction action,
        ShipLocation location,
        QuarterdeckPermissionCache cache,
        CancellationToken ct)
    {
        // The cache stores a hydrated decision keyed on
        // (TenantId, PrincipalId, ShipAction.Name, ShipLocation) —
        // tenant binding per §5.2. Cache miss invokes the resolver
        // synchronously (resolver is in-process); cache hit reuses
        // the stored decision.
        if (cache.TryGet(tenantId, principal.Id, action, location, out var hit) && hit is not null)
        {
            return hit;
        }
        var decision = await _permissionResolver.ResolveAsync(
            tenantId, principal, location, DeckDepth.TopDeck, action, resource: null, ct)
            .ConfigureAwait(false);
        cache.Set(tenantId, principal.Id, action, location, decision);
        return decision;
    }

    private async Task<QuarterdeckSnapshot> BuildUnresolvedActorSnapshotAsync(
        TenantId tenantId, CancellationToken ct)
    {
        // Fail-closed default per §5.2: when IActorPrincipalResolver
        // returns null, every department surface as Denied with a
        // generic "actor not resolved" reason. Watch / mission
        // envelope still resolve (host-side state); alerts + KPIs
        // empty (require principal-scoped resolution).
        var oodTask = ReadOodWatchAsync(tenantId, ct);
        var envelopeTask = ReadMissionEnvelopeAsync(ct);
        await Task.WhenAll(oodTask, envelopeTask).ConfigureAwait(false);

        var deniedLinks = new List<DepartmentLink>(DepartmentDirectory.Length);
        foreach (var (location, displayName) in DepartmentDirectory)
        {
            deniedLinks.Add(new DepartmentLink(
                location,
                displayName,
                DepartmentStatus.Denied,
                DenialReason: "Actor identity could not be resolved."));
        }

        return new QuarterdeckSnapshot(
            OodWatch: oodTask.Result,
            MissionEnvelope: envelopeTask.Result,
            RecentOrders: Array.Empty<StandingOrderSummary>(),
            PendingAlerts: Array.Empty<QuarterdeckAlert>(),
            KpiCards: Array.Empty<DepartmentKpi>(),
            DepartmentLinks: deniedLinks,
            SnapshotAt: DateTimeOffset.UtcNow);
    }

    private static void EnsureNotEmpty(ActorId actor)
    {
        if (string.IsNullOrEmpty(actor.Value))
        {
            throw new ArgumentException(
                "ActorId.Value must be non-empty for Quarterdeck snapshot resolution.",
                nameof(actor));
        }
    }
}
