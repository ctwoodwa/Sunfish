using System.Collections.Generic;

namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>
/// Default regime-stance table per ADR 0064-A1.13. Subject to legal-counsel
/// review; ships unmodified in Phase 1 substrate.
/// </summary>
/// <remarks>
/// <para>
/// <b>InScope</b> (alphabetized): CCPA, EU_AI_Act, FHA, GDPR, SOC2.
/// <b>CommercialProductOnly</b>: HIPAA.
/// <b>ExplicitlyDisclaimedOpenSource</b> (per A1.13 reframe): PCI_DSS_v4.
/// </para>
/// <para>
/// The <see cref="RegimeAcknowledgment.RationaleKey"/> values are
/// localization keys — the host resolves them to human-readable text via
/// its localization stack.
/// </para>
/// </remarks>
public static class DefaultRegimeStances
{
    /// <summary>The Phase 1 default regime-stance acknowledgment table.</summary>
    public static readonly IReadOnlyList<RegimeAcknowledgment> Stances = new[]
    {
        // InScope (alphabetized)
        new RegimeAcknowledgment
        {
            Regime = RegulatoryRegime.CCPA,
            Stance = RegulatoryRegimeStance.InScope,
            RationaleKey = "regulatory.stance.ccpa.inscope",
        },
        new RegimeAcknowledgment
        {
            Regime = RegulatoryRegime.EU_AI_Act,
            Stance = RegulatoryRegimeStance.InScope,
            RationaleKey = "regulatory.stance.euaiact.inscope.placeholder",
        },
        new RegimeAcknowledgment
        {
            Regime = RegulatoryRegime.FHA,
            Stance = RegulatoryRegimeStance.InScope,
            RationaleKey = "regulatory.stance.fha.inscope",
        },
        new RegimeAcknowledgment
        {
            Regime = RegulatoryRegime.GDPR,
            Stance = RegulatoryRegimeStance.InScope,
            RationaleKey = "regulatory.stance.gdpr.inscope",
        },
        new RegimeAcknowledgment
        {
            Regime = RegulatoryRegime.SOC2,
            Stance = RegulatoryRegimeStance.InScope,
            RationaleKey = "regulatory.stance.soc2.inscope",
        },
        // CommercialProductOnly
        new RegimeAcknowledgment
        {
            Regime = RegulatoryRegime.HIPAA,
            Stance = RegulatoryRegimeStance.CommercialProductOnly,
            RationaleKey = "regulatory.stance.hipaa.commercial-only",
        },
        // ExplicitlyDisclaimedOpenSource (per A1.13 reframe)
        new RegimeAcknowledgment
        {
            Regime = RegulatoryRegime.PCI_DSS_v4,
            Stance = RegulatoryRegimeStance.ExplicitlyDisclaimedOpenSource,
            RationaleKey = "regulatory.stance.pcidss.disclaimed",
        },
    };
}
