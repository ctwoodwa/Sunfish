using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Services;
using Sunfish.Blocks.FinancialTaxBridge.Adapters;
using Sunfish.Blocks.FinancialTaxBridge.DependencyInjection;
using Xunit;

namespace Sunfish.Blocks.FinancialTaxBridge.Tests;

public sealed class ArTaxCalculatorAdapterTests
{
    private static readonly TaxLocationContext FixtureLocation =
        new(IsoCountry: "US", Region: "US-VA");

    private static readonly DateOnly Txn = new(2026, 5, 17);

    private sealed class FakeTaxCalculationService : ITaxCalculationService
    {
        public TaxCalculationInput? LastInput { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public TaxCalculationResult Next { get; set; } = OkResult(0m);

        public Task<TaxCalculationResult> CalculateAsync(TaxCalculationInput input, CancellationToken ct = default)
        {
            LastInput = input;
            LastToken = ct;
            return Task.FromResult(Next);
        }
    }

    private static TaxCalculationResult OkResult(decimal taxAmount)
        => new(
            SubtotalIn: 100m,
            TaxAmount: taxAmount,
            TotalIn: 100m + taxAmount,
            Breakdown: new List<TaxRateBreakdownLine>(),
            Error: TaxCalculationError.None,
            Detail: null,
            CalculatedAtUtc: new System.DateTimeOffset(2026, 5, 17, 12, 0, 0, System.TimeSpan.Zero),
            TaxCodeVersion: 1);

    private static TaxCalculationResult ErrorResult(TaxCalculationError error)
        => new(
            SubtotalIn: 100m,
            TaxAmount: 999m,
            TotalIn: 0m,
            Breakdown: new List<TaxRateBreakdownLine>(),
            Error: error,
            Detail: "boom",
            CalculatedAtUtc: new System.DateTimeOffset(2026, 5, 17, 12, 0, 0, System.TimeSpan.Zero),
            TaxCodeVersion: 0);

    private static (ArTaxCalculatorAdapter Adapter, FakeTaxCalculationService Canonical) Build()
    {
        var fake = new FakeTaxCalculationService();
        var opts = Options.Create(new BlocksFinancialTaxBridgeOptions { DefaultLocation = FixtureLocation });
        return (new ArTaxCalculatorAdapter(fake, opts), fake);
    }

    [Fact]
    public async Task Returns_Zero_When_TaxCodeId_Null()
    {
        var (a, fake) = Build();
        Assert.Equal(0m, await a.CalculateAsync(null, 100m, Txn));
        Assert.Null(fake.LastInput);
    }

    [Fact]
    public async Task Returns_Zero_When_TaxCodeId_WhiteSpace()
    {
        var (a, fake) = Build();
        Assert.Equal(0m, await a.CalculateAsync("   ", 100m, Txn));
        Assert.Null(fake.LastInput);
    }

    [Fact]
    public async Task Returns_TaxAmount_When_Canonical_Succeeds()
    {
        var (a, fake) = Build();
        fake.Next = OkResult(7.50m);
        Assert.Equal(7.50m, await a.CalculateAsync("VA-RETAIL", 100m, Txn));
    }

    [Fact]
    public async Task Returns_Zero_When_Canonical_Errors()
    {
        var (a, _) = Build();
        var (b, fake) = Build();
        fake.Next = ErrorResult(TaxCalculationError.TaxCodeNotFound);
        Assert.Equal(0m, await b.CalculateAsync("MISSING", 100m, Txn));
    }

    [Fact]
    public async Task Forwards_TransactionDate_Verbatim()
    {
        var (a, fake) = Build();
        var date = new DateOnly(2024, 7, 4);
        await a.CalculateAsync("VA-RETAIL", 100m, date);
        Assert.Equal(date, fake.LastInput!.TransactionDate);
    }

    [Fact]
    public async Task Forwards_CancellationToken()
    {
        var (a, fake) = Build();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await a.CalculateAsync("VA-RETAIL", 100m, Txn, cts.Token);
        Assert.True(fake.LastToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Adapter_ForwardsConfiguredLocation_ToCanonical()
    {
        var (a, fake) = Build();
        await a.CalculateAsync("VA-RETAIL", 100m, Txn);
        Assert.NotNull(fake.LastInput);
        Assert.Equal("US", fake.LastInput!.Location.IsoCountry);
        Assert.Equal("US-VA", fake.LastInput!.Location.Region);
    }
}
