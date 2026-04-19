namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Lifecycle status of a <see cref="Rfq"/> (Request for Quote).
/// </summary>
/// <remarks>
/// Allowed transitions:
/// <code>
/// Draft → Sent
/// Sent → Closed | Cancelled
/// </code>
/// Terminal states: <see cref="Closed"/>, <see cref="Cancelled"/>.
/// </remarks>
public enum RfqStatus
{
    /// <summary>RFQ is being prepared but has not been sent to vendors.</summary>
    Draft,

    /// <summary>RFQ has been sent to invited vendors and is awaiting responses.</summary>
    Sent,

    /// <summary>RFQ has been closed (response period ended or a quote was accepted).</summary>
    Closed,

    /// <summary>RFQ has been cancelled before vendors could respond.</summary>
    Cancelled,
}
