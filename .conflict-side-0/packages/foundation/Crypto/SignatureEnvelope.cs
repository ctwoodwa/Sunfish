namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Algorithm-agility container for cryptographic signatures per ADR 0004.
/// W#21 Phase 0 stub authorized by ADR 0054 amendment A2; full envelope
/// semantics + verification pipeline + dual-sign window for PQC migration
/// land in a dedicated ADR 0004 Stage 06 hand-off (not yet authored).
/// </summary>
/// <param name="Algorithm">
/// Signature algorithm identifier. Phase 1 callers SHOULD use the
/// canonical strings <c>"ed25519"</c> (matching <see cref="Ed25519Signer"/>)
/// or <c>"ecdsa-p256-sha256"</c>. The Phase 0 stub does NOT validate
/// the algorithm string — that's deferred to ADR 0004 Stage 06.
/// </param>
/// <param name="Signature">Raw signature bytes per the algorithm spec. For ed25519, 64 bytes.</param>
/// <param name="Headers">
/// Algorithm-agility headers (key id, certificate chain, attestation
/// tags, etc.); opaque dictionary in Phase 0; ADR 0004 Stage 06 will
/// type-narrow to specific known headers (per RFC 9421 / COSE-style
/// header registry).
/// </param>
public sealed record SignatureEnvelope(
    string Algorithm,
    byte[] Signature,
    IReadOnlyDictionary<string, string> Headers);
