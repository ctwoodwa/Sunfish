namespace Sunfish.Foundation.Integrations;

/// <summary>
/// Opaque reference to credentials stored outside the contract surface.
/// This record never holds plaintext secrets — it names where the secret
/// can be resolved (vault path, secret-manager key). The actual resolution
/// happens in a separate secrets-management adapter.
/// </summary>
public sealed record CredentialsReference
{
    /// <summary>Provider that owns these credentials.</summary>
    public required string ProviderKey { get; init; }

    /// <summary>Authentication scheme (e.g. <c>apiKey</c>, <c>oauth2</c>, <c>mtls</c>).</summary>
    public required string Scheme { get; init; }

    /// <summary>Identifier resolvable by the host's secrets-management adapter.</summary>
    public required string ReferenceId { get; init; }

    /// <summary>Optional last-rotation timestamp (UTC).</summary>
    public DateTimeOffset? RotatedAt { get; init; }

    /// <summary>Optional expiry timestamp (UTC).</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
