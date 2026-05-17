using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Models.Events;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;
using Xunit;

namespace Sunfish.Blocks.FinancialAp.Tests;

public class BillPostingServiceTests
{
    private sealed class FakeJournalPostingService : IJournalPostingService
    {
        public List<JournalEntry> Posted { get; } = new();
        public PostError NextError { get; set; } = PostError.None;
        public string? NextDetail { get; set; }

        public Task<PostResult> PostAsync(JournalEntry entry, CancellationToken cancellationToken = default)
        {
            if (NextError != PostError.None)
                return Task.FromResult(new PostResult(null, NextError, NextDetail));
            Posted.Add(entry);
            return Task.FromResult(new PostResult(entry, PostError.None, null));
        }
    }

    private sealed class RecordingPublisher : IDomainEventPublisher
    {
        public List<(string Type, string IdempotencyKey, object Payload)> Events { get; } = new();
        public Task PublishAsync<TPayload>(DomainEventEnvelope<TPayload> envelope, CancellationToken cancellationToken = default)
        {
            Events.Add((envelope.EventType, envelope.IdempotencyKey, envelope.Payload!));
            return Task.CompletedTask;
        }
    }

    private static TenantId Tenant() => new("acme");
    private static PartyId Actor() => PartyId.NewId();

    private sealed record Sut(
        BillPostingService Service,
        InMemoryBillRepository Repo,
        FakeJournalPostingService Journals,
        RecordingPublisher Events);

    private static Sut NewSut()
    {
        var repo = new InMemoryBillRepository();
        var journals = new FakeJournalPostingService();
        var events = new RecordingPublisher();
        var svc = new BillPostingService(repo, new NoOpTaxCalculator(), journals, events);
        return new Sut(svc, repo, journals, events);
    }

    private static async Task<Bill> SeedDraftAsync(
        InMemoryBillRepository repo,
        decimal lineAmount = 100m,
        int lineCount = 1)
    {
        var billId = BillId.NewId();
        var lines = new List<BillLine>();
        for (int i = 1; i <= lineCount; i++)
            lines.Add(BillLine.Create(billId, i, $"Line {i}", 1m, lineAmount, GLAccountId.NewId()));

        var bill = Bill.Create(
            tenantId: Tenant(),
            chartId: ChartOfAccountsId.NewId(),
            billNumber: "VND-001",
            vendorId: PartyId.NewId(),
            billDate: new DateOnly(2026, 5, 17),
            dueDate: new DateOnly(2026, 6, 17),
            lines: lines,
            apAccountId: GLAccountId.NewId(),
            id: billId);
        await repo.UpsertAsync(bill);
        return bill;
    }

    // ── RecordAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task Record_UnknownBill_ReturnsError()
    {
        var sut = NewSut();
        var result = await sut.Service.RecordAsync(BillId.NewId(), Actor());
        Assert.Equal(RecordError.UnknownBill, result.Error);
    }

    [Fact]
    public async Task Record_HappyPath_TransitionsToReceived_PostsBalancedJE()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo, lineAmount: 100m);

        var result = await sut.Service.RecordAsync(draft.Id, Actor());

        Assert.True(result.IsSuccess);
        Assert.Equal(BillStatus.Received, result.Bill!.Status);
        Assert.NotNull(result.Bill.JournalEntryId);
        Assert.Equal(100m, result.Bill.Total);

        Assert.Single(sut.Journals.Posted);
        var je = sut.Journals.Posted[0];
        Assert.Equal(je.Lines.Sum(l => l.Debit), je.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task Record_AlreadyReceived_IsIdempotent_NoSecondJE()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        var first = await sut.Service.RecordAsync(draft.Id, Actor());
        sut.Events.Events.Clear();
        var initialJeCount = sut.Journals.Posted.Count;

        var second = await sut.Service.RecordAsync(draft.Id, Actor());

        Assert.True(second.IsSuccess);
        Assert.Equal(first.PostedEntryId, second.PostedEntryId);
        Assert.Equal(initialJeCount, sut.Journals.Posted.Count);
        Assert.Empty(sut.Events.Events);
    }

    [Fact]
    public async Task Record_NoLines_ReturnsError()
    {
        var sut = NewSut();
        var bill = Bill.Create(
            tenantId: Tenant(), chartId: ChartOfAccountsId.NewId(), billNumber: "VND-EMPTY",
            vendorId: PartyId.NewId(), billDate: new DateOnly(2026, 5, 17),
            dueDate: new DateOnly(2026, 6, 17), lines: Array.Empty<BillLine>(),
            apAccountId: GLAccountId.NewId());
        await sut.Repo.UpsertAsync(bill);

        var result = await sut.Service.RecordAsync(bill.Id, Actor());
        Assert.Equal(RecordError.NoLines, result.Error);
    }

    [Fact]
    public async Task Record_NonDraft_RejectedWithInvalidStatus()
    {
        var sut = NewSut();
        var bill = Bill.Create(
            tenantId: Tenant(), chartId: ChartOfAccountsId.NewId(), billNumber: "VND-DONE",
            vendorId: PartyId.NewId(), billDate: new DateOnly(2026, 5, 17),
            dueDate: new DateOnly(2026, 6, 17),
            lines: new[] { BillLine.Create(BillId.NewId(), 1, "x", 1m, 100m, GLAccountId.NewId()) },
            apAccountId: GLAccountId.NewId());
        var voided = bill with { Status = BillStatus.Voided };
        await sut.Repo.UpsertAsync(voided);

        var result = await sut.Service.RecordAsync(voided.Id, Actor());
        Assert.Equal(RecordError.InvalidStatusForRecord, result.Error);
    }

    [Fact]
    public async Task Record_EmitsBillRecordedEvent()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo, lineAmount: 150m);

        await sut.Service.RecordAsync(draft.Id, Actor());

        var ev = Assert.Single(sut.Events.Events);
        Assert.Equal(AccountsPayableEventNames.BillRecorded, ev.Type);
        Assert.Equal($"bill-recorded:{draft.Id.Value}", ev.IdempotencyKey);
        var payload = Assert.IsType<BillRecordedPayload>(ev.Payload);
        Assert.Equal(150m, payload.TotalAmount);
    }

    [Fact]
    public async Task Record_JournalRejection_DoesNotMutateBill()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        sut.Journals.NextError = PostError.Imbalanced;
        sut.Journals.NextDetail = "test-induced imbalance";

        var result = await sut.Service.RecordAsync(draft.Id, Actor());

        Assert.False(result.IsSuccess);
        Assert.Equal(RecordError.JournalRejected, result.Error);
        var refetched = await sut.Repo.GetAsync(draft.Id);
        Assert.Equal(BillStatus.Draft, refetched!.Status);
        Assert.Null(refetched.JournalEntryId);
    }

    // ── VoidAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Void_HappyPath_TransitionsToVoided_EmitsEvent()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.RecordAsync(draft.Id, Actor());
        sut.Events.Events.Clear();

        var result = await sut.Service.VoidAsync(draft.Id, "vendor returned product", Actor());

        Assert.True(result.IsSuccess);
        Assert.Equal(BillStatus.Voided, result.Bill!.Status);
        Assert.NotNull(result.Bill.VoidedByEntryId);
        Assert.Equal(0m, result.Bill.Balance);

        var ev = Assert.Single(sut.Events.Events);
        Assert.Equal(AccountsPayableEventNames.BillVoided, ev.Type);
    }

    [Fact]
    public async Task Void_DraftBill_RejectedWithInvalidStatus()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        var result = await sut.Service.VoidAsync(draft.Id, "x", Actor());
        Assert.Equal(VoidError.InvalidStatusForVoid, result.Error);
    }

    // ── ApproveAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task Approve_HappyPath_StampsApprovalAndEmitsEvent()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.RecordAsync(draft.Id, Actor());
        sut.Events.Events.Clear();

        var result = await sut.Service.ApproveAsync(draft.Id, "alice@example.com", Actor());

        Assert.True(result.IsSuccess);
        Assert.Equal(BillStatus.Approved, result.Bill!.Status);
        Assert.Equal("alice@example.com", result.Bill.ApprovedByUserId);
        Assert.NotNull(result.Bill.ApprovedAtUtc);

        var ev = Assert.Single(sut.Events.Events);
        Assert.Equal(AccountsPayableEventNames.BillApproved, ev.Type);
    }

    [Fact]
    public async Task Approve_DraftBill_RejectedWithInvalidStatus()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        var result = await sut.Service.ApproveAsync(draft.Id, "alice@example.com", Actor());
        Assert.Equal(ApproveError.InvalidStatusForApproval, result.Error);
    }

    [Fact]
    public async Task Approve_EmptyApproverId_Rejected()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.RecordAsync(draft.Id, Actor());
        var result = await sut.Service.ApproveAsync(draft.Id, "", Actor());
        Assert.Equal(ApproveError.InvalidApproverId, result.Error);
    }

    // ── DisputeAsync / ResolveDisputeAsync ────────────────────────────

    [Fact]
    public async Task Dispute_HappyPath_TransitionsToDisputed_EmitsEvent_NoGLImpact()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.RecordAsync(draft.Id, Actor());
        var jeCountBefore = sut.Journals.Posted.Count;
        sut.Events.Events.Clear();

        var result = await sut.Service.DisputeAsync(draft.Id, "quantity mismatch", Actor());

        Assert.True(result.IsSuccess);
        Assert.Equal(BillStatus.Disputed, result.Bill!.Status);
        // GL untouched — dispute is a hold, not a reversal.
        Assert.Equal(jeCountBefore, sut.Journals.Posted.Count);

        var ev = Assert.Single(sut.Events.Events);
        Assert.Equal(AccountsPayableEventNames.BillDisputed, ev.Type);
    }

    [Fact]
    public async Task Dispute_DraftBill_RejectedWithInvalidStatus()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        var result = await sut.Service.DisputeAsync(draft.Id, "x", Actor());
        Assert.Equal(DisputeError.InvalidStatusForDispute, result.Error);
    }

    [Fact]
    public async Task ResolveDispute_HappyPath_TransitionsBackToReceived_EmitsEvent()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.RecordAsync(draft.Id, Actor());
        await sut.Service.DisputeAsync(draft.Id, "quantity mismatch", Actor());
        sut.Events.Events.Clear();

        var result = await sut.Service.ResolveDisputeAsync(draft.Id, BillStatus.Received, Actor());

        Assert.True(result.IsSuccess);
        Assert.Equal(BillStatus.Received, result.Bill!.Status);

        var ev = Assert.Single(sut.Events.Events);
        Assert.Equal(AccountsPayableEventNames.DisputeResolved, ev.Type);
        var payload = Assert.IsType<DisputeResolvedPayload>(ev.Payload);
        Assert.Equal(BillStatus.Received, payload.ResolvedTo);
    }

    [Fact]
    public async Task ResolveDispute_ToApproved_AlsoAllowed()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.RecordAsync(draft.Id, Actor());
        await sut.Service.DisputeAsync(draft.Id, "x", Actor());

        var result = await sut.Service.ResolveDisputeAsync(draft.Id, BillStatus.Approved, Actor());
        Assert.True(result.IsSuccess);
        Assert.Equal(BillStatus.Approved, result.Bill!.Status);
    }

    [Theory]
    [InlineData(BillStatus.Draft)]
    [InlineData(BillStatus.PartiallyPaid)]
    [InlineData(BillStatus.Paid)]
    [InlineData(BillStatus.Voided)]
    [InlineData(BillStatus.Disputed)]
    public async Task ResolveDispute_ToNonPayableState_RejectedWithInvalidResolutionTarget(BillStatus target)
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.RecordAsync(draft.Id, Actor());
        await sut.Service.DisputeAsync(draft.Id, "x", Actor());

        var result = await sut.Service.ResolveDisputeAsync(draft.Id, target, Actor());
        Assert.Equal(ResolveDisputeError.InvalidResolutionTarget, result.Error);
    }

    [Fact]
    public async Task ResolveDispute_OnNonDisputedBill_RejectedWithInvalidStatus()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.RecordAsync(draft.Id, Actor());
        // not disputed yet
        var result = await sut.Service.ResolveDisputeAsync(draft.Id, BillStatus.Received, Actor());
        Assert.Equal(ResolveDisputeError.InvalidStatusForResolve, result.Error);
    }

    // ── Cross-cutting ─────────────────────────────────────────────────

    [Fact]
    public async Task AllEventTypes_StartWithFinancialPrefix()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.RecordAsync(draft.Id, Actor());
        await sut.Service.ApproveAsync(draft.Id, "alice@example.com", Actor());
        await sut.Service.DisputeAsync(draft.Id, "x", Actor());
        await sut.Service.ResolveDisputeAsync(draft.Id, BillStatus.Received, Actor());
        await sut.Service.VoidAsync(draft.Id, "x", Actor());

        Assert.All(sut.Events.Events, e => Assert.StartsWith("Financial.", e.Type, StringComparison.Ordinal));
    }
}
