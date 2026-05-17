using System.Text.RegularExpressions;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.People.Foundation.Validation;

/// <summary>
/// E.164 validation for <see cref="PhoneNumber"/>. Format is strict so
/// downstream SMS / dialer integrations don't have to re-normalize. The
/// regex matches the spec: leading <c>+</c>, country digit 1–9, then 1–14
/// more digits (15-digit max total).
/// </summary>
public static class PhoneNumberValidator
{
    private static readonly Regex E164Pattern =
        new(@"^\+[1-9]\d{1,14}$", RegexOptions.Compiled);

    /// <summary>Validate the E.164 string in isolation.</summary>
    public static ValidationResult ValidateE164(string? e164)
    {
        if (string.IsNullOrWhiteSpace(e164))
            return ValidationResult.Fail("Phone E.164 string is required.");

        if (!E164Pattern.IsMatch(e164))
            return ValidationResult.Fail($"Phone '{e164}' is not a valid E.164 number (expected '+<country><subscriber>', 2–15 digits total).");

        return ValidationResult.Success;
    }

    /// <summary>Validate a full <see cref="PhoneNumber"/> entity.</summary>
    public static ValidationResult Validate(PhoneNumber phone)
    {
        if (phone is null) throw new ArgumentNullException(nameof(phone));
        return ValidateE164(phone.E164);
    }
}
