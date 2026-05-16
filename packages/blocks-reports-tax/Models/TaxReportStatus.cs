namespace Sunfish.Blocks.TaxReporting.Models;

/// <summary>
/// Lifecycle status of a <see cref="TaxReport"/>.
/// Transitions: Draft → Finalized → Signed → (Amended) → Superseded (old) + new Draft.
/// </summary>
public enum TaxReportStatus
{
    /// <summary>Report is being assembled; content may change.</summary>
    Draft,

    /// <summary>
    /// Report content is locked; canonical-JSON hash (<c>SignatureValue</c>) has been computed.
    /// </summary>
    Finalized,

    /// <summary>
    /// Report has been accepted by the responsible party; a consumer-provided signature value
    /// has been recorded.
    /// </summary>
    Signed,

    /// <summary>
    /// A correction has been initiated. The original report is now <see cref="Superseded"/>;
    /// a new Draft was created as the amendment.
    /// </summary>
    Amended,

    /// <summary>
    /// Report has been superseded by an amendment. Retained for audit history; do not file.
    /// </summary>
    Superseded,
}
