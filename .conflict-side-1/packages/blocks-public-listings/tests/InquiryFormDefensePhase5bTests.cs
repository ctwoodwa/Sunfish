using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.PublicListings.Defense;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Captcha;
using Sunfish.Foundation.Integrations.Messaging;
using Xunit;

namespace Sunfish.Blocks.PublicListings.Tests;

public sealed class InquiryFormDefensePhase5bTests
{
    private static readonly TenantId TestTenant = new("tenant-a");
    private static readonly IPAddress TestIp = IPAddress.Parse("198.51.100.42");

    private static InquiryFormSubmission Submission(string body = "Is this listing still available?") => new()
    {
        Tenant = TestTenant,
        CaptchaToken = "good-token",
        ClientIp = TestIp,
        ProspectEmail = "alice@example.com",
        MessageBody = body,
        ReceivedAt = new DateTimeOffset(2026, 4, 30, 13, 0, 0, TimeSpan.Zero),
        InquirerName = "Alice Cooper",
        ListingSlug = "downtown-loft",
        UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36",
    };

    private static (InquiryFormDefense def, InMemoryCaptchaVerifier captcha, FakeScorer scorer, FakeTriageQueue queue) NewDefenseWithLayer4And5(int? hardThreshold = null, int? softThreshold = null)
    {
        var captcha = new InMemoryCaptchaVerifier();
        captcha.Seed("good-token", 0.9);
        var rate = new InMemoryInquiryRateLimiter();
        var mx = new StubEmailMxResolver { DefaultVerdict = true };
        var scorer = new FakeScorer();
        var queue = new FakeTriageQueue();
        var options = new InquiryFormDefenseOptions
        {
            HardRejectScore = hardThreshold ?? 80,
            SoftRejectScore = softThreshold ?? 50,
        };
        var def = new InquiryFormDefense(captcha, rate, mx, scorer, queue, options, audit: null);
        return (def, captcha, scorer, queue);
    }

    [Fact]
    public async Task Layer4_HighScore_HardRejectsAndDoesNotEnqueue()
    {
        var (def, _, scorer, queue) = NewDefenseWithLayer4And5();
        scorer.NextScore = 90;

        var result = await def.EvaluateAsync(Submission(), CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Equal(InquiryDefenseLayer.AbuseScore, result.RejectedAt);
        Assert.Contains("hard-reject threshold", result.Reason);
        Assert.Empty(queue.Enqueued);
    }

    [Fact]
    public async Task Layer5_MediumScore_EnqueuesAndSoftRejects()
    {
        var (def, _, scorer, queue) = NewDefenseWithLayer4And5();
        scorer.NextScore = 60;

        var result = await def.EvaluateAsync(Submission(), CancellationToken.None);

        Assert.False(result.Passed);
        Assert.Equal(InquiryDefenseLayer.ManualTriage, result.RejectedAt);
        Assert.Single(queue.Enqueued);
        Assert.Equal(TestTenant, queue.Enqueued[0].Tenant);
        Assert.Contains("soft-reject threshold", queue.Enqueued[0].Reason);
    }

    [Fact]
    public async Task Layer4_LowScore_PassesThrough()
    {
        var (def, _, scorer, queue) = NewDefenseWithLayer4And5();
        scorer.NextScore = 10;

        var result = await def.EvaluateAsync(Submission(), CancellationToken.None);

        Assert.True(result.Passed);
        Assert.Empty(queue.Enqueued);
    }

    [Fact]
    public async Task Layer4_ScorerNull_SkipsLayersFourAndFive()
    {
        var captcha = new InMemoryCaptchaVerifier();
        captcha.Seed("good-token", 0.9);
        var def = new InquiryFormDefense(
            captcha,
            new InMemoryInquiryRateLimiter(),
            new StubEmailMxResolver { DefaultVerdict = true },
            scorer: null,
            triageQueue: null,
            options: null,
            audit: null);

        var result = await def.EvaluateAsync(Submission(), CancellationToken.None);

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task SyntheticEnvelope_PreservesIpUserAgentAndCaptchaScore()
    {
        var (def, captcha, scorer, _) = NewDefenseWithLayer4And5();

        await def.EvaluateAsync(Submission(), CancellationToken.None);

        var envelope = scorer.LastEnvelope;
        Assert.NotNull(envelope);
        Assert.Equal("public-listings-inquiry-form", envelope!.ProviderKey);
        Assert.Equal(MessageChannel.Web, envelope.Channel);
        Assert.Equal("198.51.100.42", envelope.ProviderHeaders["client-ip"]);
        Assert.Equal("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36", envelope.ProviderHeaders["user-agent"]);
        Assert.Equal("0.90", envelope.ProviderHeaders["captcha-score"]);
        Assert.Equal("public-listings-inquiry-form", envelope.ProviderHeaders["form-source"]);
        Assert.Equal("alice@example.com", envelope.SenderAddress);
        Assert.Equal("Alice Cooper", envelope.SenderDisplayName);
        Assert.Contains("downtown-loft inquiry", envelope.Subject);
    }

    [Fact]
    public async Task SyntheticEnvelope_RawBodyMirrorsParsedBody()
    {
        var (def, _, scorer, _) = NewDefenseWithLayer4And5();

        await def.EvaluateAsync(Submission(body: "Looking for 2br pet-friendly"), CancellationToken.None);

        Assert.NotNull(scorer.LastEnvelope);
        Assert.Equal("Looking for 2br pet-friendly", scorer.LastEnvelope!.ParsedBody);
        var raw = System.Text.Encoding.UTF8.GetString(scorer.LastEnvelope.RawBody.Span);
        Assert.Equal("Looking for 2br pet-friendly", raw);
    }

    [Fact]
    public async Task Layer3_FailureSkipsScorerEntirely()
    {
        var (def, _, scorer, queue) = NewDefenseWithLayer4And5();
        scorer.NextScore = 90;
        var bogusMx = new InquiryFormSubmission
        {
            Tenant = TestTenant,
            CaptchaToken = "good-token",
            ClientIp = TestIp,
            ProspectEmail = "not-an-email",
            MessageBody = "...",
            ReceivedAt = DateTimeOffset.UtcNow,
        };

        var result = await def.EvaluateAsync(bogusMx, CancellationToken.None);

        Assert.Equal(InquiryDefenseLayer.EmailFormatAndMx, result.RejectedAt);
        Assert.Null(scorer.LastEnvelope);
        Assert.Empty(queue.Enqueued);
    }

    [Fact]
    public async Task Layer5_TriageQueueNullButScorerNonNull_HardRejectStillFires()
    {
        var captcha = new InMemoryCaptchaVerifier();
        captcha.Seed("good-token", 0.9);
        var scorer = new FakeScorer { NextScore = 90 };
        var def = new InquiryFormDefense(
            captcha,
            new InMemoryInquiryRateLimiter(),
            new StubEmailMxResolver { DefaultVerdict = true },
            scorer: scorer,
            triageQueue: null,
            options: null,
            audit: null);

        var result = await def.EvaluateAsync(Submission(), CancellationToken.None);

        Assert.Equal(InquiryDefenseLayer.AbuseScore, result.RejectedAt);
    }

    [Fact]
    public async Task Layer5_TriageQueueNullAndMediumScore_PassesThrough()
    {
        // When scorer is wired but triage queue isn't, medium-score
        // submissions pass — there's nowhere to enqueue them. Document
        // this as the intentional graceful-degradation behavior.
        var captcha = new InMemoryCaptchaVerifier();
        captcha.Seed("good-token", 0.9);
        var scorer = new FakeScorer { NextScore = 60 };
        var def = new InquiryFormDefense(
            captcha,
            new InMemoryInquiryRateLimiter(),
            new StubEmailMxResolver { DefaultVerdict = true },
            scorer: scorer,
            triageQueue: null,
            options: null,
            audit: null);

        var result = await def.EvaluateAsync(Submission(), CancellationToken.None);

        Assert.True(result.Passed);
    }

    private sealed class FakeScorer : IInboundMessageScorer
    {
        public int NextScore { get; set; }
        public InboundMessageEnvelope? LastEnvelope { get; private set; }

        public Task<int> ScoreAsync(InboundMessageEnvelope envelope, CancellationToken ct)
        {
            LastEnvelope = envelope;
            return Task.FromResult(NextScore);
        }
    }

    private sealed class FakeTriageQueue : IUnroutedTriageQueue
    {
        public List<(TenantId Tenant, InboundMessageEnvelope Envelope, string Reason)> Enqueued { get; } = new();

        public Task<Guid> EnqueueAsync(TenantId tenant, InboundMessageEnvelope envelope, string reason, CancellationToken ct)
        {
            Enqueued.Add((tenant, envelope, reason));
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<IReadOnlyList<UnroutedTriageEntry>> ListPendingAsync(TenantId tenant, CancellationToken ct)
            => throw new NotImplementedException();

        public Task ResolveAsync(TenantId tenant, Guid entryId, TriageResolution resolution, ActorId resolvedBy, CancellationToken ct)
            => throw new NotImplementedException();
    }
}
