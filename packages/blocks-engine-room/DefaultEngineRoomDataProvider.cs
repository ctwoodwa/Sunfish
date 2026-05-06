using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.EngineRoom;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.EngineRoom;

/// <summary>
/// Reference <see cref="IEngineRoomDataProvider"/> per ADR 0079 §1+§2 +
/// W#50 Phase 2a. Aggregates the four
/// <see cref="EngineRoomSubsystem"/> rolls into a single
/// <see cref="EngineRoomHealthSummary"/>, streams CRDT growth metrics
/// from the optional <see cref="ICrdtDocumentRegistry"/>, and emits
/// <see cref="AuditEventType.EngineRoomHealthDegraded"/> on per-tuple
/// status transitions with a configurable cooldown dedup.
/// </summary>
/// <remarks>
/// <para>
/// <b>Optional source contracts:</b> hosts that run a real sync daemon
/// register an <see cref="ISyncDaemonHealthSource"/>; hosts that run a
/// real CRDT document store register an
/// <see cref="ICrdtDocumentRegistry"/>. When a source is missing, the
/// provider returns a fail-safe default
/// (<see cref="SyncDaemonStatus.Unavailable"/> + zeros for daemon
/// telemetry; an empty stream for growth metrics) so demo / kitchen-
/// sink hosts work end-to-end without backend infrastructure.
/// </para>
/// <para>
/// <b>SubscribeHealthAsync dedup (per W#50 P2 hand-off §2):</b> emits
/// one summary on subscribe, one per status-change of any
/// <see cref="EngineRoomSubsystem"/>, and one per
/// <see cref="EngineRoomOptions.HeartbeatInterval"/> tick. Each
/// transition emits at most one
/// <see cref="AuditEventType.EngineRoomHealthDegraded"/> per
/// <c>(TenantId, EngineRoomSubsystem, statusFrom, statusTo)</c> tuple
/// within <see cref="EngineRoomOptions.DegradationDedupCooldown"/>;
/// different tuples fire independently even within the same window.
/// </para>
/// <para>
/// <b>Phase 2b deferral (CommandService):</b> the hand-off Phase 2
/// scope additionally calls for <c>DefaultEngineRoomCommandService</c>
/// (quarantine / release / compact + auth pre-flight + EOOW check). That
/// surface is deferred to a separate Phase 2b PR per the hand-off's
/// split-PR fallback (line 154-156). Phase 2b will land
/// IPermissionResolver wiring + audit-emission ordering test coverage +
/// the <c>IDocumentQuarantineStore</c> seam.
/// </para>
/// </remarks>
public sealed class DefaultEngineRoomDataProvider : IEngineRoomDataProvider
{
    private readonly IOptions<EngineRoomOptions> _options;
    private readonly ISyncDaemonHealthSource? _syncDaemon;
    private readonly ICrdtDocumentRegistry? _crdtRegistry;
    private readonly IAuditTrail? _auditTrail;
    private readonly TimeProvider _time;

    private readonly ConcurrentDictionary<DegradationKey, DateTimeOffset> _lastDegradationAuditAt =
        new();

    /// <summary>Construct the default data provider.</summary>
    public DefaultEngineRoomDataProvider(
        IOptions<EngineRoomOptions> options,
        ISyncDaemonHealthSource? syncDaemon = null,
        ICrdtDocumentRegistry? crdtRegistry = null,
        IAuditTrail? auditTrail = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _syncDaemon = syncDaemon;
        _crdtRegistry = crdtRegistry;
        _auditTrail = auditTrail;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<EngineRoomHealthSummary> GetHealthSummaryAsync(
        TenantId tenantId,
        CancellationToken ct = default)
    {
        var sync = await GetSyncDaemonHealthAsync(tenantId, ct).ConfigureAwait(false);
        var entries = new List<SubsystemHealth>(4)
        {
            new SubsystemHealth(
                EngineRoomSubsystem.MainPropulsion,
                MapSyncStatus(sync.Status),
                sync.Status == SyncDaemonStatus.Healthy ? null : SyncMessage(sync)),
            new SubsystemHealth(EngineRoomSubsystem.Electrical, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.DamageControl, SubsystemStatus.Operational, null),
            new SubsystemHealth(EngineRoomSubsystem.QaWorkshop, SubsystemStatus.Operational, null),
        };
        return new EngineRoomHealthSummary(entries);
    }

    /// <inheritdoc />
    public async ValueTask<SyncDaemonHealth> GetSyncDaemonHealthAsync(
        TenantId tenantId,
        CancellationToken ct = default)
    {
        if (_syncDaemon is null)
        {
            return new SyncDaemonHealth(
                Status: SyncDaemonStatus.Unavailable,
                PeerCount: 0,
                EventsThroughput: 0,
                GossipCycles: 0,
                AsOf: _time.GetUtcNow());
        }
        return await _syncDaemon.GetCurrentAsync(tenantId, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<CrdtGrowthMetrics> GetCrdtGrowthMetricsAsync(
        TenantId tenantId,
        CancellationToken ct = default) =>
        StreamCrdtMetricsAsync(tenantId, query: null, ct);

    /// <inheritdoc />
    public IAsyncEnumerable<CrdtGrowthMetrics> GetCrdtGrowthMetricsAsync(
        TenantId tenantId,
        CrdtGrowthQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return StreamCrdtMetricsAsync(tenantId, query, ct);
    }

    private async IAsyncEnumerable<CrdtGrowthMetrics> StreamCrdtMetricsAsync(
        TenantId tenantId,
        CrdtGrowthQuery? query,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (_crdtRegistry is null)
        {
            yield break;
        }

        await foreach (var m in _crdtRegistry
            .StreamMetricsAsync(tenantId, query, ct)
            .WithCancellation(ct)
            .ConfigureAwait(false))
        {
            // Defensive: even when a registry is registered, ensure we
            // never surface metrics for a different tenant. Hosts that
            // implement ICrdtDocumentRegistry SHOULD already enforce
            // tenant scope; we re-verify here as defence-in-depth.
            if (m.TenantId == tenantId)
            {
                yield return m;
            }
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<EngineRoomHealthSummary> SubscribeHealthAsync(
        TenantId tenantId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var prior = await GetHealthSummaryAsync(tenantId, ct).ConfigureAwait(false);
        yield return prior;

        var heartbeat = _options.Value.HeartbeatInterval;
        if (heartbeat <= TimeSpan.Zero)
        {
            yield break;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(heartbeat, _time, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            var current = await GetHealthSummaryAsync(tenantId, ct).ConfigureAwait(false);
            EmitDegradationAudits(tenantId, prior, current);
            yield return current;
            prior = current;
        }
    }

    private void EmitDegradationAudits(
        TenantId tenantId,
        EngineRoomHealthSummary prior,
        EngineRoomHealthSummary current)
    {
        if (_auditTrail is null)
        {
            return;
        }

        foreach (var subsystem in (EngineRoomSubsystem[])Enum.GetValues(typeof(EngineRoomSubsystem)))
        {
            var priorStatus = prior.For(subsystem)?.Status ?? SubsystemStatus.Unknown;
            var currentStatus = current.For(subsystem)?.Status ?? SubsystemStatus.Unknown;
            if (priorStatus == currentStatus)
            {
                continue;
            }

            // Only emit on transitions INTO degraded states; recoveries
            // back to Operational don't carry §Trust weight worth a
            // dedicated audit record (the next status-change away from
            // Operational will fire one).
            if (currentStatus is not (SubsystemStatus.Warning or SubsystemStatus.Critical))
            {
                continue;
            }

            var key = new DegradationKey(tenantId, subsystem, priorStatus, currentStatus);
            var now = _time.GetUtcNow();
            var cooldown = _options.Value.DegradationDedupCooldown;

            if (_lastDegradationAuditAt.TryGetValue(key, out var last) &&
                now - last < cooldown)
            {
                continue;
            }

            _lastDegradationAuditAt[key] = now;
            // Best-effort fire-and-forget — audit failures must not
            // propagate into the heartbeat loop. Cohort precedent:
            // DefaultPermissionResolver.EmitAsync.
            _ = TryAppendAsync(_auditTrail, key, now);
        }
    }

    private static async Task TryAppendAsync(
        IAuditTrail trail,
        DegradationKey key,
        DateTimeOffset occurredAt)
    {
        try
        {
            var payload = new AuditPayload(new Dictionary<string, object?>
            {
                ["subsystem"] = key.Subsystem.ToString(),
                ["status_from"] = key.From.ToString(),
                ["status_to"] = key.To.ToString(),
            });
            // Phase 2a stub: emit an UNSIGNED audit-payload-only marker
            // record (placeholder signature bytes). Phase 2b wires the
            // IOperationSigner cohort pattern to issue a real
            // SignedOperation envelope.
            var record = new AuditRecord(
                AuditId: Guid.NewGuid(),
                TenantId: key.TenantId,
                EventType: AuditEventType.EngineRoomHealthDegraded,
                OccurredAt: occurredAt,
                Payload: new SignedOperation<AuditPayload>(
                    Payload: payload,
                    IssuerId: Sunfish.Foundation.Crypto.PrincipalId.FromBytes(new byte[32]),
                    IssuedAt: occurredAt,
                    Nonce: Guid.NewGuid(),
                    Signature: Sunfish.Foundation.Crypto.Signature.FromBytes(new byte[64])),
                AttestingSignatures: Array.Empty<AttestingSignature>());
            await trail.AppendAsync(record, default).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Audit-backend hiccups must not stop the heartbeat. Phase 2b
            // adds proper logger plumbing per cohort precedent.
        }
    }

    private static SubsystemStatus MapSyncStatus(SyncDaemonStatus s) => s switch
    {
        SyncDaemonStatus.Healthy => SubsystemStatus.Operational,
        SyncDaemonStatus.Degraded => SubsystemStatus.Warning,
        SyncDaemonStatus.Unavailable => SubsystemStatus.Critical,
        _ => SubsystemStatus.Unknown,
    };

    private static string SyncMessage(SyncDaemonHealth h) => h.Status switch
    {
        SyncDaemonStatus.Degraded =>
            $"Sync daemon degraded ({h.PeerCount} peers, {h.EventsThroughput:F1} events/s).",
        SyncDaemonStatus.Unavailable =>
            "Sync daemon unavailable — no telemetry source registered.",
        _ => "",
    };

    private readonly record struct DegradationKey(
        TenantId TenantId,
        EngineRoomSubsystem Subsystem,
        SubsystemStatus From,
        SubsystemStatus To);
}
