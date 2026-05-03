namespace Sunfish.Foundation.Taxonomy.Services;

/// <summary>
/// Thrown when a taxonomy operation violates a governance rule (Authoritative
/// regime guard, lineage immutability, monotonic tombstoning, Alter-requires-
/// reason, or node-code immutability post-publish).
/// </summary>
public sealed class TaxonomyGovernanceException : Exception
{
    /// <summary>Initializes the exception with a message.</summary>
    public TaxonomyGovernanceException(string message) : base(message) { }

    /// <summary>Initializes the exception with a message and inner exception.</summary>
    public TaxonomyGovernanceException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when <see cref="ITaxonomyRegistry.RegisterCorePackageAsync"/> is
/// called with data that conflicts with a previously registered package
/// having the same identity.
/// </summary>
public sealed class TaxonomyConflictException : Exception
{
    /// <summary>Initializes the exception with a message.</summary>
    public TaxonomyConflictException(string message) : base(message) { }

    /// <summary>Initializes the exception with a message and inner exception.</summary>
    public TaxonomyConflictException(string message, Exception inner) : base(message, inner) { }
}
