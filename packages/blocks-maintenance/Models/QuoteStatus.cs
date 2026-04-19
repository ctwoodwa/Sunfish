namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Lifecycle status of a <see cref="Quote"/>.
/// </summary>
/// <remarks>
/// Allowed transitions:
/// <code>
/// Draft → Submitted
/// Submitted → Accepted | Declined | Expired
/// </code>
/// Terminal states: <see cref="Accepted"/>, <see cref="Declined"/>, <see cref="Expired"/>, <see cref="Withdrawn"/>.
/// </remarks>
public enum QuoteStatus
{
    /// <summary>Quote is being prepared but has not been submitted.</summary>
    Draft,

    /// <summary>Quote has been submitted for consideration.</summary>
    Submitted,

    /// <summary>Quote has been accepted and a work order will be created.</summary>
    Accepted,

    /// <summary>Quote has been declined.</summary>
    Declined,

    /// <summary>Quote has passed its validity date.</summary>
    Expired,

    /// <summary>Quote was withdrawn by the vendor before a decision was made.</summary>
    Withdrawn,
}
