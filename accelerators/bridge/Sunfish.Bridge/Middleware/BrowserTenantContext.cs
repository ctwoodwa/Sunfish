using Sunfish.Bridge.Data.Entities;

namespace Sunfish.Bridge.Middleware;

/// <summary>
/// Scoped <see cref="IBrowserTenantContext"/> populated by
/// <see cref="TenantSubdomainResolutionMiddleware"/>. Unresolved reads throw
/// so that any code path reaching the context before the middleware runs fails
/// loudly (Wave 5.3.A, <c>_shared/product/wave-5.3-decomposition.md</c> §5.3.A).
/// </summary>
public sealed class BrowserTenantContext : IBrowserTenantContext
{
    private bool _resolved;
    private Guid _tenantId;
    private string _slug = string.Empty;
    private TrustLevel _trustLevel;
    private byte[]? _teamPublicKey;
    private byte[] _authSalt = [];

    /// <inheritdoc />
    public bool IsResolved => _resolved;

    /// <inheritdoc />
    public Guid TenantId => _resolved
        ? _tenantId
        : throw new InvalidOperationException("Middleware did not resolve tenant");

    /// <inheritdoc />
    public string Slug => _resolved
        ? _slug
        : throw new InvalidOperationException("Middleware did not resolve tenant");

    /// <inheritdoc />
    public TrustLevel TrustLevel => _resolved
        ? _trustLevel
        : throw new InvalidOperationException("Middleware did not resolve tenant");

    /// <inheritdoc />
    public byte[]? TeamPublicKey => _resolved
        ? _teamPublicKey
        : throw new InvalidOperationException("Middleware did not resolve tenant");

    /// <inheritdoc />
    public byte[] AuthSalt => _resolved
        ? _authSalt
        : throw new InvalidOperationException("Middleware did not resolve tenant");

    /// <inheritdoc />
    public void Bind(Guid tenantId, string slug, TrustLevel trustLevel, byte[]? teamPublicKey, byte[] authSalt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(authSalt);
        if (authSalt.Length == 0)
        {
            throw new ArgumentException("authSalt must be non-empty.", nameof(authSalt));
        }

        _tenantId = tenantId;
        _slug = slug;
        _trustLevel = trustLevel;
        _teamPublicKey = teamPublicKey;
        _authSalt = authSalt;
        _resolved = true;
    }
}
