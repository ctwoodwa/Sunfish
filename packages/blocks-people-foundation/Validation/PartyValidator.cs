using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.People.Foundation.Validation;

/// <summary>
/// Structural rules for <see cref="Party"/>. These are entity-shape checks
/// only (kind/name coherence, parent-org guardrail); cross-collection rules
/// like "must have at least one contact method" live on
/// <c>IPartyWriteService.AttachRoleAsync</c> in PR 3, since those need the
/// party's email / phone / address rows in scope.
/// </summary>
public static class PartyValidator
{
    /// <summary>Validate a party in isolation. Returns the errors found; empty list = success.</summary>
    public static ValidationResult Validate(Party party)
    {
        if (party is null) throw new ArgumentNullException(nameof(party));
        var errors = new List<string>();

        // Display / legal naming must be coherent with kind.
        if (party.Kind == PartyKind.Person)
        {
            if (string.IsNullOrWhiteSpace(party.GivenName) && string.IsNullOrWhiteSpace(party.DisplayName))
                errors.Add("Person parties require GivenName or DisplayName.");
        }
        else // Organization
        {
            if (string.IsNullOrWhiteSpace(party.DisplayName) && string.IsNullOrWhiteSpace(party.LegalName))
                errors.Add("Organization parties require DisplayName or LegalName.");
        }

        // ParentOrgId only makes sense for organizations — a person can't be inside
        // an org-hierarchy edge (that's a role-attachment, not a parent pointer).
        if (party.ParentOrgId is not null && party.Kind != PartyKind.Organization)
            errors.Add("ParentOrgId may only be set on Organization parties.");

        return errors.Count == 0 ? ValidationResult.Success : new ValidationResult(errors);
    }
}
