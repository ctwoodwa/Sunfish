namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>Classification of a <see cref="ProjectMilestone"/>.</summary>
public enum MilestoneKind
{
    /// <summary>Schedule milestone (date-only).</summary>
    Schedule,

    /// <summary>Payment-triggering milestone (requires PaymentAmount + Currency + CustomerPartyId when TriggersInvoice).</summary>
    Payment,

    /// <summary>Phase gate / approval gate.</summary>
    Gate,

    /// <summary>Deliverable due date.</summary>
    DeliverableDue,
}
