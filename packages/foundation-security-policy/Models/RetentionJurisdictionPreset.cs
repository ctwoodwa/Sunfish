namespace Sunfish.Foundation.SecurityPolicy.Models;

/// <summary>
/// Convenience defaults informed by common interpretations of the
/// named regulatory regimes per ADR 0068 §1.3. Selecting a preset
/// does NOT make a tenant compliant with the named regime —
/// applicable retention windows depend on the specific data being
/// processed, the deployment jurisdiction, and qualified legal
/// counsel.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public enum RetentionJurisdictionPreset
{
    /// <summary>Per-class overrides apply; no preset floor.</summary>
    Custom = 0,

    /// <summary>6-year floor on Identity+Security+Configuration; §GC.1 applies.</summary>
    HipaaInformedDefault = 1,

    /// <summary>12-month retention / 3-month immediately-available for Financial+Security; §GC.1 applies.</summary>
    PciDssInformedDefault = 2,

    /// <summary>7-year common baseline; SOC 2 does not mandate a specific window; §GC.1 applies.</summary>
    Soc2InformedDefault = 3,

    /// <summary>Requires per-class manual configuration; duration = processing purpose; §GC.1 applies.</summary>
    GdprInformedDefault = 4,

    /// <summary>10-year floor for high-risk AI systems; consult general counsel before enabling; §GC.1 applies.</summary>
    EuAiActInformedDefault = 5,
}
