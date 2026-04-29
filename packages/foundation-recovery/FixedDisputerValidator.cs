using System.Security.Cryptography;

namespace Sunfish.Foundation.Recovery;

/// <summary>
/// <see cref="IDisputerValidator"/> backed by a fixed set of public keys
/// supplied at construction. Used by tests and by Phase 1 host wiring
/// where the authorized disputer keys are known up front (typically the
/// owner's NodeIdentity public key).
/// </summary>
public sealed class FixedDisputerValidator : IDisputerValidator
{
    private readonly byte[][] _authorizedKeys;

    /// <summary>
    /// Construct with a snapshot of authorized public keys. Each key is
    /// copied defensively so subsequent caller mutations cannot bypass
    /// validation.
    /// </summary>
    public FixedDisputerValidator(IEnumerable<byte[]> authorizedKeys)
    {
        ArgumentNullException.ThrowIfNull(authorizedKeys);
        _authorizedKeys = authorizedKeys.Select(k => (byte[])k.Clone()).ToArray();
    }

    /// <inheritdoc />
    public Task<bool> IsAuthorizedAsync(
        ReadOnlyMemory<byte> disputerPublicKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var span = disputerPublicKey.Span;
        foreach (var key in _authorizedKeys)
        {
            if (key.Length != span.Length) continue;
            if (CryptographicOperations.FixedTimeEquals(key, span))
            {
                return Task.FromResult(true);
            }
        }
        return Task.FromResult(false);
    }
}
