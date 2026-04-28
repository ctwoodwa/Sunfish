using System;
using System.Collections.Immutable;

namespace Sunfish.Analyzers.ProviderNeutrality;

/// <summary>
/// Registry of vendor SDK namespace prefixes that are forbidden in non-providers
/// packages (per ADR 0013 provider-neutrality). v0 covers the Phase 2 commercial
/// vendors named in the Phase 2 commercial intake; extension comes when new
/// vendors land per ADR 0013 follow-up #4.
/// </summary>
internal static class BannedVendorNamespaces
{
    /// <summary>
    /// Prefixes are matched case-sensitively and require either an exact match
    /// or a '.' boundary, so <c>Stripe</c> matches <c>Stripe</c> and
    /// <c>Stripe.PaymentIntents</c>, but NOT <c>StripeFake</c>.
    /// </summary>
    public static readonly ImmutableArray<string> Prefixes = ImmutableArray.Create(
        "Stripe",
        "Plaid",
        "SendGrid",
        "Twilio");

    /// <summary>
    /// Returns the matched prefix if <paramref name="namespaceName"/> equals or
    /// starts with one of the registry's prefixes followed by a '.', otherwise null.
    /// </summary>
    public static string? Match(string namespaceName)
    {
        if (string.IsNullOrEmpty(namespaceName))
        {
            return null;
        }

        foreach (var prefix in Prefixes)
        {
            if (IsPrefixMatch(namespaceName, prefix))
            {
                return prefix;
            }
        }

        return null;
    }

    private static bool IsPrefixMatch(string candidate, string prefix)
    {
        if (!candidate.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (candidate.Length == prefix.Length)
        {
            return true;
        }

        return candidate[prefix.Length] == '.';
    }
}
