using TaxCodeId = Sunfish.Blocks.FinancialTax.Models.TaxCodeId;
using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Models.Events;
using Sunfish.Blocks.FinancialTax.Services;
using Xunit;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialTax.Tests;

/// <summary>
/// PR 5 coverage for event emission across the three writable stores.
/// Each test installs a <see cref="RecordingDomainEventPublisher"/>
/// in place of the default <see cref="NoopDomainEventPublisher"/>
/// and asserts the captured envelope shape.
/// </summary>
public class EventEmissionTests
{
    private static DateOnly D(int y, int m, int d) => new DateOnly(y, m, d);

    private sealed class RecordingDomainEventPublisher : IDomainEventPublisher
    {
        public List<(string EventType, object Payload, string IdempotencyKey, string EventId, string? CausationId)> Captured { get; } = new();

        public Task PublishAsync<TPayload>(
            DomainEventEnvelope<TPayload> envelope,
            CancellationToken cancellationToken = default)
        {
            Captured.Add((envelope.EventType, envelope.Payload!, envelope.IdempotencyKey, envelope.EventId, envelope.CausationId));
            return Task.CompletedTask;
        }
    }

    // ── TaxCodeStore ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertTaxCode_FirstInsert_Emits_TaxCodeAddedEvent()
    {
        var pub = new RecordingDomainEventPublisher();
        var store = new InMemoryTaxCodeStore(pub);
        var code = TaxCode.Create(FL.ChartOfAccountsId.NewId(), "US-VA-SALES", "VA Sales",
            TaxKind.Sales, TaxApplication.OnSubtotal);

        await store.UpsertAsync(code);

        Assert.Single(pub.Captured);
        Assert.Equal(FinancialTaxEventNames.TaxCodeAdded, pub.Captured[0].EventType);
        Assert.IsType<TaxCodeAdded>(pub.Captured[0].Payload);
    }

    [Fact]
    public async Task UpsertTaxCode_VersionBump_Emits_TaxCodeUpdatedEvent()
    {
        var pub = new RecordingDomainEventPublisher();
        var store = new InMemoryTaxCodeStore(pub);
        var code = TaxCode.Create(FL.ChartOfAccountsId.NewId(), "US-VA-SALES", "VA Sales",
            TaxKind.Sales, TaxApplication.OnSubtotal);
        await store.UpsertAsync(code);
        pub.Captured.Clear();

        // Edit + re-upsert.
        var edited = code with { Name = "Virginia sales (edited)" };
        await store.UpsertAsync(edited);

        Assert.Single(pub.Captured);
        Assert.Equal(FinancialTaxEventNames.TaxCodeUpdated, pub.Captured[0].EventType);
        var payload = Assert.IsType<TaxCodeUpdated>(pub.Captured[0].Payload);
        Assert.Equal(2, payload.NewVersion);
    }

    // ── TaxRateLookup ────────────────────────────────────────────────────

    private static FL.GLAccount Payable() => FL.GLAccount.Create(
        id: FL.GLAccountId.NewId(),
        chartId: FL.ChartOfAccountsId.NewId(),
        code: "2200",
        name: "Sales tax payable",
        type: FL.GLAccountType.Liability,
        subtype: FL.AccountSubtype.TaxesPayable,
        currency: "USD");

    [Fact]
    public async Task UpsertTaxRate_NewRow_Emits_TaxRateAddedEvent()
    {
        var pub = new RecordingDomainEventPublisher();
        var payable = Payable();
        var accounts = new InMemoryAccountResolver(new[] { payable });
        var rates = new InMemoryTaxRateLookup(accounts, pub);

        var rate = TaxRate.Create(
            TaxCodeId.NewId(), TaxJurisdictionId.NewId(), 5m, D(2026, 1, 1), payable.Id);
        var result = await rates.UpsertAsync(rate);

        Assert.Equal(TaxRateValidationError.None, result.Error);
        Assert.Single(pub.Captured);
        Assert.Equal(FinancialTaxEventNames.TaxRateAdded, pub.Captured[0].EventType);
    }

    [Fact]
    public async Task SupersedeTaxRate_HappyPath_Emits_TaxRateExpired_Then_TaxRateAdded_InOrder()
    {
        var pub = new RecordingDomainEventPublisher();
        var payable = Payable();
        var accounts = new InMemoryAccountResolver(new[] { payable });
        var rates = new InMemoryTaxRateLookup(accounts, pub);
        var codeId = TaxCodeId.NewId();
        var jurisdictionId = TaxJurisdictionId.NewId();

        await rates.UpsertAsync(TaxRate.Create(codeId, jurisdictionId, 5m, D(2025, 1, 1), payable.Id));
        pub.Captured.Clear();

        var result = await rates.SupersedeAsync(
            codeId, jurisdictionId, 6m, D(2026, 7, 1), payable.Id);

        Assert.Equal(TaxRateValidationError.None, result.Error);
        Assert.Equal(2, pub.Captured.Count);
        Assert.Equal(FinancialTaxEventNames.TaxRateExpired, pub.Captured[0].EventType);
        Assert.Equal(FinancialTaxEventNames.TaxRateAdded, pub.Captured[1].EventType);
        // Causation: TaxRateAdded should point back to the TaxRateExpired event.
        Assert.Equal(pub.Captured[0].EventId, pub.Captured[1].CausationId);
    }

    // ── TaxFormLineMapStore ──────────────────────────────────────────────

    [Fact]
    public async Task UpsertTaxFormLineMap_EditedRow_Emits_TaxFormLineMapEditedEvent_WithPriorAndNewSelectors()
    {
        var pub = new RecordingDomainEventPublisher();
        var store = new InMemoryTaxFormLineMapStore(pub);
        var chart = FL.ChartOfAccountsId.NewId();
        var initial = TaxFormLineMap.Create(
            chartId: chart,
            formKind: TaxFormKind.ScheduleE,
            taxYear: 2026,
            line: "Line5",
            description: "Advertising",
            selectors: new[] { new TaxAccountSelector(AccountCode: "5100") },
            perPropertyDimension: true,
            isProvisional: true);
        await store.UpsertAsync(initial);
        pub.Captured.Clear();

        var edited = initial with
        {
            AccountSelectors = new[]
            {
                new TaxAccountSelector(AccountCode: "5100"),
                new TaxAccountSelector(AccountCode: "5101"),
            },
        };
        await store.UpsertAsync(edited);

        Assert.Single(pub.Captured);
        Assert.Equal(FinancialTaxEventNames.TaxFormLineMapEdited, pub.Captured[0].EventType);
        var payload = Assert.IsType<TaxFormLineMapEdited>(pub.Captured[0].Payload);
        Assert.Single(payload.PriorSelectors);
        Assert.Equal(2, payload.NewSelectors.Count);
        Assert.Equal(2, payload.NewVersion);
    }

    [Fact]
    public async Task UpsertTaxFormLineMap_InitialSeed_DoesNotEmitEditedEvent()
    {
        var pub = new RecordingDomainEventPublisher();
        var store = new InMemoryTaxFormLineMapStore(pub);
        var chart = FL.ChartOfAccountsId.NewId();

        // SeedScheduleEAsync inserts 20 rows; none of them should emit
        // Reports.TaxFormLineMapEdited because they're first-inserts.
        var inserted = await store.SeedScheduleEAsync(chart, 2026);

        Assert.Equal(20, inserted);
        Assert.Empty(pub.Captured);
    }
}
