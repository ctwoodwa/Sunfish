namespace Sunfish.Foundation.SecurityPolicy.Models;

/// <summary>
/// Multi-factor authentication factor types per ADR 0068 §1.1.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// <see cref="Email"/> is low assurance — the floor validator
/// (subsequent PR) rejects configurations where it is the sole
/// factor for elevated roles. <see cref="Sms"/> is a NIST SP
/// 800-63B Rev. 3 §5.1.3.3 RESTRICTED authenticator (SIM-swap /
/// SS7 / number-recycling risk); permitted but discouraged.
/// <see cref="Totp"/>/<see cref="Email"/>/<see cref="Sms"/> are
/// cognitive-test factors per WCAG 3.3.8 — UX MUST offer at least
/// one cognitive-test-free path (<see cref="WebAuthnPasskey"/> or
/// <see cref="HardwareKey"/>) per §1.1.6.
/// </para>
/// </remarks>
public enum MfaFactor
{
    /// <summary>RFC 6238 time-based OTP; copy-paste MUST be enabled (WCAG 3.3.8).</summary>
    Totp,

    /// <summary>FIDO2/WebAuthn passkey; no cognitive-function test (preferred).</summary>
    WebAuthnPasskey,

    /// <summary>FIDO2 hardware token (e.g., YubiKey) with touch-required assertion.</summary>
    HardwareKey,

    /// <summary>OTP delivered via email; low assurance. Copy-paste MUST be enabled.</summary>
    Email,

    /// <summary>
    /// OTP delivered via SMS. NIST SP 800-63B Rev. 3 §5.1.3.3 RESTRICTED
    /// authenticator — permitted but discouraged. Threat vectors:
    /// SIM-swap, SS7 interception, number-recycling. Subject to risk-
    /// assessment + user-notification requirements per NIST.
    /// </summary>
    Sms,
}
