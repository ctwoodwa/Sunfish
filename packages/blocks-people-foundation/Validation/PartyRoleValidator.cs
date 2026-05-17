using System.Text.RegularExpressions;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.People.Foundation.Validation;

/// <summary>
/// Shape validation for <see cref="PartyRole"/>. Enforces role-name format
/// (lowercase kebab-case, ≤ 64 chars), required fields, and chronological
/// ordering of <c>StartedAt</c> / <c>EndedAt</c>.
///
/// <para>
/// <b>Unknown but shape-valid role codes pass.</b> Per CRDT §5 the role
/// registry is open-set: future hand-offs add codes additively, so a write
/// containing <c>"landlord"</c> must round-trip cleanly even before that
/// code is canonical. <see cref="PartyRoleName.IsKnown"/> is informational,
/// not gating.
/// </para>
/// </summary>
public static class PartyRoleValidator
{
    // Lowercase kebab-case: one or more groups of [a-z0-9]+ separated by single dashes.
    // Anchored start-to-end; forbids leading/trailing dash and consecutive dashes.
    private static readonly Regex RoleNamePattern =
        new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    /// <summary>Validate a role-name string in isolation.</summary>
    public static ValidationResult ValidateRoleName(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return ValidationResult.Fail("Role name is required.");
        if (roleName.Length > 64)
            return ValidationResult.Fail($"Role name '{roleName}' exceeds 64 characters.");
        if (!RoleNamePattern.IsMatch(roleName))
            return ValidationResult.Fail($"Role name '{roleName}' must be lowercase kebab-case (a-z, 0-9, single dashes, no leading/trailing dash).");
        return ValidationResult.Success;
    }

    /// <summary>Validate a full <see cref="PartyRole"/> entity.</summary>
    public static ValidationResult Validate(PartyRole role)
    {
        if (role is null) throw new ArgumentNullException(nameof(role));
        var errors = new List<string>();

        var nameResult = ValidateRoleName(role.RoleName);
        if (!nameResult.IsValid)
            errors.AddRange(nameResult.Errors);

        if (string.IsNullOrWhiteSpace(role.RoleRecordId))
            errors.Add("RoleRecordId is required (opaque pointer into the consumer cluster's record).");

        if (role.EndedAt is { } ended && ended.Value < role.StartedAt.Value)
            errors.Add("PartyRole EndedAt must be at or after StartedAt.");

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }
}
