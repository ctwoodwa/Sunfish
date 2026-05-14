using System;
using Sunfish.UICore.Wayfinder.Integrations;

namespace Sunfish.Blocks.Integrations;

/// <summary>
/// Thrown when two <see cref="IIntegrationProviderValidator"/> instances share the same
/// <c>(SupportedCategory, SupportedProvider)</c> pair per ADR 0067 §6.2.1.
/// </summary>
public sealed class DuplicateValidatorRegistrationException : InvalidOperationException
{
    /// <summary>The integration category with the duplicate validator.</summary>
    public IntegrationCategory Category { get; }

    /// <summary>The provider id with the duplicate validator.</summary>
    public string ProviderId { get; }

    /// <param name="category">The duplicated category.</param>
    /// <param name="providerId">The duplicated provider id.</param>
    public DuplicateValidatorRegistrationException(IntegrationCategory category, string providerId)
        : base($"Duplicate IIntegrationProviderValidator registered for ({category}, \"{providerId}\"). "
               + "Per ADR 0067 §6.2.1, each (category, provider) pair must have at most one validator.")
    {
        Category = category;
        ProviderId = providerId;
    }
}
