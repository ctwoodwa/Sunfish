using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Reference <see cref="IThreatTriggerService"/> per ADR 0081 §4 +
/// W#52 Phase 2c. Mints emergency Standing Orders from
/// <see cref="TacticalAlert"/> values that match registered
/// <see cref="ThreatTriggerTemplate"/> patterns.
/// </summary>
/// <remarks>
/// <para>
/// <b>Authority gate (per <c>tactical-p2-system-principal-authority-addendum.md</c>):</b>
/// authority is identity-based, NOT role-based.
/// <c>ShipRole.System</c> does NOT exist; ADR 0077 amendment is
/// out-of-scope for v1. Instead, the service resolves the system
/// principal via <see cref="ISystemPrincipalProvider"/>; if the
/// resolution returns null OR throws, the operation is denied with
/// <c>denial_reason="no-system-principal-registered"</c>. The
/// caller's <c>IPermissionResolver</c> is NOT consulted —
/// emergency Standing Orders are system-only by construction.
/// </para>
/// <para>
/// <b>8-step OOO (per ADR 0081 §4 + W#52 hand-off §2.3):</b>
/// 1. Tenant binding (alert vs ambient ITenantContext).
/// 2. Template lookup by RuleName.
/// 3. Severity gate (alert.Severity ≤ template.MinimumSeverity ordinal).
/// 4. Per-(TenantId, RuleName) dedup window — within 60s, return
///    cached orderId rather than re-issuing.
/// 5. Per-tenant rate limit (TacticalOptions.MaxEmergencyOrdersPerMinute).
/// 6. Resolve system principal; null/throw → denial.
/// 7. Template substitution + size check (≤2048 chars post-substitute).
/// 8. Emit EmergencyStandingOrderIssued (BEFORE AppendAsync) → call
///    IStandingOrderRepository.AppendAsync → on failure log + emit
///    EmergencyStandingOrderIssuanceFailed; on success return orderId.
/// </para>
/// <para>
/// <b>Cohort lessons (W#52 P2b council, PR #707):</b>
/// <list type="bullet">
/// <item><description>All per-rule rate-limit / dedup state is keyed by
/// <c>(TenantId, RuleName)</c> — never RuleName alone. Cross-tenant
/// cooldown leakage would let one tenant consume another's audit
/// budget.</description></item>
/// <item><description><c>tenant_id</c> is included INSIDE the signed
/// <see cref="AuditPayload"/> (not just the outer
/// <see cref="AuditRecord.TenantId"/> envelope) so the
/// <see cref="IOperationSigner"/> signature covers the tenant binding.
/// A compromised storage layer cannot swap the outer tenant field
/// without invalidating the signature.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Audit emission cohort posture (W#50 / W#52 P2a/b precedent):</b>
/// emission requires BOTH <see cref="IAuditTrail"/> AND
/// <see cref="IOperationSigner"/>; partial registration is rejected
/// at construction (XOR guard). When neither is registered, denial
/// emission silently skips (Phase 2c will gate harder once host
/// wiring matures).
/// </para>
/// <para>
/// <b>Phase 2c scope cuts (filed for follow-up):</b> per-signal-
/// fingerprint budget (§4.4) is NOT implemented in this PR — the
/// (TenantId, RuleName) dedup window approximates the constraint.
/// Per-signal budget tracking lands in a Phase 2c addendum once the
/// signal-fingerprint canonicalization rules are decided.
/// </para>
/// </remarks>
public sealed class DefaultThreatTriggerService : IThreatTriggerService
{
    private const int MaxOrderContentChars = 2048;

    private readonly IOptions<TacticalOptions> _options;
    private readonly ISystemPrincipalProvider _systemPrincipalProvider;
    private readonly IStandingOrderRepository _orderRepository;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly ITenantContext? _tenantContext;
    private readonly ILogger<DefaultThreatTriggerService> _logger;
    private readonly TimeProvider _time;

    private readonly object _registrationGate = new();
    private readonly Dictionary<string, ThreatTriggerTemplate> _templates =
        new(StringComparer.Ordinal);
    private int _firstIssueProcessed; // 0 = open, 1 = closed

    // Per W#52 P2b council — keyed by (TenantId, RuleName) NOT RuleName alone.
    private readonly ConcurrentDictionary<DedupKey, DedupEntry> _dedupCache = new();
    private readonly ConcurrentDictionary<TenantId, RateCounter> _tenantRateCounters = new();

    /// <summary>Construct the threat-trigger service.</summary>
    public DefaultThreatTriggerService(
        IOptions<TacticalOptions> options,
        ISystemPrincipalProvider systemPrincipalProvider,
        IStandingOrderRepository orderRepository,
        IAuditTrail? auditTrail = null,
        IOperationSigner? signer = null,
        ITenantContext? tenantContext = null,
        ILogger<DefaultThreatTriggerService>? logger = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(systemPrincipalProvider);
        ArgumentNullException.ThrowIfNull(orderRepository);
        if ((auditTrail is null) ^ (signer is null))
        {
            throw new ArgumentException(
                "auditTrail and signer must both be supplied together, or both null. "
                + "Registering only one creates silent §Trust gaps in audit emission.");
        }
        _options = options;
        _systemPrincipalProvider = systemPrincipalProvider;
        _orderRepository = orderRepository;
        _auditTrail = auditTrail;
        _signer = signer;
        _tenantContext = tenantContext;
        _logger = logger ?? NullLogger<DefaultThreatTriggerService>.Instance;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Templates are GLOBAL — keyed by <see cref="ThreatTriggerTemplate.RuleName"/>
    /// without a tenant scope. This is by design per ADR 0081 §4: the
    /// template library is host-admin configuration, not user data, and
    /// every tenant's rule X uses the same emergency-order template.
    /// Per W#52 P2c council Major: registration is gated by the
    /// <c>open-then-closed</c> first-issue epoch (callers must register
    /// at startup before the first <see cref="TryIssueAsync"/> call);
    /// post-startup template injection is therefore impossible.
    /// </remarks>
    public void RegisterTemplate(ThreatTriggerTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentException.ThrowIfNullOrEmpty(template.RuleName);
        ArgumentException.ThrowIfNullOrEmpty(template.OrderContent);

        if (Volatile.Read(ref _firstIssueProcessed) != 0)
        {
            throw new InvalidOperationException(
                $"Cannot register template for '{template.RuleName}' — first issue already "
                + "processed; registration epoch is closed (ADR 0081 §4 contract).");
        }

        lock (_registrationGate)
        {
            if (Volatile.Read(ref _firstIssueProcessed) != 0)
            {
                throw new InvalidOperationException(
                    $"Cannot register template for '{template.RuleName}' — first issue "
                    + "processed during registration; registration epoch is closed.");
            }
            if (!_templates.TryAdd(template.RuleName, template))
            {
                throw new InvalidOperationException(
                    $"A template for rule '{template.RuleName}' is already registered.");
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask<string?> TryIssueAsync(
        TacticalAlert alert,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(alert);
        ct.ThrowIfCancellationRequested();
        Interlocked.Exchange(ref _firstIssueProcessed, 1);

        // Per W#52 P2c council Major M2: opportunistic sweep of stale
        // dedup entries to bound memory under long-running tenants
        // with many rules. Cheap: O(n) walk on call-paths only, no
        // background timer.
        SweepStaleDedupEntries(_time.GetUtcNow());

        // Step 1 — tenant binding (per §8.2).
        if (_tenantContext is not null)
        {
            if (_tenantContext.Tenant is null)
            {
                await EmitDenialAsync(alert,
                    AuditEventType.TacticalAuthorizationDenied, "tenant-unresolved",
                    ct).ConfigureAwait(false);
                return null;
            }
            if (_tenantContext.Tenant.Id != alert.TenantId)
            {
                await EmitDenialAsync(alert,
                    AuditEventType.TacticalAuthorizationDenied, "tenant-mismatch",
                    ct).ConfigureAwait(false);
                return null;
            }
        }

        // Step 2 — template lookup.
        ThreatTriggerTemplate? template;
        lock (_registrationGate)
        {
            _templates.TryGetValue(alert.RuleName, out template);
        }
        if (template is null)
        {
            return null; // No template; non-event (not a denial).
        }

        // Step 3 — severity gate. Lower ordinal = more severe.
        if ((int)alert.Severity > (int)template.MinimumSeverity)
        {
            return null;
        }

        var dedupKey = new DedupKey(alert.TenantId, alert.RuleName);
        var now = _time.GetUtcNow();

        // Step 4 — dedup. TryAdd gives exactly-once first-wins semantics.
        // AddOrUpdate cannot guarantee this: two concurrent callers both
        // observe OrderId=null (in-flight) and both proceed — a §Trust
        // double-issuance race (W#52 P2c council Blocking B1).
        // TryAdd: exactly one caller returns true and owns the slot; all
        // others read the existing entry and return early.
        if (!_dedupCache.TryAdd(dedupKey, new DedupEntry(now, OrderId: null)))
        {
            if (_dedupCache.TryGetValue(dedupKey, out var existing))
            {
                // Return cached orderId (completed) or null (in-flight, suppress).
                return existing.OrderId;
            }
            // Entry was swept between TryAdd and TryGetValue — suppress conservatively.
            return null;
        }
        // This caller owns the dedup slot; proceed through steps 5–8.

        // Step 5 — per-tenant rate limit. Per W#52 P2c council Major M1:
        // roll back the dedup-cache seed on early exit so the next
        // legitimate caller within the window doesn't observe a stale
        // null-orderId entry.
        var rateCounter = _tenantRateCounters.GetOrAdd(alert.TenantId,
            static _ => new RateCounter());
        if (!rateCounter.TryAdmit(now, _options.Value.MaxEmergencyOrdersPerMinute))
        {
            _dedupCache.TryRemove(dedupKey, out _);
            await EmitDenialAsync(alert,
                AuditEventType.EmergencyStandingOrderIssuanceFailed, "rate-limit",
                ct).ConfigureAwait(false);
            return null;
        }

        // Step 6 — resolve system principal.
        Principal? systemPrincipal;
        try
        {
            systemPrincipal = await _systemPrincipalProvider
                .GetSystemPrincipalAsync(alert.TenantId, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ISystemPrincipalProvider threw resolving system principal for tenant {TenantId}.",
                alert.TenantId);
            _dedupCache.TryRemove(dedupKey, out _);
            await EmitDenialAsync(alert,
                AuditEventType.EmergencyStandingOrderIssuanceFailed,
                "no-system-principal-registered", ct).ConfigureAwait(false);
            return null;
        }
        if (systemPrincipal is null)
        {
            _dedupCache.TryRemove(dedupKey, out _);
            await EmitDenialAsync(alert,
                AuditEventType.EmergencyStandingOrderIssuanceFailed,
                "no-system-principal-registered", ct).ConfigureAwait(false);
            return null;
        }

        // Step 7 — template substitution + size guard.
        var substituted = SubstituteTemplate(template.OrderContent, alert);
        if (substituted.Length > MaxOrderContentChars)
        {
            _dedupCache.TryRemove(dedupKey, out _);
            throw new ArgumentException(
                $"Substituted OrderContent for rule '{alert.RuleName}' is "
                + $"{substituted.Length} chars; exceeds {MaxOrderContentChars}-char cap.",
                nameof(alert));
        }

        // Step 8 — emit Issued (BEFORE AppendAsync) and write the order.
        var orderGuid = Guid.NewGuid();
        var orderId = orderGuid.ToString("N");
        var issuerActorId = new ActorId(systemPrincipal.Id.ToBase64Url());

        // Per W#52 P2c council Advisory A2: on AuditSignatureException from the
        // Issued emission, release the dedup slot before re-throwing so a retry
        // can proceed once the signing issue is resolved.
        try
        {
            await EmitIssuedAsync(alert, orderId, issuerActorId, ct).ConfigureAwait(false);
        }
        catch (AuditSignatureException)
        {
            _dedupCache.TryRemove(dedupKey, out _);
            throw;
        }

        var standingOrderId = new StandingOrderId(orderGuid);
        var order = new StandingOrder(
            Id: standingOrderId,
            TenantId: alert.TenantId,
            IssuedBy: issuerActorId,
            IssuedAt: now,
            Scope: StandingOrderScope.Tenant,
            Triples: new[]
            {
                new StandingOrderTriple(
                    Path: $"emergency.{alert.RuleName}",
                    OldValue: null,
                    NewValue: JsonValue.Create("active")),
            },
            Rationale: substituted,
            ApprovalChain: null,
            // Phase 2c: AuditRecordId placeholder; Phase 2d will thread the
            // actual emit-record id through. The Issued audit emission
            // above carries the cross-reference for now.
            AuditRecordId: new AuditRecordId(Guid.NewGuid()),
            State: StandingOrderState.Issued,
            IssuedDuringWatchId: null);

        try
        {
            await _orderRepository.AppendAsync(order, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "IStandingOrderRepository.AppendAsync failed for emergency order {OrderId} on tenant {TenantId}.",
                orderId, alert.TenantId);
            await EmitDenialAsync(alert,
                AuditEventType.EmergencyStandingOrderIssuanceFailed, "append-failed",
                ct).ConfigureAwait(false);
            // Roll back the dedup entry so a retry can fire.
            _dedupCache.TryRemove(dedupKey, out _);
            return null;
        }

        // Update the dedup cache entry with the now-known orderId.
        _dedupCache[dedupKey] = new DedupEntry(now, orderId);
        return orderId;
    }

    // ADR 0081 §4 supported placeholders.
    private static readonly Regex PlaceholderRegex = new(
        @"\{(AlertId|RuleName|Severity|DetectedAt)\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Single-pass placeholder substitution. Sequential
    /// <see cref="string.Replace(string, string, StringComparison)"/>
    /// would be vulnerable to sequence-injection: an attacker-controlled
    /// <see cref="TacticalAlert.AlertId"/> containing the literal
    /// <c>{RuleName}</c> would, after the first Replace, get its
    /// embedded token expanded by subsequent Replace calls — letting
    /// the persisted <see cref="StandingOrder.Rationale"/> spoof a
    /// rule name. Per W#52 P2c council Critical C1: a single-pass
    /// regex callback closes the hole.
    /// </summary>
    private static string SubstituteTemplate(string content, TacticalAlert alert) =>
        PlaceholderRegex.Replace(content, m => m.Groups[1].Value switch
        {
            "AlertId" => alert.AlertId,
            "RuleName" => alert.RuleName,
            "Severity" => alert.Severity.ToString(),
            "DetectedAt" => alert.DetectedAt.ToString("o"),
            _ => m.Value,
        });

    private ValueTask EmitDenialAsync(
        TacticalAlert alert,
        AuditEventType eventType,
        string denialReason,
        CancellationToken ct) =>
        EmitAsync(eventType, alert,
            ExtraFields(("denial_reason", denialReason), ("rule_name", alert.RuleName)),
            ct);

    private ValueTask EmitIssuedAsync(
        TacticalAlert alert,
        string orderId,
        ActorId issuerActorId,
        CancellationToken ct) =>
        EmitAsync(AuditEventType.EmergencyStandingOrderIssued, alert,
            ExtraFields(
                ("order_id", orderId),
                ("rule_name", alert.RuleName),
                ("issued_by", issuerActorId.Value)),
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
        // Per W#52 P2b council: tenant_id INSIDE signed payload.
        var body = new Dictionary<string, object?>
        {
            ["tenant_id"] = alert.TenantId.Value,
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
            var signed = await _signer.SignAsync(payload, occurredAt, nonce, ct)
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
        catch (OperationCanceledException) { throw; }
        catch (AuditSignatureException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Tactical threat-trigger audit append failed for {EventType} on alert {AlertId}.",
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

    private void SweepStaleDedupEntries(DateTimeOffset now)
    {
        var window = TimeSpan.FromSeconds(60);
        foreach (var kvp in _dedupCache)
        {
            if (now - kvp.Value.IssuedAt > window)
            {
                _dedupCache.TryRemove(
                    new KeyValuePair<DedupKey, DedupEntry>(kvp.Key, kvp.Value));
            }
        }
    }

    private readonly record struct DedupKey(TenantId TenantId, string RuleName);
    private readonly record struct DedupEntry(DateTimeOffset IssuedAt, string? OrderId);

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
                if (_ticks.Count >= limitPerMinute) return false;
                _ticks.Enqueue(now);
                return true;
            }
        }
    }
}
