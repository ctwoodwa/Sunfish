namespace Sunfish.Foundation.Macaroons;

/// <summary>
/// A macaroon — a bearer credential identified by a <paramref name="Location"/> and
/// <paramref name="Identifier"/>, carrying a chain of first-party <paramref name="Caveats"/>
/// and an HMAC-SHA256 <paramref name="Signature"/> that proves the caveat chain is authentic
/// and un-attenuated beyond what the issuer (or a delegated holder) authorised.
/// </summary>
/// <param name="Location">The issuer's address (used to look up the root key during verification).</param>
/// <param name="Identifier">An opaque issuer-chosen identifier — typically a random value or
/// key-id reference that the issuer can map back to the root key.</param>
/// <param name="Caveats">First-party caveats, ordered. Each caveat contributes one HMAC step
/// to the signature chain and must evaluate to <c>true</c> at verification time.</param>
/// <param name="Signature">The final 32-byte HMAC-SHA256 chain signature.</param>
/// <remarks>
/// Record value-equality over <c>byte[] Signature</c> uses reference equality — when comparing
/// two macaroons for cryptographic equivalence, compare <see cref="Signature"/> via
/// <c>SequenceEqual</c> or <c>CryptographicOperations.FixedTimeEquals</c>, not record-<c>Equals</c>.
/// </remarks>
public sealed record Macaroon(
    string Location,
    string Identifier,
    IReadOnlyList<Caveat> Caveats,
    byte[] Signature);
