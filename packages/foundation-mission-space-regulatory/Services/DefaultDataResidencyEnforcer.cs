using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>
/// Reference <see cref="IDataResidencyEnforcer"/> per ADR 0064-A1.6. Pure-
/// function evaluation; the constraint source is host-supplied per Phase 3
/// rule-content wiring.
/// </summary>
public sealed class DefaultDataResidencyEnforcer : IDataResidencyEnforcer
{
    private readonly IDataResidencyConstraintSource _source;

    public DefaultDataResidencyEnforcer(IDataResidencyConstraintSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc />
    public ValueTask<EnforcementVerdict> EnforceAsync(string recordClass, string jurisdictionCode, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(recordClass);
        ArgumentException.ThrowIfNullOrEmpty(jurisdictionCode);
        ct.ThrowIfCancellationRequested();

        var constraint = _source.GetConstraint(recordClass);
        if (constraint is null)
        {
            return ValueTask.FromResult(new EnforcementVerdict { IsPermitted = true });
        }

        // Prohibited list takes precedence: if a jurisdiction appears here,
        // the write is blocked even if also in AllowedJurisdictions.
        if (constraint.ProhibitedJurisdictions.Contains(jurisdictionCode, StringComparer.Ordinal))
        {
            return ValueTask.FromResult(new EnforcementVerdict
            {
                IsPermitted = false,
                ViolatedConstraintId = recordClass,
                Detail = $"Jurisdiction '{jurisdictionCode}' is prohibited for record-class '{recordClass}'.",
            });
        }

        // Allowed list, when non-empty, is a closed set: anything not in it is blocked.
        if (constraint.AllowedJurisdictions.Count > 0
            && !constraint.AllowedJurisdictions.Contains(jurisdictionCode, StringComparer.Ordinal))
        {
            return ValueTask.FromResult(new EnforcementVerdict
            {
                IsPermitted = false,
                ViolatedConstraintId = recordClass,
                Detail = $"Jurisdiction '{jurisdictionCode}' is not in the allowed list for record-class '{recordClass}'.",
            });
        }

        return ValueTask.FromResult(new EnforcementVerdict { IsPermitted = true });
    }
}

/// <summary>Phase 1 substrate's empty constraint source. Production hosts wire their own.</summary>
public sealed class EmptyDataResidencyConstraintSource : IDataResidencyConstraintSource
{
    /// <inheritdoc />
    public DataResidencyConstraint? GetConstraint(string recordClass) => null;
}
