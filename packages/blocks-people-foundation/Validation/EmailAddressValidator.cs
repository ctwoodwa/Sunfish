using System.Net.Mail;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.People.Foundation.Validation;

/// <summary>
/// RFC 5322-shaped validation for <see cref="EmailAddress"/>. We delegate
/// the actual parse to <see cref="MailAddress"/>; rolling our own regex
/// for "looks like an email" is a documented bug factory.
/// </summary>
public static class EmailAddressValidator
{
    /// <summary>Validate the address string in isolation.</summary>
    public static ValidationResult ValidateAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return ValidationResult.Fail("Email address is required.");

        // MailAddress(string) accepts forms that surprise readers ("Display <a@b>")
        // because that's RFC 5322's full grammar. We reject those by requiring the
        // parsed Address property to equal the trimmed input, locking the surface
        // to bare addr-spec.
        var trimmed = address.Trim();
        try
        {
            var parsed = new MailAddress(trimmed);
            if (!string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Fail($"Email '{address}' must be a bare address (no display name).");
        }
        catch (FormatException)
        {
            return ValidationResult.Fail($"Email '{address}' is not a valid RFC 5322 address.");
        }

        return ValidationResult.Success;
    }

    /// <summary>Validate a full <see cref="EmailAddress"/> entity.</summary>
    public static ValidationResult Validate(EmailAddress email)
    {
        if (email is null) throw new ArgumentNullException(nameof(email));
        return ValidateAddress(email.Address);
    }
}
