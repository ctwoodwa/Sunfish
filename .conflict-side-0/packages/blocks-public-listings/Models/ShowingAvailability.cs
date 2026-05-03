namespace Sunfish.Blocks.PublicListings.Models;

/// <summary>How showings are scheduled for a public listing.</summary>
public sealed record ShowingAvailability
{
    /// <summary>Mode of scheduling.</summary>
    public required ShowingAvailabilityKind Kind { get; init; }

    /// <summary>Open-house slots, when <see cref="Kind"/> is <see cref="ShowingAvailabilityKind.OpenHouse"/>; otherwise empty.</summary>
    public IReadOnlyList<DateTimeOffset> OpenHouses { get; init; } = Array.Empty<DateTimeOffset>();

    /// <summary>Override link to ADR 0057's appointment-scheduling surface; null falls back to the platform default.</summary>
    public string? AppointmentLinkOverride { get; init; }
}
