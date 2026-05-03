using Sunfish.Foundation.Integrations.Signatures;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Models;

/// <summary>
/// FCRA §615-mandated notice issued when an application is declined
/// based (in whole or in part) on a consumer report. Phase 3 wires the
/// generation flow + the 60-day dispute window enforcement; Phase 1
/// ships only the entity shape.
/// </summary>
/// <remarks>
/// FCRA mandatory content requirements per §615(a):
/// <list type="bullet">
///   <item>Notification of the adverse action.</item>
///   <item>Name + address of the consumer reporting agency.</item>
///   <item>Statement that the CRA did not make the decision + cannot explain it.</item>
///   <item>Notice of consumer's right to obtain a free report from the CRA within 60 days.</item>
///   <item>Notice of consumer's right to dispute accuracy of the report.</item>
/// </list>
/// </remarks>
public sealed record AdverseActionNotice
{
    /// <summary>Unique identifier for this notice.</summary>
    public required AdverseActionNoticeId Id { get; init; }

    /// <summary>The application this notice was issued for.</summary>
    public required ApplicationId Application { get; init; }

    /// <summary>The findings cited as basis for the adverse action.</summary>
    public required IReadOnlyList<AdverseFinding> CitedFindings { get; init; }

    /// <summary>Mandated FCRA §615 statement text included verbatim.</summary>
    public required string FcraStatement { get; init; }

    /// <summary>UTC timestamp by which the consumer must dispute (60 days post-issuance per FCRA).</summary>
    public required DateTimeOffset DisputeWindowExpiresAt { get; init; }

    /// <summary>Name of the consumer reporting agency cited (per FCRA §615(a)(2)).</summary>
    public required string ConsumerReportingAgency { get; init; }

    /// <summary>Address of the consumer reporting agency.</summary>
    public required string Address { get; init; }

    /// <summary>Operator-side signature on the issued notice (ADR 0054).</summary>
    public required SignatureEventRef NoticeIssuanceSignature { get; init; }

    /// <summary>UTC timestamp of issuance.</summary>
    public required DateTimeOffset IssuedAt { get; init; }
}
