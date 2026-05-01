using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>
/// Per ADR 0064-A1.6 — gates record-class writes against
/// <see cref="DataResidencyConstraint"/> tied to the resolved jurisdiction.
/// Bridge-boundary aware: deployments that route data through a Bridge
/// node enforce at upstream gate via the middleware (Phase 3); Anchor-only
/// deployments enforce inline.
/// </summary>
public interface IDataResidencyEnforcer
{
    /// <summary>
    /// Returns <see cref="EnforcementVerdict"/> for the supplied record-class
    /// + jurisdiction. <see cref="EnforcementVerdict.IsPermitted"/>=false when:
    /// (a) <c>jurisdictionCode</c> is in the constraint's
    /// <see cref="DataResidencyConstraint.ProhibitedJurisdictions"/>, or
    /// (b) the constraint specifies <see cref="DataResidencyConstraint.AllowedJurisdictions"/>
    /// and <c>jurisdictionCode</c> is not in it.
    /// </summary>
    ValueTask<EnforcementVerdict> EnforceAsync(
        string recordClass,
        string jurisdictionCode,
        CancellationToken ct = default);
}

/// <summary>Source of <see cref="DataResidencyConstraint"/>s. Phase 3 wires concrete sources.</summary>
public interface IDataResidencyConstraintSource
{
    /// <summary>Returns the constraint for <paramref name="recordClass"/> or null if unconstrained.</summary>
    DataResidencyConstraint? GetConstraint(string recordClass);
}
