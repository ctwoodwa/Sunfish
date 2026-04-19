namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>Classification of a <see cref="BankAccount"/>.</summary>
public enum BankAccountKind
{
    /// <summary>Standard demand-deposit checking account.</summary>
    Checking,

    /// <summary>Interest-bearing savings account.</summary>
    Savings,

    /// <summary>Credit card or line-of-credit account.</summary>
    Credit,
}
