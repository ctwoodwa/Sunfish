using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// A party on a messaging thread — an internal actor (operator, bookkeeper,
/// tenant) or an external party (vendor, applicant, prospect). Identity is
/// modeled via <see cref="ActorId"/> (per W#31 substitution) until a richer
/// discriminated-union <c>IdentityRef</c> primitive is introduced.
/// </summary>
public sealed record Participant
{
    /// <summary>Stable identifier for this participant's row in the messaging substrate.</summary>
    public required ParticipantId Id { get; init; }

    /// <summary>Underlying actor reference; resolves to the platform principal or external-party record.</summary>
    public required ActorId Identity { get; init; }

    /// <summary>Human-readable display name for the participant; surfaces on rendered threads + outbound recipient lists.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Email address, when this participant is reachable via <see cref="MessageChannel.Email"/>; null otherwise.</summary>
    public string? EmailAddress { get; init; }

    /// <summary>E.164 phone number, when this participant is reachable via <see cref="MessageChannel.Sms"/>; null otherwise.</summary>
    public string? PhoneNumber { get; init; }
}
