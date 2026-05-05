using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Default reference implementation of <see cref="IOodWatchService"/>.
/// Composes <see cref="IOodWatchRepository"/> with audit emission. Per
/// ADR 0078 §2.
/// </summary>
/// <remarks>
/// <para>
/// Per the H4 resolution (XO directive 2026-05-05): attesting-signature
/// enforcement is the responsibility of the API/gateway layer (capability
/// check + principal authentication). This domain service trusts the
/// authenticated <c>requestedBy</c> <see cref="ActorId"/> that arrives
/// through the already-validated call path — consistent with every other
/// domain service in the <c>Sunfish.Foundation</c> tier.
/// </para>
/// <para>
/// Wall-clock reads use <see cref="TimeProvider.GetUtcNow"/>; tests inject
/// a <c>FakeTimeProvider</c>-style subclass to avoid <c>Thread.Sleep</c>.
/// </para>
/// </remarks>
public sealed class DefaultOodWatchService : IOodWatchService
{
    private readonly IOodWatchRepository _repository;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a service bound to the supplied repository + audit + clock.</summary>
    /// <param name="repository">Persistence boundary; throws on the single-Active invariant.</param>
    /// <param name="auditTrail">Optional audit trail. Null skips audit emission silently.</param>
    /// <param name="signer">Optional signer for audit-record envelopes. Null skips audit emission silently.</param>
    /// <param name="timeProvider">Clock source. Defaults to <see cref="TimeProvider.System"/>.</param>
    public DefaultOodWatchService(
        IOodWatchRepository repository,
        IAuditTrail? auditTrail = null,
        IOperationSigner? signer = null,
        TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _auditTrail = auditTrail;
        _signer = signer;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<OodWatch> StartWatchAsync(
        TenantId tenantId, ActorId onWatchActor, OodRole role,
        TimeSpan? maxDuration, ActorId requestedBy, CancellationToken ct = default)
    {
        var existing = await _repository.GetCurrentWatchAsync(tenantId, role, ct).ConfigureAwait(false);
        if (existing is not null)
            throw new OodWatchConflictException(existing.Id, tenantId, role);

        var watch = await _repository.StartWatchAsync(
            tenantId, onWatchActor, role, maxDuration, requestedBy, ct).ConfigureAwait(false);

        await EmitStartedAuditAsync(watch, requestedBy, ct).ConfigureAwait(false);
        return watch;
    }

    /// <inheritdoc />
    public async ValueTask<(OodWatch Relieved, OodWatch Started)> HandoverWatchAsync(
        OodWatchId currentWatchId, ActorId incomingActor,
        ActorId requestedBy, string? reason, CancellationToken ct = default)
    {
        // The repository contract owns the (TenantId, OodRole) discovery for
        // the supplied watchId — RelieveWatchAsync will throw if the watch is
        // not Active. We re-fetch immediately after to read tenant + role for
        // the start-leg of the atomic handover.
        var relieved = await _repository.RelieveWatchAsync(currentWatchId, requestedBy, ct).ConfigureAwait(false);
        if (relieved.State != OodWatchState.Relieved)
            throw new OodWatchConflictException(currentWatchId, relieved.TenantId, relieved.Role);

        var started = await _repository.StartWatchAsync(
            relieved.TenantId, incomingActor, relieved.Role,
            relieved.MaxWatchDuration, requestedBy, ct).ConfigureAwait(false);

        await EmitRelievedAuditAsync(relieved, requestedBy, reason, ct).ConfigureAwait(false);
        await EmitStartedAuditAsync(started, requestedBy, ct).ConfigureAwait(false);

        // TODO(W#49-P2): emit watch-transfer Standing Order via IStandingOrderIssuer
        // once W#42 Phase 2 is built and the issuer is on origin/main. Path:
        // coordination/ood-watch/{role.ToString().ToLowerInvariant()}/transfer
        // with IssuedDuringWatchId = started.Id.
        return (relieved, started);
    }

    /// <inheritdoc />
    public ValueTask<OodWatch?> GetActiveWatchAsync(
        TenantId tenantId, OodRole role, CancellationToken ct = default)
        => _repository.GetCurrentWatchAsync(tenantId, role, ct);

    private ValueTask EmitStartedAuditAsync(OodWatch watch, ActorId requestedBy, CancellationToken ct)
        => EmitAuditAsync(
            AuditEventType.OodWatchStarted,
            watch.TenantId,
            new AuditPayload(new Dictionary<string, object?>
            {
                ["actor"] = watch.OnWatchActor.Value,
                ["role"] = watch.Role.ToString(),
                ["severity"] = "High",
                ["startedBy"] = requestedBy.Value,
                ["tenantId"] = watch.TenantId.Value,
                ["watchId"] = watch.Id.Value,
            }),
            ct);

    private ValueTask EmitRelievedAuditAsync(OodWatch watch, ActorId requestedBy, string? reason, CancellationToken ct)
    {
        var body = new Dictionary<string, object?>
        {
            ["actor"] = watch.OnWatchActor.Value,
            ["relievedBy"] = requestedBy.Value,
            ["role"] = watch.Role.ToString(),
            ["severity"] = "Normal",
            ["tenantId"] = watch.TenantId.Value,
            ["watchId"] = watch.Id.Value,
        };
        if (reason is not null) body["reason"] = reason;
        return EmitAuditAsync(AuditEventType.OodWatchRelieved, watch.TenantId, new AuditPayload(body), ct);
    }

    private async ValueTask EmitAuditAsync(
        AuditEventType eventType, TenantId tenantId, AuditPayload payload, CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null) return;
        var occurredAt = _timeProvider.GetUtcNow();
        var nonce = Guid.NewGuid();
        var signed = await _signer.SignAsync(payload, occurredAt, nonce, ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: Array.Empty<AttestingSignature>());
        try
        {
            await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
        }
        catch
        {
            // Best-effort audit emission — matches cohort precedent
            // (TenantKeyProviderFieldDecryptor / ExtensionFieldCatalog). Audit
            // backend hiccups must not deny domain operations.
        }
    }
}
