using System.Text.RegularExpressions;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.FinancialAr.Tests;

public class InvoiceNumberingServiceTests
{
    private static ReplicaId Replica(string value = "CW") => new(value);
    private static ChartOfAccountsId Chart() => ChartOfAccountsId.NewId();
    private static DateOnly D(int y, int m, int d) => new(y, m, d);
    private static Instant At(string iso) => new(DateTimeOffset.Parse(iso));

    [Fact]
    public async Task NextNumber_FormatMatchesSpec()
    {
        var svc = new InMemoryInvoiceNumberingService(Replica("CW"));
        var num = await svc.NextNumberAsync(Chart(), D(2026, 5, 17));
        Assert.Matches(@"^INV-\d{4}-\d{2}-\d{2}-[A-Z0-9]+-\d{4,}$", num);
        Assert.StartsWith("INV-2026-05-17-CW-", num);
    }

    [Fact]
    public async Task NextNumber_MonotonicallyIncrementsForSameReplicaAndChart()
    {
        var svc = new InMemoryInvoiceNumberingService(Replica("CW"));
        var chart = Chart();
        var nums = new List<string>();
        for (int i = 0; i < 5; i++)
            nums.Add(await svc.NextNumberAsync(chart, D(2026, 5, 17)));

        Assert.EndsWith("-0001", nums[0]);
        Assert.EndsWith("-0002", nums[1]);
        Assert.EndsWith("-0005", nums[4]);
    }

    [Fact]
    public async Task NextNumber_IndependentSequencesPerChart()
    {
        var svc = new InMemoryInvoiceNumberingService(Replica("CW"));
        var a = await svc.NextNumberAsync(Chart(), D(2026, 5, 17));
        var b = await svc.NextNumberAsync(Chart(), D(2026, 5, 17));
        Assert.EndsWith("-0001", a);
        Assert.EndsWith("-0001", b);
    }

    [Fact]
    public async Task NextNumber_IndependentSequencesPerReplica()
    {
        var chart = Chart();
        var cw = new InMemoryInvoiceNumberingService(Replica("CW"));
        var a4 = new InMemoryInvoiceNumberingService(Replica("A4"));

        var cwNum = await cw.NextNumberAsync(chart, D(2026, 5, 17));
        var a4Num = await a4.NextNumberAsync(chart, D(2026, 5, 17));

        Assert.Contains("-CW-0001", cwNum);
        Assert.Contains("-A4-0001", a4Num);
    }

    [Fact]
    public async Task NextNumber_DateVariationDoesNotResetCounter()
    {
        // Sequence is per-(chart, replica), NOT per-date — minting on a new
        // day continues from where the last call left off.
        var svc = new InMemoryInvoiceNumberingService(Replica("CW"));
        var chart = Chart();
        var n1 = await svc.NextNumberAsync(chart, D(2026, 5, 17));
        var n2 = await svc.NextNumberAsync(chart, D(2026, 5, 18)); // next day

        Assert.EndsWith("-0001", n1);
        Assert.Contains("2026-05-18", n2);
        Assert.EndsWith("-0002", n2);
    }

    [Fact]
    public async Task NextNumber_ZeroPadsTo4Digits_AndExpandsBeyondAt10000()
    {
        var svc = new InMemoryInvoiceNumberingService(Replica("CW"));
        var chart = Chart();
        string? last = null;
        for (int i = 0; i < 9_999; i++)
            last = await svc.NextNumberAsync(chart, D(2026, 5, 17));
        Assert.EndsWith("-9999", last);

        var n10000 = await svc.NextNumberAsync(chart, D(2026, 5, 17));
        Assert.EndsWith("-10000", n10000);
    }

    [Theory]
    [InlineData("INV-2026-05-17-CW-0001")]
    [InlineData("INV-2026-12-31-A4-9999")]
    [InlineData("INV-2026-01-01-XYZ-10000")] // 3-char replica + 5-digit seq
    public void IsWellFormed_AcceptsValid(string num)
    {
        Assert.True(InvoiceNumberFormat.IsWellFormed(num));
    }

    [Theory]
    [InlineData("INV-2026-05-17-cw-0001")] // lowercase replica
    [InlineData("INV-26-05-17-CW-0001")]   // 2-digit year
    [InlineData("INV-2026-5-17-CW-0001")]  // unpadded month
    [InlineData("INV-2026-05-17--0001")]   // empty replica
    [InlineData("INV-2026-05-17-CW-1")]    // unpadded seq
    [InlineData("CUST-2026-05-17-CW-0001")] // wrong prefix
    [InlineData("INV-2026-05-17-C W-0001")] // space in replica
    [InlineData("")]
    [InlineData(null)]
    public void IsWellFormed_RejectsInvalid(string? num)
    {
        Assert.False(InvoiceNumberFormat.IsWellFormed(num));
    }

    [Fact]
    public async Task ResolveCollision_OlderReplicaWins_YoungerReKeys()
    {
        var svc = new InMemoryInvoiceNumberingService(Replica("LOCAL"));
        var local = Replica("CW");
        var remote = Replica("A4");

        // Local was created first (earlier timestamp) → remote re-keys.
        var rekey1 = await svc.ResolveCollisionAsync(
            Chart(), "INV-X", local, remote,
            localReplicaCreatedAt: At("2026-01-01T00:00:00Z"),
            remoteReplicaCreatedAt: At("2026-02-01T00:00:00Z"));
        Assert.Equal(remote, rekey1);

        // Swap: remote first, local younger → local re-keys.
        var rekey2 = await svc.ResolveCollisionAsync(
            Chart(), "INV-X", local, remote,
            localReplicaCreatedAt: At("2026-03-01T00:00:00Z"),
            remoteReplicaCreatedAt: At("2026-02-01T00:00:00Z"));
        Assert.Equal(local, rekey2);
    }

    [Fact]
    public async Task ResolveCollision_TiebreakerSimultaneousCreation_LexicographicLargerReKeys()
    {
        var svc = new InMemoryInvoiceNumberingService(Replica("LOCAL"));
        var earlier = Replica("AA");
        var later = Replica("ZZ");
        var sameInstant = At("2026-01-01T00:00:00Z");

        var rekey = await svc.ResolveCollisionAsync(
            Chart(), "INV-X", earlier, later,
            localReplicaCreatedAt: sameInstant,
            remoteReplicaCreatedAt: sameInstant);
        Assert.Equal(later, rekey); // ZZ > AA → ZZ re-keys

        // Symmetric: swap arg order, still same outcome.
        var rekey2 = await svc.ResolveCollisionAsync(
            Chart(), "INV-X", later, earlier,
            localReplicaCreatedAt: sameInstant,
            remoteReplicaCreatedAt: sameInstant);
        Assert.Equal(later, rekey2);
    }
}
