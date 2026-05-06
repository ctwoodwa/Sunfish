using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
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
/// Reference <see cref="ITacticalRuleEngine"/> per ADR 0081 §2 + W#52
/// Phase 2b. Implements the registration / evaluation / streaming
/// contracts on top of a per-rule error-rate tracker and per-tenant
/// signal-channel partitioning.
/// </summary>
/// <remarks>
/// <para>
/// <b>Registration invariants (per ADR 0081 §2 + §8.3):</b>
/// <list type="bullet">
/// <item><description>Duplicate <see cref="ITacticalRule.RuleName"/>
/// throws <see cref="InvalidOperationException"/>.</description></item>
/// <item><description>Reserved <c>sunfish.*</c> prefix from a non-
/// first-party assembly throws — the assembly identity check uses
/// <c>Assembly.GetName().Name?.StartsWith("Sunfish.")</c> as a Phase
/// 2b proxy for verified first-party identity.</description></item>
/// <item><description>Registration after the first signal has been
/// processed (epoch closed) throws.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Evaluation contract:</b> <see cref="Evaluate(TacticalSignal)"/>
/// invokes every rule in registration order; per-rule exceptions are
/// caught and contribute to a per-rule error-rate counter. When a
/// rule throws &gt; 100 times in any 60s window, the engine emits
/// <see cref="AuditEventType.TacticalAuthorizationDenied"/> with
/// <c>denial_reason="rule-evaluation-failure-rate"</c> AT MOST once
/// per minute per rule (cooldown reset after a quiet window).
/// </para>
/// <para>
/// <b>Streaming contract:</b> <see cref="EvaluateStreamAsync"/> reads
/// signals from the source enumerable and dispatches them to per-
/// tenant <see cref="Channel{T}"/> writers; each tenant has a
/// dedicated reader task that processes its signals serially in
/// submission order (per ADR 0081 §2.2). Cross-tenant signals
/// process in parallel.
/// </para>
/// <para>
/// <b>Audit emission cohort posture (W#50 + W#52 P2a precedent):</b>
/// emission requires BOTH <see cref="IAuditTrail"/> AND
/// <see cref="IOperationSigner"/>; partial registration (one but not
/// the other) is rejected at construction with
/// <see cref="ArgumentException"/> per the security council XOR-guard
/// invariant.
/// </para>
/// </remarks>
public sealed class DefaultTacticalRuleEngine : ITacticalRuleEngine
{
    private const int RuleErrorRateThresholdPerMinute = 100;
    private const string ReservedPrefix = "sunfish.";

    private readonly IOptions<TacticalOptions> _options;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly ILogger<DefaultTacticalRuleEngine> _logger;
    private readonly TimeProvider _time;

    private readonly object _registrationGate = new();
    private readonly List<ITacticalRule> _rules = new();
    private readonly HashSet<string> _ruleNames = new(StringComparer.Ordinal);
    private int _firstSignalProcessed; // 0 = open, 1 = closed

    private readonly ConcurrentDictionary<string, RuleErrorTracker> _errorTrackers = new();

    /// <summary>Construct the rule engine.</summary>
    public DefaultTacticalRuleEngine(
        IOptions<TacticalOptions> options,
        IAuditTrail? auditTrail = null,
        IOperationSigner? signer = null,
        ILogger<DefaultTacticalRuleEngine>? logger = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if ((auditTrail is null) ^ (signer is null))
        {
            throw new ArgumentException(
                "auditTrail and signer must both be supplied together, or both null. "
                + "Registering only one creates silent §Trust gaps in audit emission.");
        }
        _options = options;
        _auditTrail = auditTrail;
        _signer = signer;
        _logger = logger ?? NullLogger<DefaultTacticalRuleEngine>.Instance;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public void RegisterRule(ITacticalRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentException.ThrowIfNullOrEmpty(rule.RuleName);

        if (Volatile.Read(ref _firstSignalProcessed) != 0)
        {
            throw new InvalidOperationException(
                $"Cannot register rule '{rule.RuleName}' — first signal already processed; "
                + "registration epoch is closed (ADR 0081 §2 contract).");
        }

        if (rule.RuleName.StartsWith(ReservedPrefix, StringComparison.Ordinal)
            && !IsFirstPartyAssembly(rule.GetType()))
        {
            throw new InvalidOperationException(
                $"Rule name '{rule.RuleName}' uses the reserved 'sunfish.*' prefix but is "
                + $"declared in non-first-party assembly '{rule.GetType().Assembly.GetName().Name}'. "
                + "Per ADR 0081 §8.3 the prefix is reserved for assemblies whose name starts with "
                + "'Sunfish.'. Third-party rules MUST use '{vendor}.{product}.{signal}' form.");
        }

        lock (_registrationGate)
        {
            // Re-check epoch under the lock to close the
            // registration-vs-first-signal race.
            if (Volatile.Read(ref _firstSignalProcessed) != 0)
            {
                throw new InvalidOperationException(
                    $"Cannot register rule '{rule.RuleName}' — first signal processed during "
                    + "registration; registration epoch is closed.");
            }
            if (!_ruleNames.Add(rule.RuleName))
            {
                throw new InvalidOperationException(
                    $"A rule with name '{rule.RuleName}' is already registered.");
            }
            _rules.Add(rule);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<TacticalAlert> Evaluate(TacticalSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        // First-time entry closes the registration epoch.
        Interlocked.Exchange(ref _firstSignalProcessed, 1);

        // Snapshot the rule list under the lock to avoid evaluating
        // against a list that's being mutated by a concurrent
        // RegisterRule (which is technically prevented by the epoch
        // guard but defensively snapshot anyway).
        ITacticalRule[] snapshot;
        lock (_registrationGate)
        {
            snapshot = _rules.ToArray();
        }

        var emitted = new List<TacticalAlert>();
        foreach (var rule in snapshot)
        {
            TacticalAlert? alert;
            try
            {
                if (rule.Evaluate(signal, out alert) && alert is not null)
                {
                    emitted.Add(alert);
                }
            }
            catch (Exception ex)
            {
                RecordRuleError(rule, ex, signal.TenantId);
            }
        }
        return emitted;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TacticalAlert> EvaluateStreamAsync(
        IAsyncEnumerable<TacticalSignal> signals,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(signals);

        var output = Channel.CreateUnbounded<TacticalAlert>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        // Per-tenant ordering: each tenant gets a Channel<TacticalSignal>
        // + dedicated reader task; cross-tenant signals process in parallel.
        var tenantWriters = new ConcurrentDictionary<TenantId, ChannelWriter<TacticalSignal>>();
        var tenantTasks = new ConcurrentDictionary<TenantId, Task>();

        var dispatcher = Task.Run(async () =>
        {
            try
            {
                await foreach (var signal in signals.WithCancellation(ct).ConfigureAwait(false))
                {
                    var writer = tenantWriters.GetOrAdd(signal.TenantId, tid =>
                    {
                        var ch = Channel.CreateUnbounded<TacticalSignal>(
                            new UnboundedChannelOptions { SingleReader = true });
                        var reader = ch.Reader;
                        var task = Task.Run(async () =>
                        {
                            await foreach (var s in reader.ReadAllAsync(ct).ConfigureAwait(false))
                            {
                                foreach (var alert in Evaluate(s))
                                {
                                    await output.Writer.WriteAsync(alert, ct).ConfigureAwait(false);
                                }
                            }
                        }, ct);
                        tenantTasks[tid] = task;
                        return ch.Writer;
                    });
                    await writer.WriteAsync(signal, ct).ConfigureAwait(false);
                }
                foreach (var w in tenantWriters.Values)
                {
                    w.TryComplete();
                }
                await Task.WhenAll(tenantTasks.Values).ConfigureAwait(false);
                output.Writer.TryComplete();
            }
            catch (OperationCanceledException)
            {
                output.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                output.Writer.TryComplete(ex);
            }
        }, ct);

        await foreach (var alert in output.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return alert;
        }
        // Surface any dispatcher fault.
        await dispatcher.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public IReadOnlyList<ITacticalRule> GetRegisteredRules()
    {
        lock (_registrationGate)
        {
            return _rules.ToArray();
        }
    }

    private static bool IsFirstPartyAssembly(Type t) =>
        t.Assembly.GetName().Name?.StartsWith("Sunfish.", StringComparison.Ordinal) == true;

    private void RecordRuleError(ITacticalRule rule, Exception ex, TenantId tenantId)
    {
        var tracker = _errorTrackers.GetOrAdd(rule.RuleName, static _ => new RuleErrorTracker());
        var now = _time.GetUtcNow();
        var (count, shouldEmit) = tracker.Record(now, RuleErrorRateThresholdPerMinute);

        _logger.LogWarning(ex,
            "Tactical rule {RuleName} threw during evaluation (recent count {Count}); continuing.",
            rule.RuleName, count);

        if (shouldEmit)
        {
            // Best-effort fire-and-forget; we want emission to NOT
            // throw out of Evaluate's hot path.
            _ = TryEmitFailureRateDenialAsync(rule.RuleName, tenantId, count, now);
        }
    }

    private async Task TryEmitFailureRateDenialAsync(
        string ruleName,
        TenantId tenantId,
        int count,
        DateTimeOffset occurredAt)
    {
        if (_auditTrail is null || _signer is null)
        {
            return;
        }
        try
        {
            var payload = new AuditPayload(new Dictionary<string, object?>
            {
                ["denial_reason"] = "rule-evaluation-failure-rate",
                ["rule_name"] = ruleName,
                ["throw_count"] = count,
                ["window_seconds"] = 60,
            });
            var nonce = Guid.NewGuid();
            var signed = await _signer
                .SignAsync(payload, occurredAt, nonce, default)
                .ConfigureAwait(false);
            var record = new AuditRecord(
                AuditId: Guid.NewGuid(),
                TenantId: tenantId,
                EventType: AuditEventType.TacticalAuthorizationDenied,
                OccurredAt: occurredAt,
                Payload: signed,
                AttestingSignatures: Array.Empty<AttestingSignature>());
            await _auditTrail.AppendAsync(record, default).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Tactical rule-error-rate denial emission failed for {RuleName}.", ruleName);
        }
    }

    private sealed class RuleErrorTracker
    {
        private readonly object _gate = new();
        private readonly Queue<DateTimeOffset> _ticks = new();
        private DateTimeOffset _lastEmittedAt = DateTimeOffset.MinValue;

        public (int Count, bool ShouldEmit) Record(
            DateTimeOffset now, int thresholdPerMinute)
        {
            lock (_gate)
            {
                var window = TimeSpan.FromMinutes(1);
                while (_ticks.Count > 0 && now - _ticks.Peek() > window)
                {
                    _ticks.Dequeue();
                }
                _ticks.Enqueue(now);
                var count = _ticks.Count;
                if (count > thresholdPerMinute && now - _lastEmittedAt >= window)
                {
                    _lastEmittedAt = now;
                    return (count, true);
                }
                return (count, false);
            }
        }
    }
}
