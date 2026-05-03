using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;
using Xunit;

namespace Sunfish.Blocks.Maintenance.Tests;

public class InMemoryMaintenanceServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static readonly EntityId TestPropertyId = new("property", "test", "prop-1");
    private static readonly EntityId TestPropertyId2 = new("property", "test", "prop-2");
    private static readonly TenantId TestTenant = new("tenant-test");

    private static InMemoryMaintenanceService MakeService() => new();

    private static async Task<(InMemoryMaintenanceService svc, Vendor vendor)> MakeServiceWithVendor(
        VendorSpecialty specialty = VendorSpecialty.Plumbing,
        VendorStatus status = VendorStatus.Active)
    {
        var svc = MakeService();
        var vendor = await svc.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "Test Plumber LLC",
            Specialties = VendorSpecialtyClassifications.ToList(specialty),
            Status = status,
        });
        return (svc, vendor);
    }

    private static async Task<(InMemoryMaintenanceService svc, MaintenanceRequest request)> MakeServiceWithRequest(
        MaintenancePriority priority = MaintenancePriority.Normal)
    {
        var svc = MakeService();
        var request = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId = TestPropertyId,
            RequestedByDisplayName = "Jane Tenant",
            Description = "Leaky faucet in kitchen",
            Priority = priority,
            RequestedDate = new DateOnly(2026, 5, 1),
        });
        return (svc, request);
    }

    private static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
            list.Add(item);
        return list;
    }

    // ── Vendor lifecycle ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateVendorAsync_RoundTrip_ReturnsVendorWithId()
    {
        var svc = MakeService();

        var vendor = await svc.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "Ace Plumbing",
            ContactName = "Bob Builder",
            ContactEmail = "bob@aceplumbing.com",
            Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing),
        });

        Assert.False(string.IsNullOrWhiteSpace(vendor.Id.Value));
        Assert.Equal("Ace Plumbing", vendor.DisplayName);
        Assert.Equal("Bob Builder", vendor.ContactName);
        Assert.Equal("plumbing", vendor.Specialties[0].Code);
        Assert.Equal(VendorStatus.Active, vendor.Status);
        Assert.Equal(VendorOnboardingState.Pending, vendor.OnboardingState);

        var retrieved = await svc.GetVendorAsync(vendor.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(vendor.Id, retrieved.Id);
    }

    [Fact]
    public async Task GetVendorAsync_UnknownId_ReturnsNull()
    {
        var svc = MakeService();

        var result = await svc.GetVendorAsync(new VendorId("no-such-vendor"));

        Assert.Null(result);
    }

    [Fact]
    public async Task ListVendorsAsync_NoFilter_ReturnsAllVendors()
    {
        var svc = MakeService();
        await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V1", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing) });
        await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V2", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Electrical) });

        var all = await CollectAsync(svc.ListVendorsAsync(ListVendorsQuery.Empty));

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task ListVendorsAsync_FilterBySpecialty_ReturnsMatchingOnly()
    {
        var svc = MakeService();
        await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "Plumber", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing) });
        await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "Electrician", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Electrical) });

        var plumbers = await CollectAsync(svc.ListVendorsAsync(new ListVendorsQuery { SpecialtyCode = "plumbing" }));

        Assert.Single(plumbers);
        Assert.Equal("Plumber", plumbers[0].DisplayName);
    }

    [Fact]
    public async Task ListVendorsAsync_FilterByActiveStatus_ExcludesSuspended()
    {
        var svc = MakeService();
        await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "Active", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.HVAC), Status = VendorStatus.Active });
        await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "Suspended", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.HVAC), Status = VendorStatus.Suspended });

        var active = await CollectAsync(svc.ListVendorsAsync(new ListVendorsQuery { Status = VendorStatus.Active }));

        Assert.Single(active);
        Assert.Equal("Active", active[0].DisplayName);
    }

    // ── MaintenanceRequest lifecycle ──────────────────────────────────────────

    [Fact]
    public async Task SubmitRequestAsync_CreatesRequest_InSubmittedStatus()
    {
        var (svc, request) = await MakeServiceWithRequest();

        Assert.False(string.IsNullOrWhiteSpace(request.Id.Value));
        Assert.Equal(MaintenanceRequestStatus.Submitted, request.Status);
        Assert.Equal(TestPropertyId, request.PropertyId);
        Assert.Null(request.DeficiencyReference);
    }

    [Fact]
    public async Task SubmitRequestAsync_WithDeficiencyReference_StoresOpaqueString()
    {
        var svc = MakeService();
        const string defRef = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

        var request = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId = TestPropertyId,
            RequestedByDisplayName = "Manager",
            Description = "From inspection deficiency",
            Priority = MaintenancePriority.High,
            RequestedDate = new DateOnly(2026, 6, 1),
            DeficiencyReference = defRef,
        });

        Assert.Equal(defRef, request.DeficiencyReference);
    }

    [Fact]
    public async Task TransitionRequest_ValidPath_SubmittedToCompleted()
    {
        var (svc, request) = await MakeServiceWithRequest();

        var r1 = await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.UnderReview);
        Assert.Equal(MaintenanceRequestStatus.UnderReview, r1.Status);

        var r2 = await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.Approved);
        Assert.Equal(MaintenanceRequestStatus.Approved, r2.Status);

        var r3 = await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.InProgress);
        Assert.Equal(MaintenanceRequestStatus.InProgress, r3.Status);

        var r4 = await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.Completed);
        Assert.Equal(MaintenanceRequestStatus.Completed, r4.Status);
    }

    [Fact]
    public async Task TransitionRequest_InvalidTransition_ThrowsInvalidOperationException()
    {
        var (svc, request) = await MakeServiceWithRequest();
        // Reject from Submitted (skip UnderReview → Approved → InProgress → Completed)
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.Completed).AsTask());
    }

    [Theory]
    [InlineData(MaintenanceRequestStatus.Submitted)]
    [InlineData(MaintenanceRequestStatus.UnderReview)]
    [InlineData(MaintenanceRequestStatus.Approved)]
    [InlineData(MaintenanceRequestStatus.InProgress)]
    public async Task TransitionRequest_CancelAllowedFromAnyNonTerminalState(MaintenanceRequestStatus startFrom)
    {
        var (svc, request) = await MakeServiceWithRequest();

        // Advance to the desired starting state first.
        if (startFrom >= MaintenanceRequestStatus.UnderReview)
            await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.UnderReview);
        if (startFrom >= MaintenanceRequestStatus.Approved)
            await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.Approved);
        if (startFrom == MaintenanceRequestStatus.InProgress)
            await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.InProgress);

        var cancelled = await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.Cancelled);
        Assert.Equal(MaintenanceRequestStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task TransitionRequest_FromTerminalState_Throws()
    {
        var (svc, request) = await MakeServiceWithRequest();
        await svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.Cancelled);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.TransitionRequestAsync(request.Id, MaintenanceRequestStatus.UnderReview).AsTask());
    }

    [Fact]
    public async Task GetRequestAsync_UnknownId_ReturnsNull()
    {
        var svc = MakeService();

        var result = await svc.GetRequestAsync(new MaintenanceRequestId("no-such-request"));

        Assert.Null(result);
    }

    // ── RFQ / Quote ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SendRfqAsync_InvitesAllListedVendors()
    {
        var (svc, request) = await MakeServiceWithRequest();
        var v1 = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V1", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing) });
        var v2 = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V2", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing) });

        var rfq = await svc.SendRfqAsync(new SendRfqRequest
        {
            RequestId = request.Id,
            InvitedVendors = [v1.Id, v2.Id],
            ResponseDueDate = new DateOnly(2026, 5, 15),
            Scope = "Fix leaky faucet",
        });

        Assert.Equal(RfqStatus.Sent, rfq.Status);
        Assert.Equal(2, rfq.InvitedVendors.Count);
        Assert.Contains(v1.Id, rfq.InvitedVendors);
        Assert.Contains(v2.Id, rfq.InvitedVendors);
    }

    [Fact]
    public async Task SubmitQuoteAsync_IsIndependentPerVendor()
    {
        var (svc, request) = await MakeServiceWithRequest();
        var v1 = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V1", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing) });
        var v2 = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V2", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing) });

        var q1 = await svc.SubmitQuoteAsync(new SubmitQuoteRequest { VendorId = v1.Id, RequestId = request.Id, Amount = 200m, ValidUntil = new DateOnly(2026, 6, 1) });
        var q2 = await svc.SubmitQuoteAsync(new SubmitQuoteRequest { VendorId = v2.Id, RequestId = request.Id, Amount = 180m, ValidUntil = new DateOnly(2026, 6, 1) });

        Assert.Equal(QuoteStatus.Submitted, q1.Status);
        Assert.Equal(QuoteStatus.Submitted, q2.Status);
        Assert.NotEqual(q1.Id, q2.Id);

        var allQuotes = await CollectAsync(svc.ListQuotesAsync(request.Id));
        Assert.Equal(2, allQuotes.Count);
    }

    [Fact]
    public async Task AcceptQuoteAsync_AcceptsTarget_DeclinesOthers_SpawnsWorkOrder()
    {
        var (svc, request) = await MakeServiceWithRequest();
        var v1 = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V1", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing) });
        var v2 = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V2", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing) });

        var q1 = await svc.SubmitQuoteAsync(new SubmitQuoteRequest { VendorId = v1.Id, RequestId = request.Id, Amount = 200m, ValidUntil = new DateOnly(2026, 6, 1) });
        var q2 = await svc.SubmitQuoteAsync(new SubmitQuoteRequest { VendorId = v2.Id, RequestId = request.Id, Amount = 180m, ValidUntil = new DateOnly(2026, 6, 1) });

        var accepted = await svc.AcceptQuoteAsync(q1.Id);

        Assert.Equal(QuoteStatus.Accepted, accepted.Status);

        // The other quote should be Declined.
        var declined = await CollectAsync(svc.ListQuotesAsync(request.Id));
        var declinedQuote = declined.First(q => q.Id == q2.Id);
        Assert.Equal(QuoteStatus.Declined, declinedQuote.Status);

        // A WorkOrder should have been created.
        var workOrders = await CollectAsync(svc.ListWorkOrdersAsync(ListWorkOrdersQuery.Empty));
        Assert.Single(workOrders);
        Assert.Equal(WorkOrderStatus.Draft, workOrders[0].Status);
        Assert.Equal(v1.Id, workOrders[0].AssignedVendorId);
        Assert.Equal(Sunfish.Foundation.Integrations.Payments.Money.Usd(200m), workOrders[0].EstimatedCost);
    }

    [Fact]
    public async Task AcceptQuoteAsync_IsAtomic_ConcurrentCallsConvergeToExactlyOneAccepted()
    {
        // Arrange: one request, two quotes from different vendors.
        var (svc, request) = await MakeServiceWithRequest();
        var v1 = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V1", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing) });
        var v2 = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V2", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing) });

        var q1 = await svc.SubmitQuoteAsync(new SubmitQuoteRequest { VendorId = v1.Id, RequestId = request.Id, Amount = 200m, ValidUntil = new DateOnly(2026, 6, 1) });
        var q2 = await svc.SubmitQuoteAsync(new SubmitQuoteRequest { VendorId = v2.Id, RequestId = request.Id, Amount = 180m, ValidUntil = new DateOnly(2026, 6, 1) });

        // Act: race both accept calls simultaneously.
        // One should succeed; the other should either succeed (with the first declining
        // the other) or throw because the quote is already Declined.
        var t1 = Task.Run(() => svc.AcceptQuoteAsync(q1.Id).AsTask());
        var t2 = Task.Run(() => svc.AcceptQuoteAsync(q2.Id).AsTask());

        // We don't care which one throws, only that the outcome is consistent.
        try { await Task.WhenAll(t1, t2); } catch { /* at most one may throw */ }

        var allQuotes = await CollectAsync(svc.ListQuotesAsync(request.Id));
        var acceptedCount = allQuotes.Count(q => q.Status == QuoteStatus.Accepted);
        var declinedCount = allQuotes.Count(q => q.Status == QuoteStatus.Declined);

        // Invariant: exactly one accepted.
        Assert.Equal(1, acceptedCount);

        // The other must be Declined (or Submitted if one threw before completing).
        Assert.True(declinedCount >= 1, $"Expected at least one Declined quote, got {declinedCount}.");

        // Exactly one WorkOrder spawned.
        var workOrders = await CollectAsync(svc.ListWorkOrdersAsync(ListWorkOrdersQuery.Empty));
        Assert.Single(workOrders);
    }

    // ── WorkOrder lifecycle ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateWorkOrderAsync_CreatesWorkOrder_InDraftStatus()
    {
        var (svc, vendor) = await MakeServiceWithVendor();
        var (_, request) = await MakeServiceWithRequest();
        // Use a fresh svc that has both vendor and request... build manually.
        var svc2 = MakeService();
        var v = await svc2.CreateVendorAsync(new CreateVendorRequest { DisplayName = "Plumber", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing) });
        var r = await svc2.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId = TestPropertyId,
            RequestedByDisplayName = "Tenant",
            Description = "Fix pipe",
            Priority = MaintenancePriority.Normal,
            RequestedDate = new DateOnly(2026, 5, 1),
        });

        var wo = await svc2.CreateWorkOrderAsync(new CreateWorkOrderRequest
        {
            RequestId = r.Id,
            AssignedVendorId = v.Id,
            ScheduledDate = new DateOnly(2026, 5, 10),
            Tenant = TestTenant,
            EstimatedCost = Money.Usd(150m),
        });

        Assert.False(string.IsNullOrWhiteSpace(wo.Id.Value));
        Assert.Equal(WorkOrderStatus.Draft, wo.Status);
        Assert.Equal(Money.Usd(150m), wo.EstimatedCost);
        Assert.Null(wo.TotalCost);
        Assert.Null(wo.CompletedDate);
    }

    [Fact]
    public async Task TransitionWorkOrder_ValidPath_DraftToCompleted()
    {
        var svc = MakeService();
        var v = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Electrical) });
        var r = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId = TestPropertyId,
            RequestedByDisplayName = "T",
            Description = "Broken outlet",
            Priority = MaintenancePriority.Normal,
            RequestedDate = new DateOnly(2026, 5, 1),
        });
        var wo = await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        {
            RequestId = r.Id,
            AssignedVendorId = v.Id,
            ScheduledDate = new DateOnly(2026, 5, 10),
            Tenant = TestTenant,
            EstimatedCost = Money.Usd(75m),
        });

        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Sent);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Accepted);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Scheduled);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.InProgress);
        var completed = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Completed);

        Assert.Equal(WorkOrderStatus.Completed, completed.Status);
        Assert.NotNull(completed.CompletedDate);
    }

    [Fact]
    public async Task TransitionWorkOrder_OnHoldCycle()
    {
        var svc = MakeService();
        var v = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Painting) });
        var r = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId = TestPropertyId,
            RequestedByDisplayName = "T",
            Description = "Paint walls",
            Priority = MaintenancePriority.Low,
            RequestedDate = new DateOnly(2026, 5, 1),
        });
        var wo = await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        {
            RequestId = r.Id,
            AssignedVendorId = v.Id,
            ScheduledDate = new DateOnly(2026, 5, 10),
            Tenant = TestTenant,
            EstimatedCost = Money.Usd(300m),
        });

        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Sent);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Accepted);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Scheduled);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.InProgress);
        var onHold = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.OnHold);
        Assert.Equal(WorkOrderStatus.OnHold, onHold.Status);

        var resumed = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.InProgress);
        Assert.Equal(WorkOrderStatus.InProgress, resumed.Status);
    }

    [Fact]
    public async Task TransitionWorkOrder_InvalidTransition_ThrowsInvalidOperationException()
    {
        var svc = MakeService();
        var v = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Roofing) });
        var r = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId = TestPropertyId,
            RequestedByDisplayName = "T",
            Description = "Roof leak",
            Priority = MaintenancePriority.High,
            RequestedDate = new DateOnly(2026, 5, 1),
        });
        var wo = await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        {
            RequestId = r.Id,
            AssignedVendorId = v.Id,
            ScheduledDate = new DateOnly(2026, 5, 10),
            Tenant = TestTenant,
            EstimatedCost = Money.Usd(1500m),
        });

        // Cannot jump from Draft directly to Completed.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Completed).AsTask());
    }

    [Fact]
    public async Task GetWorkOrderAsync_UnknownId_ReturnsNull()
    {
        var svc = MakeService();

        var result = await svc.GetWorkOrderAsync(new WorkOrderId("no-such-wo"));

        Assert.Null(result);
    }

    // ─────────────────── Post-completion segment (ADR 0053 A4) ──────────────────

    private async Task<(InMemoryMaintenanceService Svc, WorkOrder Wo)> NewCompletedWorkOrderAsync()
    {
        var svc = MakeService();
        var v = await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "V", Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Plumbing) });
        var r = await svc.SubmitRequestAsync(new SubmitMaintenanceRequest
        {
            PropertyId = TestPropertyId,
            RequestedByDisplayName = "T",
            Description = "Leaky faucet",
            Priority = MaintenancePriority.Normal,
            RequestedDate = new DateOnly(2026, 5, 1),
        });
        var wo = await svc.CreateWorkOrderAsync(new CreateWorkOrderRequest
        {
            RequestId = r.Id,
            AssignedVendorId = v.Id,
            ScheduledDate = new DateOnly(2026, 5, 10),
            Tenant = TestTenant,
            EstimatedCost = Money.Usd(200m),
        });
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Sent);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Accepted);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Scheduled);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.InProgress);
        var completed = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Completed);
        return (svc, completed);
    }

    [Fact]
    public async Task TransitionWorkOrder_Completed_To_AwaitingSignOff_ToInvoiced_ToPaid_ToClosed()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();

        var awaiting = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.AwaitingSignOff);
        Assert.Equal(WorkOrderStatus.AwaitingSignOff, awaiting.Status);

        var invoiced = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Invoiced);
        Assert.Equal(WorkOrderStatus.Invoiced, invoiced.Status);

        var paid = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Paid);
        Assert.Equal(WorkOrderStatus.Paid, paid.Status);

        var closed = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Closed);
        Assert.Equal(WorkOrderStatus.Closed, closed.Status);
    }

    [Fact]
    public async Task TransitionWorkOrder_Completed_DirectlyToInvoiced_BypassesSignOff()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();

        var invoiced = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Invoiced);
        Assert.Equal(WorkOrderStatus.Invoiced, invoiced.Status);
    }

    [Fact]
    public async Task TransitionWorkOrder_AwaitingSignOff_To_OnHold()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.AwaitingSignOff);

        var onHold = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.OnHold);
        Assert.Equal(WorkOrderStatus.OnHold, onHold.Status);
    }

    [Fact]
    public async Task TransitionWorkOrder_Invoiced_To_Disputed_ToPaid_ToClosed()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Invoiced);

        var disputed = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Disputed);
        Assert.Equal(WorkOrderStatus.Disputed, disputed.Status);

        var paid = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Paid);
        Assert.Equal(WorkOrderStatus.Paid, paid.Status);

        var closed = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Closed);
        Assert.Equal(WorkOrderStatus.Closed, closed.Status);
    }

    [Fact]
    public async Task TransitionWorkOrder_Paid_To_Disputed_ResolvesViaInvoiced_ThenClosed()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Invoiced);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Paid);

        var disputed = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Disputed);
        Assert.Equal(WorkOrderStatus.Disputed, disputed.Status);

        var reInvoiced = await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Invoiced);
        Assert.Equal(WorkOrderStatus.Invoiced, reInvoiced.Status);
    }

    [Fact]
    public async Task TransitionWorkOrder_Closed_IsTerminal()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Invoiced);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Paid);
        await svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Closed);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.TransitionWorkOrderAsync(wo.Id, WorkOrderStatus.Disputed).AsTask());
    }

    // ─────────── Child entities (W#19 Phase 3 / ADR 0053) ───────────

    private static readonly ActorId Operator = new("operator");

    [Fact]
    public async Task RecordEntryNotice_PersistsNotice()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        var notice = new WorkOrderEntryNotice
        {
            Id = WorkOrderEntryNoticeId.NewId(),
            WorkOrder = wo.Id,
            PlannedEntryUtc = DateTimeOffset.UtcNow.AddDays(2),
            EntryReason = "Plumbing repair",
            NotifiedBy = Operator,
            NotifiedAt = DateTimeOffset.UtcNow,
            NotifiedParties = new[] { new ActorId("tenant-1") },
        };
        var saved = await svc.RecordEntryNoticeAsync(notice, Operator);
        Assert.Equal(notice.Id, saved.Id);
        Assert.Equal("Plumbing repair", saved.EntryReason);
    }

    [Fact]
    public async Task RecordEntryNotice_RejectsUnknownWorkOrder()
    {
        var svc = MakeService();
        var notice = new WorkOrderEntryNotice
        {
            Id = WorkOrderEntryNoticeId.NewId(),
            WorkOrder = new WorkOrderId("no-such-wo"),
            PlannedEntryUtc = DateTimeOffset.UtcNow,
            EntryReason = "x",
            NotifiedBy = Operator,
            NotifiedAt = DateTimeOffset.UtcNow,
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RecordEntryNoticeAsync(notice, Operator).AsTask());
    }

    [Fact]
    public async Task RecordEntryNotice_RejectsDuplicate()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        var notice = new WorkOrderEntryNotice
        {
            Id = WorkOrderEntryNoticeId.NewId(),
            WorkOrder = wo.Id,
            PlannedEntryUtc = DateTimeOffset.UtcNow,
            EntryReason = "x",
            NotifiedBy = Operator,
            NotifiedAt = DateTimeOffset.UtcNow,
        };
        await svc.RecordEntryNoticeAsync(notice, Operator);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RecordEntryNoticeAsync(notice, Operator).AsTask());
    }

    [Fact]
    public async Task RecordEntryNotice_NotifiedParties_DefaultsToEmpty()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        var notice = new WorkOrderEntryNotice
        {
            Id = WorkOrderEntryNoticeId.NewId(),
            WorkOrder = wo.Id,
            PlannedEntryUtc = DateTimeOffset.UtcNow,
            EntryReason = "x",
            NotifiedBy = Operator,
            NotifiedAt = DateTimeOffset.UtcNow,
        };
        var saved = await svc.RecordEntryNoticeAsync(notice, Operator);
        Assert.Empty(saved.NotifiedParties);
    }

    [Fact]
    public async Task CaptureCompletionAttestation_PersistsAttestation()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        var a = new WorkOrderCompletionAttestation
        {
            Id = WorkOrderCompletionAttestationId.NewId(),
            WorkOrder = wo.Id,
            Signature = new Sunfish.Foundation.Integrations.Signatures.SignatureEventRef(Guid.NewGuid()),
            AttestedAt = DateTimeOffset.UtcNow,
            Attestor = Operator,
            AttestationNotes = "All good",
        };
        var saved = await svc.CaptureCompletionAttestationAsync(a, Operator);
        Assert.Equal(a.Id, saved.Id);
        Assert.Equal("All good", saved.AttestationNotes);
    }

    [Fact]
    public async Task CaptureCompletionAttestation_RejectsUnknownWorkOrder()
    {
        var svc = MakeService();
        var a = new WorkOrderCompletionAttestation
        {
            Id = WorkOrderCompletionAttestationId.NewId(),
            WorkOrder = new WorkOrderId("no-such-wo"),
            Signature = new Sunfish.Foundation.Integrations.Signatures.SignatureEventRef(Guid.NewGuid()),
            AttestedAt = DateTimeOffset.UtcNow,
            Attestor = Operator,
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CaptureCompletionAttestationAsync(a, Operator).AsTask());
    }

    [Fact]
    public async Task CaptureCompletionAttestation_RejectsDuplicate()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        var a = new WorkOrderCompletionAttestation
        {
            Id = WorkOrderCompletionAttestationId.NewId(),
            WorkOrder = wo.Id,
            Signature = new Sunfish.Foundation.Integrations.Signatures.SignatureEventRef(Guid.NewGuid()),
            AttestedAt = DateTimeOffset.UtcNow,
            Attestor = Operator,
        };
        await svc.CaptureCompletionAttestationAsync(a, Operator);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CaptureCompletionAttestationAsync(a, Operator).AsTask());
    }

    [Fact]
    public async Task ProposeAppointment_PersistsProposed()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        var slotStart = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var appt = new WorkOrderAppointment
        {
            Id = WorkOrderAppointmentId.NewId(),
            WorkOrder = wo.Id,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotStart.AddHours(2),
            Status = AppointmentStatus.Proposed,
            ProposedBy = Operator,
        };
        var saved = await svc.ProposeAppointmentAsync(appt, Operator);
        Assert.Equal(AppointmentStatus.Proposed, saved.Status);
    }

    [Fact]
    public async Task ProposeAppointment_RejectsOverlappingSlot()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        var slotStart = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

        await svc.ProposeAppointmentAsync(new WorkOrderAppointment
        {
            Id = WorkOrderAppointmentId.NewId(),
            WorkOrder = wo.Id,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotStart.AddHours(2),
            Status = AppointmentStatus.Proposed,
            ProposedBy = Operator,
        }, Operator);

        var overlapping = new WorkOrderAppointment
        {
            Id = WorkOrderAppointmentId.NewId(),
            WorkOrder = wo.Id,
            SlotStartUtc = slotStart.AddHours(1), // overlaps
            SlotEndUtc = slotStart.AddHours(3),
            Status = AppointmentStatus.Proposed,
            ProposedBy = Operator,
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ProposeAppointmentAsync(overlapping, Operator).AsTask());
    }

    [Fact]
    public async Task ConfirmAppointment_FlipsStatus()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        var slotStart = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var proposed = await svc.ProposeAppointmentAsync(new WorkOrderAppointment
        {
            Id = WorkOrderAppointmentId.NewId(),
            WorkOrder = wo.Id,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotStart.AddHours(2),
            Status = AppointmentStatus.Proposed,
            ProposedBy = Operator,
        }, Operator);

        var confirmer = new ActorId("vendor-1");
        var confirmed = await svc.ConfirmAppointmentAsync(proposed.Id, confirmer);

        Assert.Equal(AppointmentStatus.Confirmed, confirmed.Status);
        Assert.Equal(confirmer, confirmed.ConfirmedBy);
        Assert.NotNull(confirmed.ConfirmedAt);
    }

    [Fact]
    public async Task ConfirmAppointment_RejectsUnknownAppointment()
    {
        var svc = MakeService();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ConfirmAppointmentAsync(WorkOrderAppointmentId.NewId(), Operator).AsTask());
    }

    [Fact]
    public async Task ConfirmAppointment_RejectsDoubleConfirmation()
    {
        var (svc, wo) = await NewCompletedWorkOrderAsync();
        var slotStart = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);
        var proposed = await svc.ProposeAppointmentAsync(new WorkOrderAppointment
        {
            Id = WorkOrderAppointmentId.NewId(),
            WorkOrder = wo.Id,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotStart.AddHours(2),
            Status = AppointmentStatus.Proposed,
            ProposedBy = Operator,
        }, Operator);
        await svc.ConfirmAppointmentAsync(proposed.Id, Operator);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.ConfirmAppointmentAsync(proposed.Id, Operator).AsTask());
    }
}
