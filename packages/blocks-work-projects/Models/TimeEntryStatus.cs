namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Workflow state of a <see cref="TimeEntry"/>. Transitions:
/// <c>Open → Submitted → (Approved | Rejected) → Invoiced</c>. Reverse
/// paths forbidden — corrections via new entry + a reversing entry.
/// </summary>
public enum TimeEntryStatus
{
    /// <summary>Created + still running OR stopped but not yet submitted.</summary>
    Open,

    /// <summary>Submitted to approver; awaiting decision.</summary>
    Submitted,

    /// <summary>Approver accepted — posted-then-immutable except for InvoicedFlag.</summary>
    Approved,

    /// <summary>Approver rejected with reason; worker corrects via new entry.</summary>
    Rejected,

    /// <summary>Approved entry rolled into an invoice (one-way; financial cluster reactor).</summary>
    Invoiced,
}
