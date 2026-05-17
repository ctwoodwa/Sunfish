using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialAr.Tests;

public class InvoiceRepositoryValidationTests
{
    private static TenantId Tenant() => new("acme");

    private static Invoice NewInvoice(string number, InvoiceStatus status) =>
        Invoice.Create(
            tenantId: Tenant(),
            chartId: ChartOfAccountsId.NewId(),
            invoiceNumber: number,
            customerId: PartyId.NewId(),
            issueDate: new DateOnly(2026, 5, 17),
            dueDate: new DateOnly(2026, 6, 17),
            lines: Array.Empty<InvoiceLine>(),
            arAccountId: GLAccountId.NewId())
        with { Status = status };

    [Fact]
    public async Task Upsert_AllowsDraftInvoice_WithEmptyNumber()
    {
        var repo = new InMemoryInvoiceRepository();
        var draft = NewInvoice(number: "", InvoiceStatus.Draft);
        await repo.UpsertAsync(draft); // does not throw
        var fetched = await repo.GetAsync(draft.Id);
        Assert.NotNull(fetched);
    }

    [Theory]
    [InlineData("")]
    [InlineData("INV-001")]              // missing date + replica components
    [InlineData("INV-2026-5-17-CW-0001")] // unpadded month
    [InlineData("invoice-2026-05-17-CW-0001")]
    public async Task Upsert_RejectsIssuedInvoice_WithoutValidNumber(string badNumber)
    {
        var repo = new InMemoryInvoiceRepository();
        var issued = NewInvoice(badNumber, InvoiceStatus.Issued);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UpsertAsync(issued));
    }

    [Theory]
    [InlineData(InvoiceStatus.Issued)]
    [InlineData(InvoiceStatus.PartiallyPaid)]
    [InlineData(InvoiceStatus.Paid)]
    [InlineData(InvoiceStatus.Voided)]
    [InlineData(InvoiceStatus.WrittenOff)]
    public async Task Upsert_AcceptsNonDraftInvoice_WithCanonicalNumber(InvoiceStatus status)
    {
        var repo = new InMemoryInvoiceRepository();
        var inv = NewInvoice("INV-2026-05-17-CW-0001", status);
        await repo.UpsertAsync(inv);
        var fetched = await repo.GetAsync(inv.Id);
        Assert.NotNull(fetched);
    }
}
