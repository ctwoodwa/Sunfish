namespace Sunfish.Foundation.SecurityPolicy.Models;

/// <summary>Audit-event classification used by <see cref="AuditRetentionPolicy"/> per ADR 0068 §1.3.</summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public enum AuditEventClass
{
    Security,
    Financial,
    Identity,
    Configuration,
    System,
}
