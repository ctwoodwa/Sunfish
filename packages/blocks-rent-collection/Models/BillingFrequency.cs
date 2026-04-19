namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>Defines how often a rent schedule generates invoices.</summary>
public enum BillingFrequency
{
    /// <summary>One invoice per calendar month.</summary>
    Monthly,

    /// <summary>One invoice every two months.</summary>
    BiMonthly,

    /// <summary>One invoice every three months.</summary>
    Quarterly,

    /// <summary>One invoice per year.</summary>
    Annually,
}
