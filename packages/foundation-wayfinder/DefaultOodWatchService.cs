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
/// <para>
/// <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/> are MANDATORY
/// for OOD-authority operations per ADR 0078 §Trust + W#49 P2 council
/// Finding 1. Constructor accepts them as nullable for DI ergonomics; the
/// emit path throws <see cref="InvalidOperationException"/> at first
/// invocation when either is missing — fail loudly rather than run
/// authority operations with zero audit trail.
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
    /// <param name="auditTrail">Audit trail. MUST be non-null at first authority invocation per §Trust.</param>
    /// <param name="signer">Signer for audit-record envelopes. MUST be non-null at first authority invocation per §Trust.</param>
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

        await EmitStartedAuditAsync(watch, requestedBy, _timeProvider.GetUtcNow(), ct).ConfigureAwait(false);
        return watch;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Delegates to <see cref="IOodWatchRepository.HandoverWatchAsync"/> which
    /// owns transactional atomicity per ADR 0078 §2 + W#49 P2 council Finding 3.
    /// On partial failure the repository rolls back, so we never observe an
    /// authority-vacuum state. The relieved watch's
    /// <see cref="OodWatch.MaxWatchDuration"/> is inherited by the new watch
    /// per the repository contract; callers needing a fresh TTL must
    /// <see cref="StartWatchAsync"/> separately after relieve.
    /// </remarks>
    public async ValueTask<(OodWatch Relieved, OodWatch Started)> HandoverWatchAsync(
        OodWatchId currentWatchId, ActorId incomingActor,
        ActorId requestedBy, string? reason, CancellationToken ct = default)
    {
        // Capture a single OccurredAt so the relieve + start audit records
        // share an identical timestamp (council Finding 9).
        var occurredAt = _timeProvider.GetUtcNow();
        var (relieved, started) = await _repository.HandoverWatchAsync(
            currentWatchId, incomingActor, requestedBy, ct).ConfigureAwait(false);

        await EmitRelievedAuditAsync(relieved, requestedBy, reason, occurredAt, ct).ConfigureAwait(false);
        await EmitStartedAuditAsync(started, requestedBy, occurredAt, ct).ConfigureAwait(false);

        // TODO(W#49-P3): emit watch-transfer Standing Order via IStandingOrderIssuer
        // once W#42 Phase 2 is built and the issuer is on origin/main. Field
        // StandingOrder.IssuedDuringWatchId is already available (P1). Path:
        // coordination/ood-watch/{role.ToString().ToLowerInvariant()}/transfer.
        return (relieved, started);
    }

    /// <inheritdoc />
    public ValueTask<OodWatch?> GetActiveWatchAsync(
        TenantId tenantId, OodRole role, CancellationToken ct = default)
        => _repository.GetCurrentWatchAsync(tenantId, role, ct);

    private ValueTask EmitStartedAuditAsync(OodWatch watch, ActorId requestedBy, DateTimeOffset occurredAt, CancellationToken ct)
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
            occurredAt,
            ct);

    private ValueTask EmitRelievedAuditAsync(OodWatch watch, ActorId requestedBy, string? reason, DateTimeOffset occurredAt, CancellationToken ct)
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
        return EmitAuditAsync(AuditEventType.OodWatchRelieved, watch.TenantId, new AuditPayload(body), occurredAt, ct);
    }

    private async ValueTask EmitAuditAsync(
        AuditEventType eventType, TenantId tenantId, AuditPayload payload,
        DateTimeOffset occurredAt, CancellationToken ct)
    {
        // Council Finding 1: OOD-authority operations require IAuditTrail
        // + IOperationSigner. Fail loudly at first invocation rather than
        // run authority ops with zero audit trail.
        if (_auditTrail is null || _signer is null)
        {
            throw new InvalidOperationException(
                "OOD-authority operations require IAuditTrail + IOperationSigner. " +
                "Register both via the host's DI container before invoking IOodWatchService.");
        }

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
        // Council Finding 2: narrow catch — re-throw on cryptographic
        // integrity failures (AuditSignatureException) and cancellation;
        // swallow only transient transport / backend hiccups.
        catch (Exception ex) when (ex is not AuditSignatureException && ex is not OperationCanceledException)
        {
            // Best-effort: audit-backend hiccups must not deny authority operations.
        }
    }
}
