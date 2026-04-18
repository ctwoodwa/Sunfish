namespace Sunfish.Foundation.Macaroons;

/// <summary>
/// Issues new macaroons and attenuates existing ones. Minting requires the root key
/// (so the issuer is the authority at <see cref="Macaroon.Location"/>); attenuation does
/// not — any holder of a macaroon can narrow it further without ever seeing the root key.
/// </summary>
public interface IMacaroonIssuer
{
    /// <summary>
    /// Mints a new macaroon bound to <paramref name="location"/> and <paramref name="identifier"/>,
    /// initialised with the supplied <paramref name="caveats"/>. Requires a root key to be
    /// registered for <paramref name="location"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no root key is registered for <paramref name="location"/>.
    /// </exception>
    ValueTask<Macaroon> MintAsync(
        string location,
        string identifier,
        IEnumerable<Caveat> caveats,
        CancellationToken ct = default);

    /// <summary>
    /// Produces a new macaroon that extends <paramref name="existing"/> with
    /// <paramref name="additionalCaveats"/>. The root key is not required — the chain is
    /// extended by HMAC-chaining from the existing signature.
    /// </summary>
    ValueTask<Macaroon> AttenuateAsync(
        Macaroon existing,
        IEnumerable<Caveat> additionalCaveats,
        CancellationToken ct = default);
}
