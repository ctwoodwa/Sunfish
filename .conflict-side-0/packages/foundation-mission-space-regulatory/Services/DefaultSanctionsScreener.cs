using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.MissionSpace.Regulatory.Audit;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>
/// Reference <see cref="ISanctionsScreener"/> per ADR 0064-A1.3 + A1.6.
/// Operator-decision-aware emit-only; <see cref="ScreeningPolicy.AdvisoryOnly"/>
/// surfaces matches as visibility only.
/// </summary>
/// <remarks>
/// <para>
/// The substrate does NOT decide what to do with hits — that's a host
/// concern (block / log / notify). Phase 1 ships the contract; Phase 4
/// wires the audit emission (per A1.7 always-on telemetry +
/// <see cref="ScreeningPolicy.AdvisoryOnly"/> opt-out audit).
/// </para>
/// <para>
/// <b>Empty source default.</b> An <see cref="EmptySanctionsListSource"/> is
/// used when no host source is wired; <see cref="ScreenAsync"/> returns a
/// hit-free result (the substrate doesn't fabricate matches).
/// </para>
/// </remarks>
public sealed class DefaultSanctionsScreener : ISanctionsScreener
{
    private readonly ISanctionsListSource _source;
    private readonly TimeProvider _time;
    private readonly RegulatoryAuditEmitter? _emitter;

    /// <summary>Audit-disabled overload (test / bootstrap).</summary>
    public DefaultSanctionsScreener(
        ISanctionsListSource source,
        ScreeningPolicy policy = ScreeningPolicy.Default,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        Policy = policy;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Audit-enabled overload — W#32 both-or-neither contract. When the
    /// configured policy is <see cref="ScreeningPolicy.AdvisoryOnly"/>, the
    /// emitter fires <c>SanctionsAdvisoryOnlyConfigured</c> at construction
    /// (per A1.3 — operator opt-out emission path) using the supplied
    /// <paramref name="operatorPrincipalId"/> for telemetry.
    /// </summary>
    public DefaultSanctionsScreener(
        ISanctionsListSource source,
        RegulatoryAuditEmitter emitter,
        ScreeningPolicy policy,
        string operatorPrincipalId,
        TimeProvider? time = null)
        : this(source, policy, time)
    {
        ArgumentNullException.ThrowIfNull(emitter);
        ArgumentException.ThrowIfNullOrEmpty(operatorPrincipalId);
        _emitter = emitter;

        if (policy == ScreeningPolicy.AdvisoryOnly)
        {
            // Emit-once at construction; not awaited to keep ctor synchronous.
            // The operator-opt-out signal is rare + intentional — fire-and-forget
            // is acceptable per W#34 P4 precedent for one-shot configuration audits.
            _ = _emitter.EmitAsync(
                AuditEventType.SanctionsAdvisoryOnlyConfigured,
                RegulatoryAuditPayloads.SanctionsAdvisoryOnlyConfigured(operatorPrincipalId),
                CancellationToken.None);
        }
    }

    /// <inheritdoc />
    public ScreeningPolicy Policy { get; }

    /// <inheritdoc />
    public async ValueTask<SanctionsScreeningResult> ScreenAsync(string subjectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectId);
        ct.ThrowIfCancellationRequested();

        var hits = _source.MatchesFor(subjectId);

        var result = new SanctionsScreeningResult
        {
            SubjectId = subjectId,
            Hits = hits,
            Policy = Policy,
            ScreenedAt = _time.GetUtcNow(),
        };

        if (_emitter is not null && hits.Count > 0)
        {
            // SanctionsScreeningHit — 1-hour dedup keyed on (subject_id, list_source) per A1.7.
            foreach (var hit in hits)
            {
                await _emitter.EmitWithRuleDedupAsync(
                    $"sanctions-hit:{subjectId}:{hit.ListSource}",
                    AuditEventType.SanctionsScreeningHit,
                    RegulatoryAuditPayloads.SanctionsScreeningHit(
                        subjectId,
                        hit.ListSource,
                        hit.ListVersion,
                        hit.MatchScore),
                    ct).ConfigureAwait(false);
            }
        }

        return result;
    }
}

/// <summary>Phase 1 substrate's empty sanctions list source.</summary>
public sealed class EmptySanctionsListSource : ISanctionsListSource
{
    /// <inheritdoc />
    public System.Collections.Generic.IReadOnlyList<SanctionsListEntry> MatchesFor(string subjectId) =>
        Array.Empty<SanctionsListEntry>();
}
