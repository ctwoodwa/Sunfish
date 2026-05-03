using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.MissionSpace.Regulatory.Audit;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>
/// Reference <see cref="IDataResidencyEnforcer"/> per ADR 0064-A1.6. Pure-
/// function evaluation; the constraint source is host-supplied per Phase 3
/// rule-content wiring.
/// </summary>
public sealed class DefaultDataResidencyEnforcer : IDataResidencyEnforcer
{
    private readonly IDataResidencyConstraintSource _source;
    private readonly RegulatoryAuditEmitter? _emitter;

    /// <summary>Audit-disabled overload (test / bootstrap).</summary>
    public DefaultDataResidencyEnforcer(IDataResidencyConstraintSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Audit-enabled overload — W#32 both-or-neither contract.</summary>
    public DefaultDataResidencyEnforcer(IDataResidencyConstraintSource source, RegulatoryAuditEmitter emitter)
        : this(source)
    {
        ArgumentNullException.ThrowIfNull(emitter);
        _emitter = emitter;
    }

    /// <inheritdoc />
    public async ValueTask<EnforcementVerdict> EnforceAsync(string recordClass, string jurisdictionCode, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordClass);
        ArgumentException.ThrowIfNullOrEmpty(jurisdictionCode);
        ct.ThrowIfCancellationRequested();

        var constraint = _source.GetConstraint(recordClass);
        if (constraint is null)
        {
            return new EnforcementVerdict { IsPermitted = true };
        }

        EnforcementVerdict verdict;

        // Prohibited list takes precedence: if a jurisdiction appears here,
        // the write is blocked even if also in AllowedJurisdictions.
        if (constraint.ProhibitedJurisdictions.Contains(jurisdictionCode, StringComparer.Ordinal))
        {
            verdict = new EnforcementVerdict
            {
                IsPermitted = false,
                ViolatedConstraintId = recordClass,
                Detail = $"Jurisdiction '{jurisdictionCode}' is prohibited for record-class '{recordClass}'.",
            };
        }
        // Allowed list, when non-empty, is a closed set: anything not in it is blocked.
        else if (constraint.AllowedJurisdictions.Count > 0
            && !constraint.AllowedJurisdictions.Contains(jurisdictionCode, StringComparer.Ordinal))
        {
            verdict = new EnforcementVerdict
            {
                IsPermitted = false,
                ViolatedConstraintId = recordClass,
                Detail = $"Jurisdiction '{jurisdictionCode}' is not in the allowed list for record-class '{recordClass}'.",
            };
        }
        else
        {
            verdict = new EnforcementVerdict { IsPermitted = true };
        }

        if (!verdict.IsPermitted && _emitter is not null)
        {
            // DataResidencyViolation — 1-hour dedup keyed on (recordClass, jurisdictionCode) per A1.7.
            await _emitter.EmitWithRuleDedupAsync(
                $"residency-violation:{recordClass}:{jurisdictionCode}",
                AuditEventType.DataResidencyViolation,
                RegulatoryAuditPayloads.DataResidencyViolation(recordClass, jurisdictionCode, verdict.Detail!),
                ct).ConfigureAwait(false);
        }

        return verdict;
    }
}

/// <summary>Phase 1 substrate's empty constraint source. Production hosts wire their own.</summary>
public sealed class EmptyDataResidencyConstraintSource : IDataResidencyConstraintSource
{
    /// <inheritdoc />
    public DataResidencyConstraint? GetConstraint(string recordClass) => null;
}
