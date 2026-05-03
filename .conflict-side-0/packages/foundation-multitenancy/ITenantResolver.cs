namespace Sunfish.Foundation.MultiTenancy;

/// <summary>
/// Resolves a candidate tenant identifier (host segment, claim value, route
/// parameter, header, …) into <see cref="TenantMetadata"/>. Hosts decide where
/// the candidate comes from; the resolver only knows how to look it up.
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Attempts to resolve a tenant by a host-supplied candidate string.
    /// Returns <c>null</c> when the candidate does not match any registered tenant.
    /// </summary>
    ValueTask<TenantMetadata?> ResolveAsync(
        string candidateIdentifier,
        CancellationToken cancellationToken = default);
}
