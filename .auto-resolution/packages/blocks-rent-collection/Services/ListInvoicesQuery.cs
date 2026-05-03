using Sunfish.Blocks.RentCollection.Models;

namespace Sunfish.Blocks.RentCollection.Services;

/// <summary>
/// Filter parameters for <see cref="IRentCollectionService.ListInvoicesAsync"/>.
/// All filters are optional; omitting a field means "no constraint on that dimension".
/// </summary>
/// <param name="LeaseId">When set, only return invoices for this lease.</param>
/// <param name="ScheduleId">When set, only return invoices for this schedule.</param>
/// <param name="Status">When set, only return invoices in this status.</param>
/// <param name="DueBefore">When set, only return invoices due on or before this date.</param>
/// <param name="DueAfter">When set, only return invoices due on or after this date.</param>
public sealed record ListInvoicesQuery(
    string? LeaseId = null,
    RentScheduleId? ScheduleId = null,
    InvoiceStatus? Status = null,
    DateOnly? DueBefore = null,
    DateOnly? DueAfter = null);
