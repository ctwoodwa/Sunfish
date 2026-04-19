namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>Lifecycle status of a rent <see cref="Invoice"/>.</summary>
public enum InvoiceStatus
{
    /// <summary>Generated but not yet issued to the tenant.</summary>
    Draft,

    /// <summary>Issued and awaiting payment.</summary>
    Open,

    /// <summary>At least one payment has been received but the balance is not cleared.</summary>
    PartiallyPaid,

    /// <summary>Balance is fully cleared (AmountPaid &gt;= AmountDue).</summary>
    Paid,

    /// <summary>Past due date with no full payment received.</summary>
    Overdue,

    /// <summary>Invoice has been voided and should not be collected.</summary>
    Cancelled,
}
