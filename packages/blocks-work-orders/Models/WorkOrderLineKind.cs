namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Classification of a <see cref="WorkOrderLine"/>'s cost contribution.
/// Drives downstream AR rebill + AP accrual posting decisions.
/// </summary>
public enum WorkOrderLineKind
{
    /// <summary>Labor time charged at a rate.</summary>
    Labor,

    /// <summary>Material / parts consumed.</summary>
    Material,

    /// <summary>Equipment rental.</summary>
    Equipment,

    /// <summary>Subcontracted scope.</summary>
    Subcontract,

    /// <summary>Permit / inspection / dump fees.</summary>
    Fee,

    /// <summary>Reimbursable pass-through expense (mileage, tolls).</summary>
    Reimbursable,
}
