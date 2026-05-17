using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Models.Events;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;
using Xunit;

namespace Sunfish.Blocks.FinancialAr.Tests;

public class InvoicePostingServiceTests
{
    // ── Test fixtures ─────────────────────────────────────────────────

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

    private sealed class FixedTaxCalculator : ITaxCalculator
    {
        private readonly decimal _rate;
        public FixedTaxCalculator(decimal rate) { _rate = rate; }
        public Task<decimal> CalculateAsync(string? taxCodeId, decimal taxableBase, DateOnly transactionDate, CancellationToken cancellationToken = default)
            => Task.FromResult(string.IsNullOrEmpty(taxCodeId) ? 0m : Math.Round(taxableBase * _rate, 2, MidpointRounding.ToEven));
    }

    private static TenantId Tenant() => new("acme");
    private static PartyId Actor() => PartyId.NewId();

    private sealed record Sut(
        InvoicePostingService Service,
        InMemoryInvoiceRepository Repo,
        FakeJournalPostingService Journals,
        RecordingPublisher Events);

    private static Sut NewSut(decimal taxRate = 0m)
    {
        var repo = new InMemoryInvoiceRepository();
        var numbering = new InMemoryInvoiceNumberingService(new ReplicaId("CW"));
        var journals = new FakeJournalPostingService();
        var events = new RecordingPublisher();
        var tax = new FixedTaxCalculator(taxRate);
        var svc = new InvoicePostingService(repo, numbering, tax, journals, events);
        return new Sut(svc, repo, journals, events);
    }

    private static async Task<Invoice> SeedDraftAsync(
        InMemoryInvoiceRepository repo,
        decimal lineAmount = 100m,
        int lineCount = 1,
        string? taxCodeId = null)
    {
        var invId = InvoiceId.NewId();
        var lines = new List<InvoiceLine>();
        for (int i = 1; i <= lineCount; i++)
            lines.Add(InvoiceLine.Create(invId, i, $"Line {i}", 1m, lineAmount, GLAccountId.NewId(), taxCodeId));

        var inv = Invoice.Create(
            tenantId: Tenant(),
            chartId: ChartOfAccountsId.NewId(),
            invoiceNumber: "",
            customerId: PartyId.NewId(),
            issueDate: new DateOnly(2026, 5, 17),
            dueDate: new DateOnly(2026, 6, 17),
            lines: lines,
            arAccountId: GLAccountId.NewId(),
            id: invId);
        await repo.UpsertAsync(inv);
        return inv;
    }

    // ── IssueAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Issue_UnknownInvoice_ReturnsError()
    {
        var sut = NewSut();
        var result = await sut.Service.IssueAsync(InvoiceId.NewId(), Actor());
        Assert.False(result.IsSuccess);
        Assert.Equal(IssueError.UnknownInvoice, result.Error);
    }

    [Fact]
    public async Task Issue_HappyPath_TransitionsToIssued_AndPostsBalancedJE()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo, lineAmount: 100m);

        var result = await sut.Service.IssueAsync(draft.Id, Actor());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Invoice);
        Assert.Equal(InvoiceStatus.Issued, result.Invoice!.Status);
        Assert.NotNull(result.Invoice.JournalEntryId);
        Assert.Equal(100m, result.Invoice.Total);

        // Journal entry was posted via the fake.
        Assert.Single(sut.Journals.Posted);
        var je = sut.Journals.Posted[0];
        // Balance invariant — enforced by JournalEntry ctor, but verify just in case.
        Assert.Equal(je.Lines.Sum(l => l.Debit), je.Lines.Sum(l => l.Credit));
    }

    [Fact]
    public async Task Issue_MintsInvoiceNumber_WhenDraftBlank()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        Assert.Equal("", draft.InvoiceNumber);

        var result = await sut.Service.IssueAsync(draft.Id, Actor());

        Assert.True(InvoiceNumberFormat.IsWellFormed(result.Invoice!.InvoiceNumber));
        Assert.Contains("CW-0001", result.Invoice.InvoiceNumber);
    }

    [Fact]
    public async Task Issue_AlreadyIssued_IsIdempotent_NoSecondJE()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        var first = await sut.Service.IssueAsync(draft.Id, Actor());
        sut.Events.Events.Clear();
        var initialJeCount = sut.Journals.Posted.Count;

        var second = await sut.Service.IssueAsync(draft.Id, Actor());

        Assert.True(second.IsSuccess);
        Assert.Equal(first.PostedEntryId, second.PostedEntryId);
        Assert.Equal(initialJeCount, sut.Journals.Posted.Count); // no re-post
        Assert.Empty(sut.Events.Events); // no duplicate event
    }

    [Fact]
    public async Task Issue_NoLines_ReturnsError()
    {
        var sut = NewSut();
        var inv = Invoice.Create(
            tenantId: Tenant(), chartId: ChartOfAccountsId.NewId(), invoiceNumber: "",
            customerId: PartyId.NewId(), issueDate: new DateOnly(2026, 5, 17),
            dueDate: new DateOnly(2026, 6, 17), lines: Array.Empty<InvoiceLine>(),
            arAccountId: GLAccountId.NewId());
        await sut.Repo.UpsertAsync(inv);

        var result = await sut.Service.IssueAsync(inv.Id, Actor());
        Assert.Equal(IssueError.NoLines, result.Error);
    }

    [Fact]
    public async Task Issue_TerminalStatus_RejectedWithInvalidStatus()
    {
        // Build an invoice already in Voided status (skip the natural transition for test setup).
        var sut = NewSut();
        var inv = Invoice.Create(
            tenantId: Tenant(), chartId: ChartOfAccountsId.NewId(),
            invoiceNumber: "INV-2026-05-17-CW-9999",
            customerId: PartyId.NewId(), issueDate: new DateOnly(2026, 5, 17),
            dueDate: new DateOnly(2026, 6, 17),
            lines: new[] { InvoiceLine.Create(InvoiceId.NewId(), 1, "x", 1m, 100m, GLAccountId.NewId()) },
            arAccountId: GLAccountId.NewId());
        var voided = inv with { Status = InvoiceStatus.Voided };
        await sut.Repo.UpsertAsync(voided);

        var result = await sut.Service.IssueAsync(voided.Id, Actor());
        Assert.Equal(IssueError.InvalidStatusForIssue, result.Error);
    }

    [Fact]
    public async Task Issue_EmitsInvoiceIssuedEvent_WithIdempotencyKey()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo, lineAmount: 150m);

        await sut.Service.IssueAsync(draft.Id, Actor());

        var ev = Assert.Single(sut.Events.Events);
        Assert.Equal(AccountsReceivableEventNames.InvoiceIssued, ev.Type);
        Assert.Equal($"invoice-issued:{draft.Id.Value}", ev.IdempotencyKey);
        var payload = Assert.IsType<InvoiceIssuedPayload>(ev.Payload);
        Assert.Equal(150m, payload.TotalAmount);
    }

    [Fact]
    public async Task Issue_JournalRejection_Propagates_AndDoesNotMutateInvoice()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        sut.Journals.NextError = PostError.Imbalanced;
        sut.Journals.NextDetail = "test-induced imbalance";

        var result = await sut.Service.IssueAsync(draft.Id, Actor());

        Assert.False(result.IsSuccess);
        Assert.Equal(IssueError.JournalRejected, result.Error);
        Assert.Equal("test-induced imbalance", result.Detail);

        // Invoice should remain Draft.
        var refetched = await sut.Repo.GetAsync(draft.Id);
        Assert.Equal(InvoiceStatus.Draft, refetched!.Status);
        Assert.Null(refetched.JournalEntryId);
    }

    // ── VoidAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Void_HappyPath_TransitionsToVoided_PostsReversal_EmitsEvent()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.IssueAsync(draft.Id, Actor());
        sut.Events.Events.Clear();

        var result = await sut.Service.VoidAsync(draft.Id, "customer dispute", Actor());

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Voided, result.Invoice!.Status);
        Assert.NotNull(result.Invoice.VoidedByEntryId);
        Assert.Equal(0m, result.Invoice.Balance);

        var ev = Assert.Single(sut.Events.Events);
        Assert.Equal(AccountsReceivableEventNames.InvoiceVoided, ev.Type);
        var payload = Assert.IsType<InvoiceVoidedPayload>(ev.Payload);
        Assert.Equal("customer dispute", payload.Reason);
    }

    [Fact]
    public async Task Void_DraftStatus_RejectedWithInvalidStatus()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);

        var result = await sut.Service.VoidAsync(draft.Id, "no", Actor());
        Assert.Equal(VoidError.InvalidStatusForVoid, result.Error);
    }

    [Fact]
    public async Task Void_UnknownInvoice_ReturnsError()
    {
        var sut = NewSut();
        var result = await sut.Service.VoidAsync(InvoiceId.NewId(), "nope", Actor());
        Assert.Equal(VoidError.UnknownInvoice, result.Error);
    }

    [Fact]
    public async Task Void_AlreadyVoided_RejectedWithInvalidStatus()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.IssueAsync(draft.Id, Actor());
        await sut.Service.VoidAsync(draft.Id, "first", Actor());

        var result = await sut.Service.VoidAsync(draft.Id, "second", Actor());
        Assert.Equal(VoidError.InvalidStatusForVoid, result.Error);
    }

    // ── WriteOffAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task WriteOff_HappyPath_TransitionsToWrittenOff_PostsBadDebt_EmitsEvent()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo, lineAmount: 200m);
        await sut.Service.IssueAsync(draft.Id, Actor());
        sut.Events.Events.Clear();
        var badDebt = GLAccountId.NewId();

        var result = await sut.Service.WriteOffAsync(draft.Id, badDebt, "uncollectable", Actor());

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.WrittenOff, result.Invoice!.Status);
        Assert.NotNull(result.Invoice.WrittenOffByEntryId);
        Assert.Equal(0m, result.Invoice.Balance);

        var ev = Assert.Single(sut.Events.Events);
        Assert.Equal(AccountsReceivableEventNames.InvoiceWrittenOff, ev.Type);
        var payload = Assert.IsType<InvoiceWrittenOffPayload>(ev.Payload);
        Assert.Equal(200m, payload.AmountWrittenOff);
    }

    [Fact]
    public async Task WriteOff_EmptyBadDebtAccount_ReturnsInvalidBadDebtAccount()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.IssueAsync(draft.Id, Actor());

        var result = await sut.Service.WriteOffAsync(draft.Id, new GLAccountId(""), "no", Actor());
        Assert.Equal(WriteOffError.InvalidBadDebtAccount, result.Error);
    }

    [Fact]
    public async Task WriteOff_DraftStatus_RejectedWithInvalidStatus()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);

        var result = await sut.Service.WriteOffAsync(draft.Id, GLAccountId.NewId(), "n/a", Actor());
        Assert.Equal(WriteOffError.InvalidStatusForWriteOff, result.Error);
    }

    [Fact]
    public async Task WriteOff_UnknownInvoice_ReturnsError()
    {
        var sut = NewSut();
        var result = await sut.Service.WriteOffAsync(InvoiceId.NewId(), GLAccountId.NewId(), "n/a", Actor());
        Assert.Equal(WriteOffError.UnknownInvoice, result.Error);
    }

    // ── Cross-cutting ─────────────────────────────────────────────────

    [Fact]
    public async Task AllEventTypes_StartWithFinancialPrefix()
    {
        var sut = NewSut();
        var draft = await SeedDraftAsync(sut.Repo);
        await sut.Service.IssueAsync(draft.Id, Actor());
        await sut.Service.VoidAsync(draft.Id, "test", Actor());

        var draft2 = await SeedDraftAsync(sut.Repo);
        await sut.Service.IssueAsync(draft2.Id, Actor());
        await sut.Service.WriteOffAsync(draft2.Id, GLAccountId.NewId(), "test", Actor());

        Assert.All(sut.Events.Events, e => Assert.StartsWith("Financial.", e.Type, StringComparison.Ordinal));
    }
}
