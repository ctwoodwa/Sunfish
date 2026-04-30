using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PropertyLeasingPipeline.Services;
using Sunfish.Foundation.Integrations.Signatures;
using Xunit;
using ApplicationId = Sunfish.Blocks.PropertyLeasingPipeline.Models.ApplicationId;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Tests;

public sealed class FcraWorkflowTests
{
    private static readonly ApplicationId TestApplication = new(Guid.NewGuid());
    private static readonly SignatureEventRef TestSignature = new(Guid.NewGuid());
    private static readonly ConsumerReportingAgencyInfo TestCra = new("ABC Reports Inc.", "123 Main St, Anytown, USA");

    private static AdverseFinding MakeFinding() => new()
    {
        Category = "Eviction",
        Description = "2024 eviction in CA Alameda County",
        Source = "ABC Reports Inc.",
        EventDate = new DateOnly(2024, 6, 15),
    };

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    // ─────────── InMemoryBackgroundCheckProvider ───────────

    [Fact]
    public async Task UnseededSsn_ReturnsClearOutcome()
    {
        var provider = new InMemoryBackgroundCheckProvider();
        var result = await provider.KickOffAsync(MakeRequest(), default);

        Assert.Equal(BackgroundCheckOutcome.Clear, result.Outcome);
        Assert.Empty(result.Findings);
        Assert.Equal(TestApplication, result.Application);
        Assert.False(string.IsNullOrEmpty(result.VendorRef));
    }

    [Fact]
    public async Task SeededFindings_AreReturned()
    {
        var provider = new InMemoryBackgroundCheckProvider();
        provider.SeedFindings("XXX-XX-1234", new[] { MakeFinding() });

        var result = await provider.KickOffAsync(MakeRequest(), default);

        Assert.Equal(BackgroundCheckOutcome.HasFindings, result.Outcome);
        Assert.Single(result.Findings);
    }

    [Fact]
    public async Task SeededError_ReturnsErrorOutcome()
    {
        var provider = new InMemoryBackgroundCheckProvider();
        provider.SeedError("XXX-XX-1234");

        var result = await provider.KickOffAsync(MakeRequest(), default);

        Assert.Equal(BackgroundCheckOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsStoredResult()
    {
        var provider = new InMemoryBackgroundCheckProvider();
        var initial = await provider.KickOffAsync(MakeRequest(), default);

        var fetched = await provider.GetStatusAsync(initial.VendorRef, default);

        Assert.Equal(initial, fetched);
    }

    [Fact]
    public async Task GetStatusAsync_UnknownRef_Throws()
    {
        var provider = new InMemoryBackgroundCheckProvider();
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetStatusAsync("unknown", default));
    }

    // ─────────── FcraAdverseActionNoticeGenerator ───────────

    [Fact]
    public void Generate_PopulatesFcraMandatoryStatement()
    {
        var gen = new FcraAdverseActionNoticeGenerator();
        var notice = gen.Generate(TestApplication, new[] { MakeFinding() }, TestCra, TestSignature);

        Assert.Equal(FcraAdverseActionNoticeGenerator.MandatoryFcraStatement, notice.FcraStatement);
        Assert.Contains("60 days", notice.FcraStatement);
        Assert.Contains("dispute", notice.FcraStatement);
    }

    [Fact]
    public void Generate_Sets60DayDisputeWindow()
    {
        var fixedNow = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        var gen = new FcraAdverseActionNoticeGenerator(time: new FakeTimeProvider(fixedNow), disputeWindow: null);

        var notice = gen.Generate(TestApplication, new[] { MakeFinding() }, TestCra, TestSignature);

        Assert.Equal(fixedNow, notice.IssuedAt);
        Assert.Equal(fixedNow.AddDays(60), notice.DisputeWindowExpiresAt);
    }

    [Fact]
    public void Generate_HonorsCustomDisputeWindow()
    {
        var fixedNow = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        // Some state laws extend the window beyond 60; the generator is configurable.
        var gen = new FcraAdverseActionNoticeGenerator(time: new FakeTimeProvider(fixedNow), disputeWindow: TimeSpan.FromDays(90));

        var notice = gen.Generate(TestApplication, new[] { MakeFinding() }, TestCra, TestSignature);

        Assert.Equal(fixedNow.AddDays(90), notice.DisputeWindowExpiresAt);
    }

    [Fact]
    public void Generate_RejectsEmptyFindings()
    {
        var gen = new FcraAdverseActionNoticeGenerator();
        Assert.Throws<ArgumentException>(() =>
            gen.Generate(TestApplication, Array.Empty<AdverseFinding>(), TestCra, TestSignature));
    }

    [Fact]
    public void Generate_RejectsBlankCraName()
    {
        var gen = new FcraAdverseActionNoticeGenerator();
        Assert.Throws<ArgumentException>(() =>
            gen.Generate(TestApplication, new[] { MakeFinding() }, new ConsumerReportingAgencyInfo("", "addr"), TestSignature));
    }

    [Fact]
    public void Generate_RejectsBlankCraAddress()
    {
        var gen = new FcraAdverseActionNoticeGenerator();
        Assert.Throws<ArgumentException>(() =>
            gen.Generate(TestApplication, new[] { MakeFinding() }, new ConsumerReportingAgencyInfo("ABC", "  "), TestSignature));
    }

    [Fact]
    public void Generate_PreservesAllFindingsVerbatim()
    {
        var findings = new[]
        {
            new AdverseFinding { Category = "Eviction", Description = "2024 eviction", Source = "ABC" },
            new AdverseFinding { Category = "CreditDelinquency", Description = "2025 collections", Source = "ABC" },
        };
        var gen = new FcraAdverseActionNoticeGenerator();

        var notice = gen.Generate(TestApplication, findings, TestCra, TestSignature);

        Assert.Equal(2, notice.CitedFindings.Count);
        Assert.Equal("Eviction", notice.CitedFindings[0].Category);
        Assert.Equal("CreditDelinquency", notice.CitedFindings[1].Category);
    }

    [Fact]
    public void Generate_LinksToApplicationAndSignature()
    {
        var gen = new FcraAdverseActionNoticeGenerator();
        var notice = gen.Generate(TestApplication, new[] { MakeFinding() }, TestCra, TestSignature);

        Assert.Equal(TestApplication, notice.Application);
        Assert.Equal(TestSignature, notice.NoticeIssuanceSignature);
        Assert.Equal(TestCra.Name, notice.ConsumerReportingAgency);
        Assert.Equal(TestCra.Address, notice.Address);
    }

    private static BackgroundCheckRequest MakeRequest() => new()
    {
        Application = TestApplication,
        ApplicantFullName = "Alice Smith",
        DateOfBirth = new DateOnly(1990, 1, 1),
        SocialSecurityIdentifier = "XXX-XX-1234",
        PrimaryResidenceState = "CA",
    };
}
