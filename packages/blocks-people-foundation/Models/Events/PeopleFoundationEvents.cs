namespace Sunfish.Blocks.People.Foundation.Models.Events;

/// <summary>
/// Canonical event-type strings emitted by <c>InMemoryPartyRepository</c> via
/// <see cref="Sunfish.Foundation.Events.IDomainEventPublisher"/>. Per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c> §2: stable
/// dotted names, never renamed. Producer cluster = <c>People</c>.
/// </summary>
public static class PeopleFoundationEventNames
{
    /// <summary>A new <see cref="Party"/> was created.</summary>
    public const string PartyCreated = "People.PartyCreated";

    /// <summary>A <see cref="Party"/> was tombstoned.</summary>
    public const string PartyDeleted = "People.PartyDeleted";

    /// <summary>A <see cref="PartyRole"/> edge was attached.</summary>
    public const string RoleAttached = "People.RoleAttached";

    /// <summary>An attached <see cref="PartyRole"/> was detached (EndedAt set).</summary>
    public const string RoleDetached = "People.RoleDetached";

    /// <summary>An <see cref="EmailAddress"/> row was added.</summary>
    public const string EmailAddressAdded = "People.EmailAddressAdded";

    /// <summary>A <see cref="PhoneNumber"/> row was added.</summary>
    public const string PhoneNumberAdded = "People.PhoneNumberAdded";

    /// <summary>A <see cref="PartyAddress"/> row was added.</summary>
    public const string AddressAdded = "People.AddressAdded";
}

/// <summary>Payload for <see cref="PeopleFoundationEventNames.PartyCreated"/>.</summary>
public sealed record PartyCreatedPayload(
    PartyId PartyId,
    PartyKind Kind,
    string DisplayName);

/// <summary>Payload for <see cref="PeopleFoundationEventNames.PartyDeleted"/>.</summary>
public sealed record PartyDeletedPayload(
    PartyId PartyId,
    string? Reason);

/// <summary>Payload for <see cref="PeopleFoundationEventNames.RoleAttached"/>.</summary>
public sealed record RoleAttachedPayload(
    PartyRoleId PartyRoleId,
    PartyId PartyId,
    string RoleName,
    string RoleRecordId);

/// <summary>Payload for <see cref="PeopleFoundationEventNames.RoleDetached"/>.</summary>
public sealed record RoleDetachedPayload(
    PartyRoleId PartyRoleId,
    PartyId PartyId,
    string RoleName,
    string? EndedReason);

/// <summary>Payload for <see cref="PeopleFoundationEventNames.EmailAddressAdded"/>.</summary>
public sealed record EmailAddressAddedPayload(
    EmailAddressId EmailAddressId,
    PartyId PartyId,
    string Address,
    bool IsPrimary);

/// <summary>Payload for <see cref="PeopleFoundationEventNames.PhoneNumberAdded"/>.</summary>
public sealed record PhoneNumberAddedPayload(
    PhoneNumberId PhoneNumberId,
    PartyId PartyId,
    string E164,
    bool IsPrimary);

/// <summary>Payload for <see cref="PeopleFoundationEventNames.AddressAdded"/>.</summary>
public sealed record AddressAddedPayload(
    PartyAddressId PartyAddressId,
    PartyId PartyId,
    string CountryCode,
    bool IsPrimary);
