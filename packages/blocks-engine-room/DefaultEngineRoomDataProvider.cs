using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly IOperationSigner? _signer;
    private readonly ILogger<DefaultEngineRoomDataProvider> _logger;
    private readonly TimeProvider _time;

    private readonly ConcurrentDictionary<DegradationKey, DateTimeOffset> _lastDegradationAuditAt =
        new();

    /// <summary>Construct the default data provider.</summary>
    /// <remarks>
    /// Audit emission requires BOTH <paramref name="auditTrail"/> AND
    /// <paramref name="signer"/> to be registered (per W#50 P2 council
    /// Critical: a placeholder all-zeros signature would fail
    /// <see cref="IAuditTrail.AppendAsync"/>'s envelope verification and
    /// the failure would be swallowed, producing silent §Trust gaps). When
    /// either dependency is absent the provider skips degradation
    /// audit emission entirely; Phase 2b will land the full signer-
    /// integrated path.
    /// </remarks>
    public DefaultEngineRoomDataProvider(
        IOptions<EngineRoomOptions> options,
        ISyncDaemonHealthSource? syncDaemon = null,
        ICrdtDocumentRegistry? crdtRegistry = null,
        IAuditTrail? auditTrail = null,
        IOperationSigner? signer = null,
        ILogger<DefaultEngineRoomDataProvider>? logger = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _syncDaemon = syncDaemon;
        _crdtRegistry = crdtRegistry;
        _auditTrail = auditTrail;
        _signer = signer;
        _logger = logger ?? NullLogger<DefaultEngineRoomDataProvider>.Instance;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<EngineRoomHealthSummary> GetHealthSummaryAsync(
        TenantId tenantId,
        CancellationToken ct = default)
    {
        var sync = await GetSyncDaemonHealthAsync(tenantId, ct).ConfigureAwait(false);
        // Per W#50 P2 council Critical (W#54 P2 precedent): subsystems
        // without a registered probe source surface as Unknown, NOT
        // Operational. Otherwise a UI tile would render "all green" while
        // the host has no probe — a §Trust misrepresentation.
        var entries = new List<SubsystemHealth>(4)
        {
            new SubsystemHealth(
                EngineRoomSubsystem.MainPropulsion,
                MapSyncStatus(sync.Status, syncSourceRegistered: _syncDaemon is not null),
                sync.Status == SyncDaemonStatus.Healthy ? null : SyncMessage(sync, _syncDaemon is not null)),
            new SubsystemHealth(
                EngineRoomSubsystem.Electrical,
                SubsystemStatus.Unknown,
                "No Electrical probe source registered (Phase 2a)."),
            new SubsystemHealth(
                EngineRoomSubsystem.DamageControl,
                SubsystemStatus.Unknown,
                "No Damage Control probe source registered (Phase 2a — wires in Phase 2b)."),
            new SubsystemHealth(
                EngineRoomSubsystem.QaWorkshop,
                SubsystemStatus.Unknown,
                "No QA Workshop probe source registered (Phase 2a)."),
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

            // Per W#50 P2 council Major: synthesize Unavailable on
            // sync-source faults so the heartbeat loop survives
            // transient telemetry failures (cohort precedent:
            // DefaultPermissionResolver.EmitAsync swallows audit
            // failures the same way).
            EngineRoomHealthSummary current;
            try
            {
                current = await GetHealthSummaryAsync(tenantId, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Engine Room heartbeat health rollup failed; surfacing Unknown summary and continuing.");
                current = new EngineRoomHealthSummary(
                [
                    new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, SubsystemStatus.Unknown, "Telemetry source threw — see logs."),
                    new SubsystemHealth(EngineRoomSubsystem.Electrical, SubsystemStatus.Unknown, null),
                    new SubsystemHealth(EngineRoomSubsystem.DamageControl, SubsystemStatus.Unknown, null),
                    new SubsystemHealth(EngineRoomSubsystem.QaWorkshop, SubsystemStatus.Unknown, null),
                ]);
            }

            EmitDegradationAudits(tenantId, prior, current);
            yield return current;
            prior = current;
        }
    }

    /// <summary>
    /// Per W#50 P2 council Critical: audit emission requires BOTH a
    /// registered <see cref="IAuditTrail"/> AND
    /// <see cref="IOperationSigner"/>. With either missing the provider
    /// skips emission entirely (NOT a silent fake-signed record that
    /// would throw <see cref="AuditSignatureException"/> at the audit
    /// trail boundary). Phase 2b wires the signer-integrated path.
    /// </summary>
    internal void EmitDegradationAudits(
        TenantId tenantId,
        EngineRoomHealthSummary prior,
        EngineRoomHealthSummary current)
    {
        if (_auditTrail is null || _signer is null)
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

            // Atomic dedup — AddOrUpdate so two concurrent subscribers can
            // never both observe "no prior" and both proceed to emit. If
            // the existing entry is within cooldown, the closure returns
            // it unchanged and the resulting value will not equal `now`,
            // so the emit is skipped.
            var actual = _lastDegradationAuditAt.AddOrUpdate(
                key,
                _ => now,
                (_, last) => now - last < cooldown ? last : now);
            if (actual != now)
            {
                continue;
            }

            _ = TryAppendAsync(_auditTrail, _signer, key, now, _logger);
        }
    }

    private static async Task TryAppendAsync(
        IAuditTrail trail,
        IOperationSigner signer,
        DegradationKey key,
        DateTimeOffset occurredAt,
        ILogger logger)
    {
        try
        {
            var payload = new AuditPayload(new Dictionary<string, object?>
            {
                ["subsystem"] = key.Subsystem.ToString(),
                ["status_from"] = key.From.ToString(),
                ["status_to"] = key.To.ToString(),
            });
            var nonce = Guid.NewGuid();
            var signed = await signer.SignAsync(payload, occurredAt, nonce, default)
                .ConfigureAwait(false);
            var record = new AuditRecord(
                AuditId: Guid.NewGuid(),
                TenantId: key.TenantId,
                EventType: AuditEventType.EngineRoomHealthDegraded,
                OccurredAt: occurredAt,
                Payload: signed,
                AttestingSignatures: Array.Empty<AttestingSignature>());
            await trail.AppendAsync(record, default).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Audit-backend hiccups must not stop the heartbeat — but per
            // W#50 P2 council Major-1, swallowed-but-logged is the
            // canonical posture. Cohort precedent:
            // DefaultPermissionResolver.EmitAsync.
            logger.LogError(ex,
                "Engine Room degradation audit append failed for {Subsystem} ({From} → {To}); continuing best-effort.",
                key.Subsystem, key.From, key.To);
        }
    }

    private static SubsystemStatus MapSyncStatus(SyncDaemonStatus s, bool syncSourceRegistered)
    {
        // Per W#50 P2 council Critical: when no sync-daemon source is
        // registered, surface Unknown rather than mapping the synthetic
        // Unavailable default to Critical (which would imply real
        // probe data exists and is failing). Only an authentic source
        // returning Unavailable maps to Critical.
        if (!syncSourceRegistered)
        {
            return SubsystemStatus.Unknown;
        }
        return s switch
        {
            SyncDaemonStatus.Healthy => SubsystemStatus.Operational,
            SyncDaemonStatus.Degraded => SubsystemStatus.Warning,
            SyncDaemonStatus.Unavailable => SubsystemStatus.Critical,
            _ => SubsystemStatus.Unknown,
        };
    }

    private static string SyncMessage(SyncDaemonHealth h, bool syncSourceRegistered)
    {
        if (!syncSourceRegistered)
        {
            return "No sync-daemon telemetry source registered (Phase 2a stub).";
        }
        return h.Status switch
        {
            SyncDaemonStatus.Degraded =>
                $"Sync daemon degraded ({h.PeerCount} peers, {h.EventsThroughput:F1} events/s).",
            SyncDaemonStatus.Unavailable =>
                "Sync daemon unavailable — no peers reachable.",
            _ => "",
        };
    }

    private readonly record struct DegradationKey(
        TenantId TenantId,
        EngineRoomSubsystem Subsystem,
        SubsystemStatus From,
        SubsystemStatus To);
}
