using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Foundation.Integrations.Signatures;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Services;

/// <summary>
/// Generates an FCRA §615-compliant <see cref="AdverseActionNotice"/>
/// when an application is declined and a consumer report contributed to
/// the decision. The 60-day dispute window per FCRA §612 starts at
/// issuance.
/// </summary>
public interface IAdverseActionNoticeGenerator
{
    /// <summary>
    /// Builds an adverse-action notice citing <paramref name="findings"/>
    /// from <paramref name="cra"/>. The returned record carries the
    /// FCRA mandatory statement + a 60-day dispute window expiry.
    /// </summary>
    AdverseActionNotice Generate(
        ApplicationId application,
        IReadOnlyList<AdverseFinding> findings,
        ConsumerReportingAgencyInfo cra,
        SignatureEventRef issuanceSignature);
}

/// <summary>Identification of the consumer reporting agency that supplied the report (per FCRA §615(a)(2)).</summary>
/// <param name="Name">Legal name of the CRA.</param>
/// <param name="Address">Mail address of the CRA — consumer mails dispute here.</param>
public sealed record ConsumerReportingAgencyInfo(string Name, string Address);

/// <summary>
/// Default <see cref="IAdverseActionNoticeGenerator"/>. The FCRA §615
/// statement is the mandatory disclosure language that every
/// consumer-report-based adverse action must include verbatim per the
/// Consumer Financial Protection Bureau model summary form.
/// </summary>
public sealed class FcraAdverseActionNoticeGenerator : IAdverseActionNoticeGenerator
{
    /// <summary>Default FCRA dispute window (60 days post-issuance per §612(a)).</summary>
    public static readonly TimeSpan DefaultDisputeWindow = TimeSpan.FromDays(60);

    /// <summary>
    /// FCRA §615(a) mandatory statement. The wording mirrors the model
    /// summary the CFPB publishes as the safe-harbor disclosure for
    /// adverse-action notices.
    /// </summary>
    public const string MandatoryFcraStatement =
        "We took adverse action on your application based in whole or in part on information " +
        "contained in a consumer report. The consumer reporting agency named below provided " +
        "the report; that agency did not make the decision and cannot explain why the decision " +
        "was made. You have the right to obtain, free of charge within 60 days of receiving " +
        "this notice, a copy of the report from the consumer reporting agency. You also have " +
        "the right to dispute the accuracy or completeness of any information in the report " +
        "directly with the agency.";

    private readonly TimeProvider _time;
    private readonly TimeSpan _disputeWindow;

    /// <summary>Creates a generator with the standard 60-day window + system clock.</summary>
    public FcraAdverseActionNoticeGenerator() : this(time: null, disputeWindow: null) { }

    /// <summary>Creates a generator with custom time + dispute-window settings.</summary>
    public FcraAdverseActionNoticeGenerator(TimeProvider? time, TimeSpan? disputeWindow)
    {
        _time = time ?? TimeProvider.System;
        _disputeWindow = disputeWindow ?? DefaultDisputeWindow;
    }

    /// <inheritdoc />
    public AdverseActionNotice Generate(
        ApplicationId application,
        IReadOnlyList<AdverseFinding> findings,
        ConsumerReportingAgencyInfo cra,
        SignatureEventRef issuanceSignature)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(cra);
        if (findings.Count == 0)
        {
            throw new ArgumentException("FCRA notice requires at least one cited finding.", nameof(findings));
        }
        if (string.IsNullOrWhiteSpace(cra.Name))
        {
            throw new ArgumentException("ConsumerReportingAgency.Name is required.", nameof(cra));
        }
        if (string.IsNullOrWhiteSpace(cra.Address))
        {
            throw new ArgumentException("ConsumerReportingAgency.Address is required.", nameof(cra));
        }

        var issuedAt = _time.GetUtcNow();
        return new AdverseActionNotice
        {
            Id = new AdverseActionNoticeId(Guid.NewGuid()),
            Application = application,
            CitedFindings = findings,
            FcraStatement = MandatoryFcraStatement,
            DisputeWindowExpiresAt = issuedAt + _disputeWindow,
            ConsumerReportingAgency = cra.Name,
            Address = cra.Address,
            NoticeIssuanceSignature = issuanceSignature,
            IssuedAt = issuedAt,
        };
    }
}
