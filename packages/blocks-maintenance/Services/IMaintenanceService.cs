using Sunfish.Blocks.Maintenance.Models;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Contract for managing vendors, maintenance requests, RFQ/quote workflows, and work orders.
/// </summary>
/// <remarks>
/// Deferred in this pass (G16 part 2 first pass):
/// <list type="bullet">
///   <item><description>Deficiency → work-order auto-rollup (requires event-bus wiring)</description></item>
///   <item><description>Vendor portal UI</description></item>
///   <item><description>Quote-comparison UI</description></item>
///   <item><description>Offline mobile work-order capture</description></item>
///   <item><description>Photo/signature attachment handling</description></item>
///   <item><description>BusinessRuleEngine hookup</description></item>
/// </list>
/// </remarks>
public interface IMaintenanceService
{
    // ── Vendors ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new vendor from <paramref name="request"/> and returns the persisted record.
    /// </summary>
    /// <param name="request">Vendor creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Vendor> CreateVendorAsync(CreateVendorRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns the vendor with the specified <paramref name="id"/>, or <see langword="null"/>
    /// if no such vendor exists.
    /// </summary>
    /// <param name="id">The vendor identifier to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Vendor?> GetVendorAsync(VendorId id, CancellationToken ct = default);

    /// <summary>
    /// Streams all vendors matching <paramref name="query"/>.
    /// Pass <see cref="ListVendorsQuery.Empty"/> to return all vendors.
    /// </summary>
    /// <param name="query">Optional filter criteria.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<Vendor> ListVendorsAsync(ListVendorsQuery query, CancellationToken ct = default);

    // ── Maintenance requests ──────────────────────────────────────────────────

    /// <summary>
    /// Submits a new maintenance request from <paramref name="request"/> and returns the created record.
    /// The new request is always in <see cref="MaintenanceRequestStatus.Submitted"/>.
    /// </summary>
    /// <param name="request">Maintenance request submission payload.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<MaintenanceRequest> SubmitRequestAsync(SubmitMaintenanceRequest request, CancellationToken ct = default);

    /// <summary>
    /// Transitions a maintenance request to <paramref name="newStatus"/>.
    /// </summary>
    /// <param name="id">The request to transition.</param>
    /// <param name="newStatus">The target status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the transition is not allowed from the current status.
    /// Allowed transitions:
    /// <code>
    /// Submitted → UnderReview
    /// UnderReview → Approved | Rejected
    /// Approved → InProgress
    /// InProgress → Completed
    /// * → Cancelled  (from any non-terminal state: Submitted, UnderReview, Approved, InProgress)
    /// </code>
    /// </exception>
    ValueTask<MaintenanceRequest> TransitionRequestAsync(MaintenanceRequestId id, MaintenanceRequestStatus newStatus, CancellationToken ct = default);

    /// <summary>
    /// Returns the maintenance request with the specified <paramref name="id"/>, or <see langword="null"/>
    /// if no such request exists.
    /// </summary>
    /// <param name="id">The request identifier to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<MaintenanceRequest?> GetRequestAsync(MaintenanceRequestId id, CancellationToken ct = default);

    /// <summary>
    /// Streams all maintenance requests matching <paramref name="query"/>.
    /// Pass <see cref="ListRequestsQuery.Empty"/> to return all requests.
    /// </summary>
    /// <param name="query">Optional filter criteria.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<MaintenanceRequest> ListRequestsAsync(ListRequestsQuery query, CancellationToken ct = default);

    // ── RFQ / Quote ───────────────────────────────────────────────────────────

    /// <summary>
    /// Sends an RFQ to one or more vendors and returns the created record.
    /// The new RFQ is always in <see cref="RfqStatus.Sent"/>.
    /// </summary>
    /// <param name="request">RFQ creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Rfq> SendRfqAsync(SendRfqRequest request, CancellationToken ct = default);

    /// <summary>
    /// Records a vendor quote for a maintenance request and returns the created record.
    /// The new quote is always in <see cref="QuoteStatus.Submitted"/>.
    /// </summary>
    /// <param name="request">Quote submission payload.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<Quote> SubmitQuoteAsync(SubmitQuoteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Accepts a quote, transitioning it to <see cref="QuoteStatus.Accepted"/>,
    /// declining all other quotes for the same maintenance request,
    /// and atomically creating a new <see cref="WorkOrder"/>.
    /// </summary>
    /// <param name="id">The quote to accept.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The accepted quote.</returns>
    /// <remarks>
    /// This operation is serialized under a per-request lock so that concurrent
    /// <c>AcceptQuoteAsync</c> calls on different quotes for the same maintenance request
    /// converge to exactly one accepted quote.
    /// </remarks>
    ValueTask<Quote> AcceptQuoteAsync(QuoteId id, CancellationToken ct = default);

    /// <summary>
    /// Streams all quotes for the specified maintenance request.
    /// </summary>
    /// <param name="requestId">The maintenance request whose quotes to stream.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<Quote> ListQuotesAsync(MaintenanceRequestId requestId, CancellationToken ct = default);

    // ── Work orders ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new work order from <paramref name="request"/> and returns the persisted record.
    /// The new work order is always in <see cref="WorkOrderStatus.Draft"/>.
    /// </summary>
    /// <param name="request">Work order creation payload.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<WorkOrder> CreateWorkOrderAsync(CreateWorkOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Transitions a work order to <paramref name="newStatus"/>.
    /// </summary>
    /// <param name="id">The work order to transition.</param>
    /// <param name="newStatus">The target status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the transition is not allowed from the current status.
    /// Allowed transitions:
    /// <code>
    /// Draft → Sent
    /// Sent → Accepted | Cancelled
    /// Accepted → Scheduled
    /// Scheduled → InProgress
    /// InProgress → Completed | OnHold
    /// OnHold → InProgress
    /// </code>
    /// </exception>
    ValueTask<WorkOrder> TransitionWorkOrderAsync(WorkOrderId id, WorkOrderStatus newStatus, CancellationToken ct = default);

    /// <summary>
    /// Returns the work order with the specified <paramref name="id"/>, or <see langword="null"/>
    /// if no such work order exists.
    /// </summary>
    /// <param name="id">The work order identifier to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<WorkOrder?> GetWorkOrderAsync(WorkOrderId id, CancellationToken ct = default);

    /// <summary>
    /// Streams all work orders matching <paramref name="query"/>.
    /// Pass <see cref="ListWorkOrdersQuery.Empty"/> to return all work orders.
    /// </summary>
    /// <param name="query">Optional filter criteria.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<WorkOrder> ListWorkOrdersAsync(ListWorkOrdersQuery query, CancellationToken ct = default);
}
