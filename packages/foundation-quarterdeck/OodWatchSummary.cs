namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Aggregate of the two OOD-role summaries surfaced on the Quarterdeck
/// per ADR 0080 §2.3 rule 2. Both fields are required; null
/// <see cref="OodRoleSummary.CurrentActorDisplayName"/> indicates an
/// inactive watch (the field-level distinction).
/// </summary>
/// <param name="OfficerOfTheDeck">
/// Watch summary for <see cref="Sunfish.Foundation.Wayfinder.OodRole.OfficerOfTheDeck"/>.
/// </param>
/// <param name="EngineeringOfficerOfTheWatch">
/// Watch summary for <see cref="Sunfish.Foundation.Wayfinder.OodRole.EngineeringOfficerOfTheWatch"/>.
/// </param>
public sealed record OodWatchSummary(
    OodRoleSummary OfficerOfTheDeck,
    OodRoleSummary EngineeringOfficerOfTheWatch);
