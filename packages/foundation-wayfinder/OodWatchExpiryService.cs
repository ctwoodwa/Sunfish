using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Background hosted service that periodically expires
/// <see cref="OodWatchState.Active"/> watches whose
/// <c>StartedAt + MaxWatchDuration</c> has elapsed. Per ADR 0078 §5.
/// </summary>
/// <remarks>
/// Default sweep interval is 5 minutes; tests override via the
/// <see cref="SweepInterval"/> constructor parameter to avoid
/// <c>Thread.Sleep</c>. Wall-clock reads use the injected
/// <see cref="TimeProvider"/>; production hosts pass
/// <see cref="TimeProvider.System"/>; tests pass a deterministic
/// fake. R4 (XO post-merge council 2026-05-06): the cross-tenant
/// sweep enumerator lives on the internal
/// <see cref="IOodWatchSweepRepository"/>, so this service is the
/// only type that can resolve the contract — application code
/// cannot accidentally enumerate Active watches across tenants.
/// </remarks>
internal sealed class OodWatchExpiryService : BackgroundService
{
    private static readonly TimeSpan DefaultSweepInterval = TimeSpan.FromMinutes(5);

    private readonly IOodWatchRepository _repository;
    private readonly IOodWatchSweepRepository _sweepRepository;
    private readonly ILogger<OodWatchExpiryService> _logger;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly TimeProvider _timeProvider;

    /// <summary>How often the sweep fires. Defaults to 5 minutes.</summary>
    public TimeSpan SweepInterval { get; }

    /// <summary>Creates a sweep service bound to the supplied repository + sweep repo + audit + clock.</summary>
    /// <param name="repository">Per-tenant operations (used to call <see cref="IOodWatchRepository.ExpireWatchAsync"/> on each candidate).</param>
    /// <param name="sweepRepository">Cross-tenant sweep enumerator. R4 separation per W#49 P2 amendment.</param>
    /// <param name="logger">Logger. R2 (XO post-merge council 2026-05-06): non-nullable so audit-write swallows are observable.</param>
    /// <param name="auditTrail">Audit trail. MUST be non-null when expirations occur per §Trust.</param>
    /// <param name="signer">Signer for audit-record envelopes. MUST be non-null when expirations occur per §Trust.</param>
    /// <param name="timeProvider">Clock source. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="sweepInterval">Sweep cadence. Defaults to 5 minutes.</param>
    public OodWatchExpiryService(
        IOodWatchRepository repository,
        IOodWatchSweepRepository sweepRepository,
        ILogger<OodWatchExpiryService> logger,
        IAuditTrail? auditTrail = null,
        IOperationSigner? signer = null,
        TimeProvider? timeProvider = null,
        TimeSpan? sweepInterval = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _sweepRepository = sweepRepository ?? throw new ArgumentNullException(nameof(sweepRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditTrail = auditTrail;
        _signer = signer;
        _timeProvider = timeProvider ?? TimeProvider.System;
        SweepInterval = sweepInterval ?? DefaultSweepInterval;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                // R2 (XO post-merge council 2026-05-06): observable swallow.
                // Sweep failures must not crash the host; next iteration
                // retries — but they MUST surface in logs so operators can
                // detect a stuck sweep.
                _logger.LogError(ex, "OodWatchExpiry sweep iteration failed; will retry on next interval");
            }
            try
            {
                await Task.Delay(SweepInterval, _timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    /// <summary>
    /// Single-shot sweep entry-point exposed for tests. Production callers
    /// should rely on <see cref="ExecuteAsync"/>. Internal-only to honor
    /// the <see cref="IOodWatchSweepRepository.GetExpiredCandidatesAsync"/>
    /// cross-tenant single-caller invariant per W#49 P2 council Finding 6.
    /// </summary>
    internal async Task SweepOnceAsync(CancellationToken ct)
    {
        var cutoff = _timeProvider.GetUtcNow();
        await foreach (var candidate in _sweepRepository.GetExpiredCandidatesAsync(cutoff, ct).ConfigureAwait(false))
        {
            var expired = await _repository.ExpireWatchAsync(candidate.Id, ct).ConfigureAwait(false);
            await EmitExpiredAuditAsync(expired, cutoff, ct).ConfigureAwait(false);
        }
    }

    private async ValueTask EmitExpiredAuditAsync(OodWatch watch, DateTimeOffset expiredAt, CancellationToken ct)
    {
        // Council Finding 1: OOD-authority operations require IAuditTrail
        // + IOperationSigner. Fail loudly rather than expire authority
        // records with no audit trail.
        if (_auditTrail is null || _signer is null)
        {
            throw new InvalidOperationException(
                "OodWatchExpiryService requires IAuditTrail + IOperationSigner. " +
                "Register both via the host's DI container before adding the hosted service.");
        }

        var payload = new AuditPayload(new Dictionary<string, object?>
        {
            ["expiredAt"] = expiredAt.ToString("O"),
            ["maxWatchDuration"] = watch.MaxWatchDuration.ToString(),
            ["role"] = watch.Role.ToString(),
            ["severity"] = "High",
            ["tenantId"] = watch.TenantId.Value,
            ["watchId"] = watch.Id.Value,
        });
        var nonce = Guid.NewGuid();
        var signed = await _signer.SignAsync(payload, expiredAt, nonce, ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: watch.TenantId,
            EventType: AuditEventType.OodWatchExpired,
            OccurredAt: expiredAt,
            Payload: signed,
            AttestingSignatures: Array.Empty<AttestingSignature>());
        try
        {
            await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
        }
        // Council Finding 2: narrow catch — re-throw on cryptographic
        // integrity failures and cancellation; swallow only transient hiccups.
        catch (Exception ex) when (ex is not AuditSignatureException && ex is not OperationCanceledException)
        {
            // R2 (XO post-merge council 2026-05-06): observable swallow.
            _logger.LogError(ex,
                "OOD audit write failed for OodWatchExpired on watch {WatchId}; continuing best-effort",
                watch.Id.Value);
        }
    }
}
