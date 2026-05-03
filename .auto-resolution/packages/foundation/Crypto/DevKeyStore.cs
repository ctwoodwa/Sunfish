namespace Sunfish.Foundation.Crypto;

/// <summary>
/// An in-memory keystore intended for tests and local development. Holds keypairs by principal
/// and disposes them on store disposal. NOT suitable for production — secret key material lives
/// in process memory with no envelope encryption or hardware backing.
/// </summary>
public sealed class DevKeyStore : IDisposable
{
    private readonly Dictionary<PrincipalId, KeyPair> _keyPairs = new();

    /// <summary>Generates a new keypair, registers it, and returns it.</summary>
    public KeyPair Create()
    {
        var keyPair = KeyPair.Generate();
        _keyPairs[keyPair.PrincipalId] = keyPair;
        return keyPair;
    }

    /// <summary>Attempts to retrieve a keypair by principal.</summary>
    public bool TryGet(PrincipalId principalId, out KeyPair? keyPair)
        => _keyPairs.TryGetValue(principalId, out keyPair);

    /// <summary>All principals currently registered in the store.</summary>
    public IReadOnlyCollection<PrincipalId> AllPrincipals => _keyPairs.Keys;

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var kp in _keyPairs.Values)
            kp.Dispose();
        _keyPairs.Clear();
    }
}
