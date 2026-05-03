using System.Globalization;
using System.Net.Mail;
using System.Text;
using Sunfish.Blocks.PublicListings.Audit;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Captcha;
using Sunfish.Foundation.Integrations.Messaging;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.PublicListings.Defense;

/// <summary>
/// Default <see cref="IInquiryFormDefense"/> wiring all 5 defense layers.
/// Layers 1–3 ship in Phase 5a (CAPTCHA + per-IP/per-tenant rate limit +
/// email format + DNS MX). Layers 4–5 ship in Phase 5b — abuse scoring
/// via <see cref="IInboundMessageScorer"/> and manual triage via
/// <see cref="IUnroutedTriageQueue"/>, both reused from W#20 (ADR 0052)
/// through a synthetic <see cref="InboundMessageEnvelope"/> adapter
/// per the W#28 Phase 5b unblock addendum.
/// W#28 Phase 7: when a <see cref="PublicListingAuditEmitter"/> is
/// supplied, every rejection emits <see cref="AuditEventType.InquiryRejected"/>
/// with the rejecting layer + reason in the body for forensic
/// reconstruction.
/// </summary>
public sealed class InquiryFormDefense : IInquiryFormDefense
{
    private readonly ICaptchaVerifier _captcha;
    private readonly IInquiryRateLimiter _rateLimiter;
    private readonly IEmailMxResolver _mxResolver;
    private readonly IInboundMessageScorer? _scorer;
    private readonly IUnroutedTriageQueue? _triageQueue;
    private readonly InquiryFormDefenseOptions _options;
    private readonly PublicListingAuditEmitter? _audit;

    /// <summary>Creates the orchestrator with Layers 1–3 only (Phase 5a parity); audit emission disabled.</summary>
    public InquiryFormDefense(
        ICaptchaVerifier captcha,
        IInquiryRateLimiter rateLimiter,
        IEmailMxResolver mxResolver)
        : this(captcha, rateLimiter, mxResolver, audit: null) { }

    /// <summary>Creates the orchestrator with Layers 1–3 + optional audit emission (W#28 Phase 7).</summary>
    public InquiryFormDefense(
        ICaptchaVerifier captcha,
        IInquiryRateLimiter rateLimiter,
        IEmailMxResolver mxResolver,
        PublicListingAuditEmitter? audit)
        : this(captcha, rateLimiter, mxResolver, scorer: null, triageQueue: null, options: null, audit: audit) { }

    /// <summary>
    /// Creates the orchestrator with all 5 layers — adds Layers 4–5
    /// (W#28 Phase 5b) when <paramref name="scorer"/> +
    /// <paramref name="triageQueue"/> are supplied. When either is null
    /// the corresponding layer is skipped (graceful degradation matches
    /// the audit-emitter pattern).
    /// </summary>
    public InquiryFormDefense(
        ICaptchaVerifier captcha,
        IInquiryRateLimiter rateLimiter,
        IEmailMxResolver mxResolver,
        IInboundMessageScorer? scorer,
        IUnroutedTriageQueue? triageQueue,
        InquiryFormDefenseOptions? options,
        PublicListingAuditEmitter? audit)
    {
        ArgumentNullException.ThrowIfNull(captcha);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(mxResolver);
        _captcha = captcha;
        _rateLimiter = rateLimiter;
        _mxResolver = mxResolver;
        _scorer = scorer;
        _triageQueue = triageQueue;
        _options = options ?? new InquiryFormDefenseOptions();
        _audit = audit;
    }

    /// <inheritdoc />
    public async Task<InquiryDefenseResult> EvaluateAsync(InquiryFormSubmission submission, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(submission);

        // Layer 1 — CAPTCHA verify.
        var captchaResult = await _captcha.VerifyAsync(submission.CaptchaToken, submission.ClientIp, ct).ConfigureAwait(false);
        if (!captchaResult.Passed)
        {
            return await RejectAsync(
                submission,
                InquiryDefenseLayer.Captcha,
                $"CAPTCHA rejected (provider={captchaResult.Provider}, score={captchaResult.Score:0.00}).",
                ct).ConfigureAwait(false);
        }

        // Layer 2 — per-IP + per-tenant rate limit.
        var rateVerdict = await _rateLimiter.CheckAsync(submission.ClientIp, submission.Tenant, submission.ReceivedAt, ct).ConfigureAwait(false);
        if (!rateVerdict.Allowed)
        {
            return await RejectAsync(
                submission,
                InquiryDefenseLayer.RateLimit,
                $"Rate limit exceeded ({rateVerdict.ExceededWindow}).",
                ct).ConfigureAwait(false);
        }

        // Layer 3 — email format + DNS MX.
        if (!TryParseEmail(submission.ProspectEmail, out var domain))
        {
            return await RejectAsync(
                submission,
                InquiryDefenseLayer.EmailFormatAndMx,
                "Email failed format validation.",
                ct).ConfigureAwait(false);
        }
        var hasMx = await _mxResolver.HasMxRecordAsync(domain, ct).ConfigureAwait(false);
        if (!hasMx)
        {
            return await RejectAsync(
                submission,
                InquiryDefenseLayer.EmailFormatAndMx,
                $"Domain '{domain}' publishes no MX record.",
                ct).ConfigureAwait(false);
        }

        // Layer 4 — abuse scoring (W#20 IInboundMessageScorer via synthetic envelope).
        if (_scorer is not null)
        {
            var envelope = ToInboundMessageEnvelope(submission, captchaResult.Score);
            var score = await _scorer.ScoreAsync(envelope, ct).ConfigureAwait(false);

            if (score >= _options.HardRejectScore)
            {
                return await RejectAsync(
                    submission,
                    InquiryDefenseLayer.AbuseScore,
                    $"Abuse score {score} >= hard-reject threshold {_options.HardRejectScore}.",
                    ct).ConfigureAwait(false);
            }

            // Layer 5 — manual triage queue (W#20 IUnroutedTriageQueue).
            if (score >= _options.SoftRejectScore && _triageQueue is not null)
            {
                var reason = $"Abuse score {score} >= soft-reject threshold {_options.SoftRejectScore}; held for manual review.";
                await _triageQueue.EnqueueAsync(submission.Tenant, envelope, reason, ct).ConfigureAwait(false);
                return await RejectAsync(
                    submission,
                    InquiryDefenseLayer.ManualTriage,
                    reason,
                    ct).ConfigureAwait(false);
            }
        }

        return InquiryDefenseResult.Pass;
    }

    /// <summary>
    /// Builds the synthetic <see cref="InboundMessageEnvelope"/>
    /// per the Phase 5b unblock addendum: <c>Channel = Web</c>,
    /// <c>ProviderKey = "public-listings-inquiry-form"</c>,
    /// provider headers preserve client-ip / user-agent / captcha-score.
    /// </summary>
    internal static InboundMessageEnvelope ToInboundMessageEnvelope(
        InquiryFormSubmission submission,
        double captchaScore)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["client-ip"] = submission.ClientIp.ToString(),
            ["user-agent"] = submission.UserAgent ?? string.Empty,
            ["captcha-score"] = captchaScore.ToString("F2", CultureInfo.InvariantCulture),
            ["form-source"] = "public-listings-inquiry-form",
        };

        var subject = string.IsNullOrEmpty(submission.ListingSlug)
            ? $"inquiry from {submission.InquirerName ?? submission.ProspectEmail}"
            : $"{submission.ListingSlug} inquiry from {submission.InquirerName ?? submission.ProspectEmail}";

        return new InboundMessageEnvelope
        {
            ProviderKey = "public-listings-inquiry-form",
            Channel = MessageChannel.Web,
            ProviderHeaders = headers,
            RawBody = Encoding.UTF8.GetBytes(submission.MessageBody),
            ParsedBody = submission.MessageBody,
            Subject = subject,
            SenderAddress = submission.ProspectEmail,
            SenderDisplayName = submission.InquirerName,
            ParsedToken = null,
            ReceivedAt = submission.ReceivedAt,
        };
    }

    private async Task<InquiryDefenseResult> RejectAsync(InquiryFormSubmission submission, InquiryDefenseLayer layer, string reason, CancellationToken ct)
    {
        if (_audit is not null)
        {
            // The defense doesn't know the listing id at this layer — the
            // route maps slug → listing upstream; audit captures tenant +
            // route-shape submission only. A "listing-unknown" sentinel
            // keeps the body shape stable for downstream filtering.
            await _audit.EmitAsync(
                AuditEventType.InquiryRejected,
                PublicListingAuditPayloadFactory.InquiryRejected(submission.Tenant, default, layer, reason),
                submission.ReceivedAt,
                ct).ConfigureAwait(false);
        }
        return InquiryDefenseResult.Fail(layer, reason);
    }

    private static bool TryParseEmail(string email, out string domain)
    {
        domain = string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }
        try
        {
            var parsed = new MailAddress(email);
            domain = parsed.Host;
            return !string.IsNullOrWhiteSpace(domain);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
