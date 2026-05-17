namespace Sunfish.Blocks.WorkOrders.Events;

/// <summary>
/// Inbound handler for <c>Work.DeficiencyRaised</c> events from the
/// <c>blocks-property-*</c> cluster. Produces a corresponding
/// <see cref="Models.WorkOrder"/> per schema §4.1. Idempotent on
/// <see cref="DeficiencyRaisedEvent.DeficiencyId"/> — a second
/// invocation with the same DeficiencyId returns the existing
/// WorkOrder + does NOT create a duplicate.
/// </summary>
public interface IDeficiencyRaisedHandler
{
    /// <summary>
    /// Process the deficiency. Returns the created or
    /// pre-existing <see cref="Models.WorkOrder"/>.
    /// </summary>
    Task<Models.WorkOrder> HandleAsync(
        DeficiencyRaisedEvent evt,
        Guid handledBy,
        CancellationToken cancellationToken = default);
}
