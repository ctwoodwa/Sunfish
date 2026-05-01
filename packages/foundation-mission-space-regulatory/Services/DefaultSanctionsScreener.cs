using System;
using System.Threading;
using System.Threading.Tasks;

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

    /// <inheritdoc />
    public ScreeningPolicy Policy { get; }

    /// <inheritdoc />
    public ValueTask<SanctionsScreeningResult> ScreenAsync(string subjectId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subjectId);
        ct.ThrowIfCancellationRequested();

        var hits = _source.MatchesFor(subjectId);

        return ValueTask.FromResult(new SanctionsScreeningResult
        {
            SubjectId = subjectId,
            Hits = hits,
            Policy = Policy,
            ScreenedAt = _time.GetUtcNow(),
        });
    }
}

/// <summary>Phase 1 substrate's empty sanctions list source.</summary>
public sealed class EmptySanctionsListSource : ISanctionsListSource
{
    /// <inheritdoc />
    public System.Collections.Generic.IReadOnlyList<SanctionsListEntry> MatchesFor(string subjectId) =>
        Array.Empty<SanctionsListEntry>();
}
