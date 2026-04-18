using System.Security.Cryptography;
using System.Text;

namespace Sunfish.Foundation.Macaroons;

/// <summary>
/// Default <see cref="IMacaroonIssuer"/> backed by an <see cref="IRootKeyStore"/>.
/// </summary>
public sealed class DefaultMacaroonIssuer : IMacaroonIssuer
{
    private readonly IRootKeyStore _keyStore;

    /// <summary>Constructs an issuer that looks up root keys via <paramref name="keyStore"/>.</summary>
    public DefaultMacaroonIssuer(IRootKeyStore keyStore)
    {
        ArgumentNullException.ThrowIfNull(keyStore);
        _keyStore = keyStore;
    }

    /// <inheritdoc />
    public async ValueTask<Macaroon> MintAsync(
        string location,
        string identifier,
        IEnumerable<Caveat> caveats,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(identifier);
        ArgumentNullException.ThrowIfNull(caveats);

        var rootKey = await _keyStore.GetRootKeyAsync(location, ct).ConfigureAwait(false);
        if (rootKey is null)
            throw new InvalidOperationException($"No root key for location '{location}'");

        var caveatList = caveats.ToList();
        var sig = MacaroonCodec.ComputeChain(rootKey, identifier, caveatList);
        return new Macaroon(location, identifier, caveatList, sig);
    }

    /// <inheritdoc />
    public ValueTask<Macaroon> AttenuateAsync(
        Macaroon existing,
        IEnumerable<Caveat> additionalCaveats,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(additionalCaveats);

        // Attenuation does NOT consult the root key — it continues the HMAC chain from the
        // existing signature, which is the whole point of the macaroon chain property.
        var added = additionalCaveats.ToList();
        var sig = existing.Signature;
        foreach (var c in added)
        {
            sig = HMACSHA256.HashData(sig, Encoding.UTF8.GetBytes(c.Predicate));
        }

        var allCaveats = existing.Caveats.Concat(added).ToList();
        return ValueTask.FromResult(new Macaroon(existing.Location, existing.Identifier, allCaveats, sig));
    }
}

/// <summary>
/// Default <see cref="IMacaroonVerifier"/> backed by an <see cref="IRootKeyStore"/>.
/// </summary>
/// <remarks>
/// The verifier performs (1) chain-signature reconstruction using the root key, then
/// (2) constant-time comparison against the macaroon's signature via
/// <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>,
/// then (3) evaluation of each caveat against the supplied context. Failure reasons are
/// diagnostic only — treat any <c>!IsValid</c> as access-denied at the policy layer.
/// </remarks>
public sealed class DefaultMacaroonVerifier : IMacaroonVerifier
{
    private readonly IRootKeyStore _keyStore;

    /// <summary>Constructs a verifier that looks up root keys via <paramref name="keyStore"/>.</summary>
    public DefaultMacaroonVerifier(IRootKeyStore keyStore)
    {
        ArgumentNullException.ThrowIfNull(keyStore);
        _keyStore = keyStore;
    }

    /// <inheritdoc />
    public async ValueTask<MacaroonVerificationResult> VerifyAsync(
        Macaroon macaroon,
        MacaroonContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(macaroon);
        ArgumentNullException.ThrowIfNull(context);

        var rootKey = await _keyStore.GetRootKeyAsync(macaroon.Location, ct).ConfigureAwait(false);
        if (rootKey is null)
            return new MacaroonVerificationResult(false, "No root key");

        var expected = MacaroonCodec.ComputeChain(rootKey, macaroon.Identifier, macaroon.Caveats);
        if (macaroon.Signature is null || macaroon.Signature.Length != expected.Length
            || !CryptographicOperations.FixedTimeEquals(expected, macaroon.Signature))
            return new MacaroonVerificationResult(false, "Signature mismatch");

        for (var i = 0; i < macaroon.Caveats.Count; i++)
        {
            var caveat = macaroon.Caveats[i];
            if (!FirstPartyCaveatParser.Evaluate(caveat, context))
                return new MacaroonVerificationResult(false, $"Caveat failed: {caveat.Predicate}");
        }

        return new MacaroonVerificationResult(true);
    }
}
