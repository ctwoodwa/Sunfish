using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Reference <see cref="IAlertRouter"/> per ADR 0081 §2 + W#52 Phase 2.
/// Implements the 8-step <c>RouteAsync</c> contract: alert-id format
/// validation, per-rule rate limiting, audit emission ordering,
/// allowlist downgrade for high-priority routing, and destination
/// dispatch.
/// </summary>
/// <remarks>
/// <para>
/// <b>Audit emission requires</b> both <see cref="IAuditTrail"/> AND
/// <see cref="IOperationSigner"/> per the W#50 P2 cohort precedent: a
/// placeholder all-zeros signature would fail
/// <see cref="IAuditTrail.AppendAsync"/>'s envelope verification and
/// the failure would be silently swallowed, producing §Trust gaps.
/// When either dependency is absent the router still routes (via
/// <see cref="ILookout"/> / <see cref="ISonarStore"/>) but skips audit
/// emission entirely. Production hosts MUST register both.
/// </para>
/// <para>
/// <b>Tenant binding (§8.2):</b> when an
/// <see cref="ITenantContext"/> is registered, the router verifies
/// <c>alert.TenantId == ambient.Tenant.Id</c> before routing; on
/// mismatch it emits
/// <see cref="AuditEventType.TacticalAuthorizationDenied"/> with
/// <c>denialReason="tenant-mismatch"</c> and throws
/// <see cref="TacticalUnauthorizedException"/>.
/// </para>
/// </remarks>
public sealed class DefaultAlertRouter : IAlertRouter
{
    // ADR 0081 §1 — alertId regex.
    private static readonly Regex AlertIdRegex =
        new(@"^[A-Za-z0-9_\-\.:]{1,128}$", RegexOptions.Compiled);

    private readonly IOptions<TacticalOptions> _options;
    private readonly ILookout _lookout;
    private readonly ISonarStore _sonar;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly ITenantContext? _tenantContext;
    private readonly ILogger<DefaultAlertRouter> _logger;
    private readonly TimeProvider _time;

    // Per-(TenantId, RuleName) sliding rate-limit window. Each entry is
    // a circular buffer of routing timestamps within the last minute.
    private readonly ConcurrentDictionary<RateKey, RateCounter> _rateCounters = new();

    /// <summary>Construct the alert router.</summary>
    public DefaultAlertRouter(
        IOptions<TacticalOptions> options,
        ILookout lookout,
        ISonarStore sonar,
        IAuditTrail? auditTrail = null,
        IOperationSigner? signer = null,
        ITenantContext? tenantContext = null,
        ILogger<DefaultAlertRouter>? logger = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(lookout);
        ArgumentNullException.ThrowIfNull(sonar);
        if ((auditTrail is null) ^ (signer is null))
        {
            throw new ArgumentException(
                "auditTrail and signer must both be supplied together, or both null. " +
                "Registering only one creates silent §Trust gaps in audit emission.");
        }
        _options = options;
        _lookout = lookout;
        _sonar = sonar;
        _auditTrail = auditTrail;
        _signer = signer;
        _tenantContext = tenantContext;
        _logger = logger ?? NullLogger<DefaultAlertRouter>.Instance;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <b>Audit-emission propagation policy (§Trust + W#52 P2 council Major):</b>
    /// any audit emission may fail with
    /// <see cref="AuditSignatureException"/>; the impl propagates such
    /// failures rather than swallowing them — an unsigned-but-routed
    /// alert is a §Trust violation worse than a missed route. Destination
    /// failures (<see cref="ILookout.WriteAsync"/> / <see cref="ISonarStore.WriteAsync"/>)
    /// are best-effort: they are caught and logged at Warning per the
    /// hand-off "audit records retained on destination failure" contract.
    /// </remarks>
    public async ValueTask RouteAsync(TacticalAlert alert, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        ct.ThrowIfCancellationRequested();

        // §8.2 tenant binding — fail-closed when an ambient context is
        // registered. Per W#52 P2 council Major M2: a registered-but-
        // unresolved context (Tenant == null) is also a §Trust hole; treat
        // as denial rather than silent pass-through.
        if (_tenantContext is not null)
        {
            if (_tenantContext.Tenant is null)
            {
                await EmitDenialAsync(alert, "tenant-unresolved", ct).ConfigureAwait(false);
                throw new TacticalUnauthorizedException(
                    "Tenant context is registered but unresolved; cannot verify alert tenant binding.");
            }
            if (_tenantContext.Tenant.Id != alert.TenantId)
            {
                await EmitDenialAsync(alert, "tenant-mismatch", ct).ConfigureAwait(false);
                throw new TacticalUnauthorizedException(
                    $"Alert TenantId '{alert.TenantId}' does not match ambient '{_tenantContext.Tenant.Id}'.");
            }
        }

        // Step 1 — AlertId regex validation.
        if (!AlertIdRegex.IsMatch(alert.AlertId))
        {
            await EmitDenialAsync(alert, "invalid-alert-id", ct).ConfigureAwait(false);
            return;
        }

        // Step 2 — per-(TenantId, RuleName) rate limit. GetOrAdd's
        // factory may run twice under contention (cohort note); the
        // loser counter is GC'd and the winning counter is the only
        // one ever observed by subsequent calls — effective rate-limit
        // window is single, not 2x.
        var ruleKey = new RateKey(alert.TenantId, alert.RuleName);
        var now = _time.GetUtcNow();
        var counter = _rateCounters.GetOrAdd(ruleKey, static _ => new RateCounter());
        if (!counter.TryAdmit(now, _options.Value.MaxAlertsPerMinutePerRule))
        {
            await EmitDenialAsync(alert, "rule-rate-limit", ct).ConfigureAwait(false);
            return;
        }

        // §8.3 high-priority allowlist gate (decided pre-emit so all
        // audit records reflect the final routing).
        var routing = alert.RoutingPolicy;
        var downgraded = false;
        if (routing == AlertRoutingPolicy.HighPriorityLookout
            && !MatchesAllowlist(alert.RuleName))
        {
            routing = AlertRoutingPolicy.InformationalSonar;
            downgraded = true;
        }

        // Per W#52 P2 council Critical C1: if a downgrade applies, emit
        // the denial FIRST so a signing failure on the denial cannot
        // leave AnomalyDetected+AlertRouted as a phantom-routing pair.
        // Audit-by-construction: all-three-records-or-none.
        if (downgraded)
        {
            await EmitDenialAsync(
                alert, "high-priority-routing-not-allowlisted", ct).ConfigureAwait(false);
        }

        // Steps 3 + 4 — emission BEFORE destination write.
        await EmitAsync(AuditEventType.AnomalyDetected, alert,
            ExtraFields(("rule_name", alert.RuleName), ("severity", alert.Severity.ToString())),
            ct).ConfigureAwait(false);
        await EmitAsync(AuditEventType.AlertRouted, alert,
            ExtraFields(("rule_name", alert.RuleName), ("policy", routing.ToString()),
                ("downgraded", downgraded)),
            ct).ConfigureAwait(false);

        // Step 6 — dispatch. Destination failure is best-effort per the
        // hand-off "audit records retained" contract.
        try
        {
            if (routing == AlertRoutingPolicy.HighPriorityLookout)
            {
                await _lookout.WriteAsync(alert, ct).ConfigureAwait(false);
            }
            else
            {
                await _sonar.WriteAsync(alert, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Tactical alert {AlertId} routing to {Policy} failed; audit records retained.",
                alert.AlertId, routing);
        }
    }

    private bool MatchesAllowlist(string ruleName)
    {
        var allowed = _options.Value.AllowedHighPriorityRulePrefixes;
        if (allowed is null || allowed.Count == 0)
        {
            return false;
        }
        foreach (var prefix in allowed)
        {
            if (string.IsNullOrEmpty(prefix)) continue;
            if (prefix.EndsWith('*'))
            {
                var stem = prefix[..^1];
                if (ruleName.StartsWith(stem, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (ruleName.Equals(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private ValueTask EmitDenialAsync(
        TacticalAlert alert,
        string denialReason,
        CancellationToken ct) =>
        EmitAsync(
            AuditEventType.TacticalAuthorizationDenied,
            alert,
            ExtraFields(("denial_reason", denialReason), ("rule_name", alert.RuleName)),
            ct);

    private async ValueTask EmitAsync(
        AuditEventType eventType,
        TacticalAlert alert,
        IEnumerable<KeyValuePair<string, object?>> extras,
        CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null)
        {
            return;
        }

        var occurredAt = _time.GetUtcNow();
        var body = new Dictionary<string, object?>
        {
            ["alert_id"] = alert.AlertId,
        };
        foreach (var kv in extras)
        {
            body[kv.Key] = kv.Value;
        }
        var payload = new AuditPayload(body);

        try
        {
            var nonce = Guid.NewGuid();
            var signed = await _signer
                .SignAsync(payload, occurredAt, nonce, ct)
                .ConfigureAwait(false);
            var record = new AuditRecord(
                AuditId: Guid.NewGuid(),
                TenantId: alert.TenantId,
                EventType: eventType,
                OccurredAt: occurredAt,
                Payload: signed,
                AttestingSignatures: Array.Empty<AttestingSignature>());
            await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AuditSignatureException)
        {
            // Signature failures are §Trust-elevated — propagate rather
            // than swallow so the host can recover (cohort precedent:
            // DefaultPermissionResolver.EmitAsync).
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Tactical audit append failed for {EventType} on alert {AlertId}; continuing best-effort.",
                eventType, alert.AlertId);
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> ExtraFields(
        params (string Key, object? Value)[] pairs)
    {
        foreach (var p in pairs)
        {
            yield return new KeyValuePair<string, object?>(p.Key, p.Value);
        }
    }

    private readonly record struct RateKey(TenantId TenantId, string RuleName);

    private sealed class RateCounter
    {
        private readonly object _gate = new();
        private readonly Queue<DateTimeOffset> _ticks = new();

        public bool TryAdmit(DateTimeOffset now, int limitPerMinute)
        {
            lock (_gate)
            {
                var window = TimeSpan.FromMinutes(1);
                while (_ticks.Count > 0 && now - _ticks.Peek() > window)
                {
                    _ticks.Dequeue();
                }
                if (_ticks.Count >= limitPerMinute)
                {
                    return false;
                }
                _ticks.Enqueue(now);
                return true;
            }
        }
    }
}
