namespace Sunfish.Foundation.SecurityPolicy.Models;

/// <summary>
/// Device-attestation tier per ADR 0068 §1.2. Numeric values are
/// ordered (None &lt; SoftwareSandbox &lt; ... &lt; Fido2HardwareToken)
/// to support future ordinal-comparison consistency checks.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public enum AttestationTier
{
    /// <summary>No attestation; dev / test only.</summary>
    None = 0,

    /// <summary>Software-only; no hardware root of trust.</summary>
    SoftwareSandbox = 10,

    /// <summary>Android Keystore StrongBox / Titan M / OEM secure element.</summary>
    AndroidHardwareKeyStore = 20,

    /// <summary>Windows TPM 2.0.</summary>
    Tpm2 = 30,

    /// <summary>Apple T2 / Apple Silicon SEP / iOS Secure Enclave.</summary>
    AppleSecureElement = 40,

    /// <summary>FIDO2 hardware token with touch-required assertion.</summary>
    Fido2HardwareToken = 50,
}
