using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.People.Foundation.Validation;

/// <summary>
/// Structural validation for <see cref="PartyAddress"/>. We enforce
/// (a) ISO 3166-1 alpha-2 shape on the country code and (b) chronological
/// ordering of ValidFrom / ValidTo. Postal-code format intentionally NOT
/// checked — alphanumeric postcodes exist (UK, CA) and a per-country format
/// table belongs in a follow-on data block, not this entity-level guard.
/// </summary>
public static class PartyAddressValidator
{
    /// <summary>Validate a full <see cref="PartyAddress"/> entity.</summary>
    public static ValidationResult Validate(PartyAddress addr)
    {
        if (addr is null) throw new ArgumentNullException(nameof(addr));
        var errors = new List<string>();

        // Required sub-fields on the inner Address.
        if (string.IsNullOrWhiteSpace(addr.Address.Line1))
            errors.Add("Address Line1 is required.");
        if (string.IsNullOrWhiteSpace(addr.Address.City))
            errors.Add("Address City is required.");
        if (string.IsNullOrWhiteSpace(addr.Address.Region))
            errors.Add("Address Region is required.");
        if (string.IsNullOrWhiteSpace(addr.Address.PostalCode))
            errors.Add("Address PostalCode is required.");

        // Country: two uppercase A-Z letters (ISO 3166-1 alpha-2). We don't
        // validate against the full code list — that table changes; the shape
        // check is enough to reject typos and forms like "USA" or "us".
        if (string.IsNullOrEmpty(addr.Address.Country))
        {
            errors.Add("Address Country (ISO 3166-1 alpha-2) is required.");
        }
        else if (!IsIso3166Alpha2(addr.Address.Country))
        {
            errors.Add($"Address Country '{addr.Address.Country}' must be ISO 3166-1 alpha-2 (two uppercase letters).");
        }

        // Validity-window ordering.
        if (addr.ValidFrom is { } from && addr.ValidTo is { } to && to.Value <= from.Value)
            errors.Add("PartyAddress ValidTo must be strictly after ValidFrom when both are set.");

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }

    private static bool IsIso3166Alpha2(string country) =>
        country.Length == 2 && country[0] >= 'A' && country[0] <= 'Z' && country[1] >= 'A' && country[1] <= 'Z';
}
