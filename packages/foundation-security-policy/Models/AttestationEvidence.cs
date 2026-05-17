namespace Sunfish.Foundation.SecurityPolicy.Models;

/// <summary>
/// Platform-attestation evidence per ADR 0068 §1.2.
/// <see cref="PlatformProof"/> is opaque to the policy layer; an
/// <c>IAttestationVerifier</c> (future PR) verifies the proof against
/// the platform's attestation root before the evidence is accepted.
/// Hardware tiers (above <see cref="AttestationTier.SoftwareSandbox"/>)
/// REQUIRE non-empty <see cref="PlatformProof"/>; the verifier MUST
/// fail closed otherwise.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// §1.2.1: caller-supplied <see cref="Tier"/> is NOT trusted for
/// hardware tiers — the verifier is the authority on what tier the
/// evidence proves.
/// </para>
/// <para>
/// <see cref="PlatformProof"/> is exposed as <c>ReadOnlyMemory&lt;byte&gt;</c>
/// rather than <c>byte[]</c> so callers cannot mutate the proof bytes
/// after construction; pair with a verifier-side defensive copy when
/// long-term retention is needed.
/// </para>
/// </remarks>
public sealed record AttestationEvidence(
    AttestationTier Tier,
    ReadOnlyMemory<byte> PlatformProof,
    DateTimeOffset EvidenceAt);
